# Large Document Processing — Technical Exercise Submission

## 1. Executive Summary

This solution addresses a construction-plan document platform where users upload large PDFs (5 MB–2 GB+) and view individual pages in the browser under strict latency and retention requirements.

The prototype is a **Clean Architecture .NET application** with explicit application services (no MediatR or licensed CQRS). It demonstrates the full lifecycle: upload → persist original → enqueue processing → background worker → page artifacts → fast page retrieval.

The read path is deliberately decoupled from PDF parsing. That is the central design choice that makes a sub-2-second page SLA achievable at scale.

**Honest scope note:** Page rendering in this prototype uses **placeholder SVG files**, not real PDF rasterization. The abstraction (`IPageArtifactGenerator`) is in place so production can swap in PDFium, MuPDF, Ghostscript, or a cloud rendering service without changing the API or read path.

---

## 2. Key Architecture Decision

### Original PDFs are the source of truth

Every upload is stored immutably at:

```
originals/{documentId}/{safeFileName}
```

Metadata (`Document` entity) records owner, size, content type, status, and `RetentionUntil` (CreatedAt + 7 years). The original is retained for audit, legal hold, and re-processing if rendering pipelines change.

### Browser rendering uses pre-generated page artifacts

After upload, a background worker generates per-page artifacts:

```
pages/{documentId}/page-{n}.svg   (prototype)
pages/{documentId}/page-{n}.webp  (typical production)
```

The browser never receives or parses the multi-GB PDF for page view. It receives a URL to a small, cacheable artifact.

### The read path never parses the full PDF

`GET /api/documents/{id}/pages/{pageNumber}`:

1. Validates ownership
2. Reads page metadata from the database
3. Returns `artifactUrl` when status is `Ready`

It does **not** open `originals/...` on the request path. Upload and processing are the only stages that touch the original file.

**Why this matters:** Parsing a 2 GB PDF on every page request would make the 2-second SLA impossible and would saturate API servers. Pre-generation moves cost to async processing and makes reads O(1) relative to PDF size.

---

## 3. Requirement Mapping

| Requirement | Approach |
|-------------|----------|
| **50,000 active users** | Stateless Web tier; metadata in relational DB; artifacts on object storage + CDN. API nodes do not hold PDFs in memory. |
| **~2,000 document sets/hour (peak)** | Upload returns 202 immediately; processing is async via a queue. Workers scale independently of Web. |
| **7-year retention** | `RetentionUntil` set at upload. Originals in durable object storage with lifecycle policies (Glacier / Cool tier after hot period). |
| **Sub-2-second page rendering** | Pre-rendered artifacts served from CDN edge. API returns a URL in milliseconds; browser fetches a fixed-size asset. Optional thumbnails for first paint. |

The prototype validates the **flow and boundaries**. It does not load-test 50k users or 2k uploads/hour.

---

## 4. What This Prototype Implements

| Capability | Implementation |
|------------|----------------|
| PDF upload | `POST /api/documents/upload` — validates PDF, stores original, creates DB record |
| Metadata | `GET /api/documents/{id}` |
| Page list | `GET /api/documents/{id}/pages` |
| Single page | `GET /api/documents/{id}/pages/{pageNumber}` — status + `artifactUrl` when ready |
| Async processing | `DocumentProcessingWorker` + `InMemoryDocumentProcessingQueue` |
| Artifact generation | `PlaceholderPageArtifactGenerator` — 3 SVG pages per document |
| Local storage | `LocalFileObjectStorageService` under `./storage`, served at `/storage` |
| Retention field | `RetentionUntil = CreatedAt + 7 years` |
| Clean Architecture | Domain / Application / Infrastructure / Web with service interfaces |

Layers use explicit services (`IDocumentService`, `IObjectStorageService`, etc.) — no MediatR.

---

## Prototype vs Production Components

The table below separates what runs locally from the production target. **Redis, RabbitMQ, S3, CDN, and real PDF rendering are not implemented in this prototype**—they are architectural targets wired through extension points.

