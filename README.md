# Large Document Processing

Technical exercise submission: a construction-plan document platform where users upload large PDFs (5 MB–2 GB+), originals are retained for compliance, and the browser reads **pre-generated page artifacts**—never the full PDF on the view path.

**Start here for reviewers:** [SUBMISSION.md](SUBMISSION.md)

## Problem summary

- **50,000** active users  
- Peak **~2,000 document sets/hour**  
- **7-year** legal retention for all originals  
- **SLA:** any single page renders in the browser in under **2 seconds**

## Key requirements

| Requirement | Prototype | Production approach |
|-------------|-----------|---------------------|
| 50,000 active users | Not load-tested | Stateless API + CDN-served artifacts |
| 2,000 document sets/hour | In-memory queue | Durable queue + autoscaling workers |
| 7-year retention | `RetentionUntil` on upload | Object storage lifecycle policies |
| Sub-2-second page render | Pre-generated artifacts (placeholder SVG) | CDN + real rasterized page files |

## Prototype vs Production Components

The local prototype validates **flow and boundaries only**. Redis, RabbitMQ, S3, CDN, and real PDF rendering are **not implemented** in this repository—they are production targets behind swappable interfaces.

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

**Extension points in this codebase:** `IObjectStorageService`, `IDocumentProcessingQueue`, and `IPageArtifactGenerator`. Replace the Infrastructure implementations without changing the Application or Web layers.

## Core architecture decision

**The original PDF is stored for audit and retention but is not parsed or streamed for page viewing.**

Upload → store original → enqueue processing → worker generates `pages/{documentId}/page-{n}.*` → API returns artifact URLs.

Page artifacts in this prototype are **placeholder SVG files** (not rendered from the PDF). Production replaces `PlaceholderPageArtifactGenerator` with an `IPageArtifactGenerator` implementation (PDFium, MuPDF, Ghostscript, or a cloud render service).

See [ADR 0001: Use Page-Level Artifacts for Browser Rendering](docs/adr/0001-use-page-level-artifacts.md) for the full decision record and rejected alternatives.

## Document API (`/api/documents`)

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/documents/upload` | Upload PDF, enqueue processing |
| GET | `/api/documents/{id}` | Document metadata |
| GET | `/api/documents/{id}/pages` | Page list |
| GET | `/api/documents/{id}/pages/{n}` | Page status + `artifactUrl` when ready |

## Upload flow

1. `POST /api/documents/upload` (multipart PDF)  
2. `DocumentService` validates PDF type, writes `originals/{documentId}/{fileName}`  
3. `Document` row: `Status = Uploaded`, `RetentionUntil = CreatedAt + 7 years`  
4. Document ID enqueued on `IDocumentProcessingQueue`  
5. Response **202 Accepted** with `documentId` and status  

## Processing flow

1. `DocumentProcessingWorker` dequeues document ID  
2. `DocumentProcessingService` sets `Processing`  
3. `IPageArtifactGenerator` produces page artifacts (3 placeholder SVGs in prototype)  
4. `DocumentPage` rows created; document `TotalPages` and `Status = Ready`  

## Retrieval flow

1. `GET /api/documents/{id}` — metadata  
2. `GET /api/documents/{id}/pages` — page list and statuses  
3. `GET /api/documents/{id}/pages/{pageNumber}` — `artifactUrl` when `Ready` (original PDF is not opened)  

Artifacts are served from `/storage/...` on local disk in the prototype.

## Storage key structure

```
originals/{documentId}/{safeFileName}     # immutable original PDF
pages/{documentId}/page-{n}.svg           # prototype page artifacts
```

## Local prototype limitations

- Placeholder SVG artifacts — does not render uploaded PDF content  
- File storage on disk under `./storage`  
- In-memory `Channel<Guid>` queue (single process)  
- Demo owner: `demo-user` (no per-user auth on document APIs yet)  
- No antivirus or malware scanning on upload  
- Development database recreated on startup (`EnsureCreated`)  

## How to run locally

```bash
dotnet build LargeDocumentProcessing.slnx
dotnet run --project src/Web --no-build
```

Default URLs (see console output): **http://localhost:5156** and **https://localhost:7065**.  
Open API docs: `/scalar`

## How to test upload and page retrieval

From the repository root (uses included `samples/minimal.pdf`):

1. Upload:

```bash
curl -X POST "http://localhost:5156/api/documents/upload" -F "file=@samples/minimal.pdf"
```

Note the `documentId` in the response.

2. Poll until `status` is `Ready` (usually a few seconds):

```bash
curl "http://localhost:5156/api/documents/{documentId}"
```

3. List pages:

```bash
curl "http://localhost:5156/api/documents/{documentId}/pages"
```

4. Get page 1:

```bash
curl "http://localhost:5156/api/documents/{documentId}/pages/1"
```

5. Open `artifactUrl` in a browser, e.g. `http://localhost:5156/storage/pages/{documentId}/page-1.svg`

For HTTPS, use port **7065** and add `-k` to curl if using a dev certificate.

## Optional smoke test script

With the API running locally:

```bash
dotnet run --project src/Web --no-build
powershell -ExecutionPolicy Bypass -File scripts/smoke-test.ps1
```

The script checks reachability, uploads `samples/minimal.pdf`, polls until `Ready`, validates three pages, and fetches the page-1 artifact. Override the base URL with `-BaseUrl` if needed.

## Build and test

```bash
dotnet build LargeDocumentProcessing.slnx
dotnet test LargeDocumentProcessing.slnx
```

Automated tests include service-level functional tests (`IDocumentService`) and **HTTP integration tests** that exercise `/api/documents` endpoints via `WebApplicationFactory` (upload, poll, pages, artifact retrieval, non-PDF rejection).

## Further reading

- [SUBMISSION.md](SUBMISSION.md) — hiring-team summary  
- [docs/architecture.md](docs/architecture.md) — diagrams, scalability, trade-offs  
- [docs/adr/0001-use-page-level-artifacts.md](docs/adr/0001-use-page-level-artifacts.md) — ADR: page-level artifacts vs on-demand PDF parsing