| Concern | Prototype | Production |
|--------|-----------|------------|
| Queue | In-memory `Channel<Guid>` | RabbitMQ / SQS / Azure Service Bus |
| Storage | Local disk `./storage` | S3 / Azure Blob |
| Page delivery | `/storage` static files | CDN + signed URLs |
| Cache | None | Redis for metadata / signed URL cache |
| Renderer | Placeholder SVG | PDFium / MuPDF / Ghostscript / cloud renderer |
| Database | SQLite / EF Core local | PostgreSQL / SQL Server |
| Worker scaling | Single `BackgroundService` | Horizontally scaled worker pool |
| Upload path | Multipart upload through Web API | Presigned multipart upload directly to object storage |
| Retention | `RetentionUntil` field | Object lifecycle policy + legal hold |
| Resilience | Basic `Failed` status | Retry, DLQ, visibility timeout, idempotent reprocessing |
| Security | `demo-user` | Real authentication, authorization, signed URLs, antivirus scanning |

**Swappable interfaces:** `IObjectStorageService`, `IDocumentProcessingQueue`, `IPageArtifactGenerator`. The Application layer and HTTP API remain unchanged when Infrastructure is replaced.

---

## 5. Production Architecture

```
Client
  → Load Balancer
  → Web API (stateless, autoscale)
       → Metadata DB (PostgreSQL / SQL Server)
       → Object Storage (S3 / Azure Blob) — originals + artifacts
       → Message Queue (SQS / Service Bus / RabbitMQ)
  → Render Workers (autoscale on queue depth)
       → IPageArtifactGenerator (PDFium / MuPDF / Ghostscript / cloud API)
       → Write artifacts back to object storage
  → CDN (CloudFront / Azure CDN) — artifact egress only
  → Redis (optional) — page metadata cache, signed URL cache
```

**Separation of concerns:**

- **Web:** auth, upload orchestration, metadata queries, signed URL issuance
- **Workers:** CPU/IO-heavy PDF processing, isolated from user-facing latency
- **Storage:** originals (long retention) vs artifacts (cache-friendly, versioned per render)
- **CDN:** serves artifacts; not used for mutable API or originals

---

## 6. Scalability Strategy

**Upload burst (2k/hour):**

- Accept upload stream directly to object storage (multipart / presigned URL for very large files)
- Enqueue `documentId` only after durable storage commit
- Return 202; no synchronous rendering

**Read scale (50k users):**

- Page GET is a DB lookup + URL generation — horizontally scalable
- Artifacts are immutable and CDN-cacheable (`Cache-Control: public, max-age=31536000, immutable`)
- Hot documents benefit from edge cache without hitting origin

**Processing scale:**

- Worker count driven by queue depth and age-of-oldest-message alarms
- Idempotent processing keyed by `documentId` (safe retries)
- Large PDFs: stream from object storage; avoid loading full file into memory where possible

**Metadata:**

- Index on `OwnerUserId`, `CreatedAt`; unique `(DocumentId, PageNumber)`
- Partition or shard by tenant if row count grows into billions

---

## 7. Failure Handling

| Scenario | Behavior |
|----------|----------|
| Invalid upload (non-PDF) | 400; no storage write |
| Storage write failure | Upload fails; no DB commit |
| Processing error | `Document.Status = Failed`, `FailureReason` persisted |
| Worker crash mid-job | Production: message visibility timeout → redelivery; idempotent re-process or versioned artifacts |
| Page not yet ready | GET page returns status (`Pending` / `Processing`) without `artifactUrl` |
| Partial page failure | Extensible: per-page `Failed` status with reason |
| Renderer upgrade | Re-enqueue document; write artifacts under new version prefix |

Dead-letter queue for messages that exceed retry limits. Alert on DLQ depth and processing failure rate.

---

## 8. Security Considerations

**Prototype gaps (intentional):**

- Document APIs use fixed owner `demo-user` — no real auth on document routes yet
- Artifacts served from local `/storage` without signed URLs

**Production requirements:**

- **Authentication / authorization:** JWT or Entra ID; every query scoped by `OwnerUserId` (or tenant/project ACL)
- **Upload:** size limits, content-type validation, malware scanning before processing
- **Storage access:** time-limited signed URLs (SAS / CloudFront signed cookies); no public bucket listing
- **Path traversal:** storage keys validated (implemented in `LocalFileObjectStorageService`)
- **Encryption:** at rest (SSE-KMS / Azure CMK) and in transit (TLS)
- **Audit:** who uploaded, who viewed which page, retention and legal-hold events

---

## 9. Observability

**Metrics (production):**

- Upload rate, processing queue depth, age of oldest message
- Processing duration p50/p95/p99 per document and per page
- Page GET latency and CDN cache hit ratio
- Failure rate by stage (upload / process / render)
- Storage growth (originals vs artifacts)

**Tracing:**

- Correlation ID from upload through queue → worker → artifact write
- Span per page render for bottleneck analysis

**Logging:**

- Structured logs with `documentId`, `pageNumber`, `ownerUserId`
- No PDF content in logs

**Alerting:**

- Queue lag above SLO threshold
- Processing failure rate spike
- CDN origin error rate
- Retention policy job failures

The prototype logs worker start/stop and per-document success/failure via `ILogger`. Full telemetry is not wired.

---

## 10. Prototype Limitations

- **Placeholder artifacts:** `PlaceholderPageArtifactGenerator` writes 3 SVG files per document. It does not read or render the uploaded PDF.
- **In-memory queue:** `Channel<Guid>` — single process, not durable across restarts.
- **Local disk storage:** Not suitable for multi-instance deployment or 2 GB+ at scale without shared storage.
- **SQLite:** Fine for demo; production needs a managed relational DB.
- **No auth on document APIs:** Uses `demo-user`.
- **No multipart/presigned upload:** Large files stream through the Web process.
- **No antivirus, tiling, or thumbnails:** Interfaces allow extension; not implemented.
- **Development DB reset:** `EnsureCreated` / `EnsureDeleted` on startup in Development.

These limitations are deliberate to keep the exercise focused on architecture and boundaries.

---

## 11. Future Improvements

1. **Real renderer:** Replace `PlaceholderPageArtifactGenerator` with PDFium, MuPDF, Ghostscript, or AWS/Azure document rendering. Worker and read API unchanged.
2. **Durable queue:** SQS, Service Bus, or RabbitMQ with DLQ and retry policy.
3. **Object storage + CDN:** S3/Blob for originals and artifacts; CloudFront/CDN for reads.
4. **Presigned multipart upload:** Browser uploads directly to storage; API only registers metadata.
5. **Tiling / deep zoom:** IIIF or custom tile pyramid for large construction sheets.
6. **Thumbnails:** Low-res first paint while full artifact loads.
7. **Redis cache:** Page metadata and signed URL cache.
8. **Retention automation:** Lifecycle rules + legal hold API.
9. **Re-processing pipeline:** Re-render when renderer version changes.

---

## 12. How To Run and Test

### Prerequisites

- .NET 10 SDK
- Clone this repository

### Build

```bash
dotnet build LargeDocumentProcessing.slnx
dotnet test  LargeDocumentProcessing.slnx
```

Automated coverage includes service-level functional tests and **HTTP integration tests** (`DocumentApiHttpTests`) that call `/api/documents` through `WebApplicationFactory`: PDF upload (202), poll until Ready, page list, artifact URL, SVG artifact GET, and non-PDF rejection (400). Manual curl steps below remain the reviewer quick-check.

### Run

```bash
dotnet build LargeDocumentProcessing.slnx
dotnet run --project src/Web --no-build
```

Default URLs: **http://localhost:5156** and **https://localhost:7065** (see console output).  
Open API docs: `http://localhost:5156/scalar`

In Scalar, use the **Documents** tag (`/api/documents`).

### Test the document flow

From the repository root (uses included `samples/minimal.pdf`):

1. **Upload a PDF**

```bash
curl -X POST "http://localhost:5156/api/documents/upload" -F "file=@samples/minimal.pdf"
```

Response: `202 Accepted` with `documentId` and `status`.

2. **Poll until ready**

```bash
curl "http://localhost:5156/api/documents/{documentId}"
```

Wait until `status` is `Ready` (background worker processes within seconds locally).

3. **List pages**

```bash
curl "http://localhost:5156/api/documents/{documentId}/pages"
```

4. **Get page 1**

```bash
curl "http://localhost:5156/api/documents/{documentId}/pages/1"
```

Response includes `artifactUrl` (e.g. `/storage/pages/{documentId}/page-1.svg`).

5. **View artifact in browser**

```
http://localhost:5156/storage/pages/{documentId}/page-1.svg
```

The SVG is a **placeholder** (document ID and page number only). Production replaces `PlaceholderPageArtifactGenerator` with a real `IPageArtifactGenerator` (PDFium, MuPDF, Ghostscript, or cloud rendering).

### Further reading

- [README.md](README.md) — project overview and storage layout
- [docs/architecture.md](docs/architecture.md) — Mermaid diagrams, ERD, detailed trade-offs
