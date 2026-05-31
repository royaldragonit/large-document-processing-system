# ADR 0001: Use Page-Level Artifacts for Browser Rendering

**Status:** Accepted

## Context

- Users upload construction plan PDFs from 5 MB to 2 GB+
- 50,000 active users
- ~2,000 document sets/hour
- 7-year retention
- Any individual page should render in the browser in under 2 seconds

Construction plans are large, multi-page documents. A single upload may contain hundreds of sheets at gigabyte scale. The platform must retain the original file for legal and audit purposes while allowing fast, per-page viewing in the browser at high concurrency.

## Decision

- Store the original PDF as the source of truth for retention and reprocessing.
- Asynchronously process the PDF into page-level artifacts.
- Serve page artifacts on the read path instead of parsing or streaming the full PDF.

The read API returns metadata and a URL to a pre-generated artifact (e.g. SVG/WebP in production). It does not open or parse `originals/...` on page requests.

## Consequences

### Positive

- **Predictable read latency** — page GET is a metadata lookup plus URL issuance, independent of PDF size.
- **CDN-friendly immutable assets** — each page artifact has a stable key and long cache TTL.
- **Workers scale independently from Web API** — CPU/IO-heavy rendering stays off the request path.
- **Renderer can be upgraded and documents reprocessed** — swap `IPageArtifactGenerator` without changing the HTTP contract.

### Negative

- **More storage usage** — originals plus one or more artifact formats per page.
- **More processing pipeline complexity** — queue, workers, status tracking, failure handling.
- **Eventual consistency** — pages are not available immediately after upload.
- **Production needs retries, DLQ, and reprocessing** — not fully implemented in the local prototype.

## Alternatives considered

### 1. Serve the original PDF directly

The browser or a client-side PDF library downloads and parses the full file for every viewing session.

**Rejected.** A 2 GB PDF cannot be fetched and parsed within a 2-second page SLA. Bandwidth and client memory scale with file size, not page count. CDN caching of the whole object does not help random page access. Retention still requires storing the original, but it must not be the read path.

### 2. Split or render pages on demand

On `GET /pages/{n}`, the API opens the stored PDF, seeks to page *n*, rasterizes, and returns the result (or streams a tile).

**Rejected.** On-demand rendering ties read latency to PDF parse cost and server CPU. Under peak load (50k users, many concurrent page views), API nodes become render bottlenecks. Cold starts on rarely viewed pages spike latency above 2 seconds. Caching helps only after the first expensive render; the first viewer still pays full cost. This couples read traffic to the same resource constraints as batch processing without the benefit of pre-warming artifacts at upload time.

### 3. Pre-generate page-level artifacts (chosen)

After upload, a worker generates fixed-size artifacts per page. Reads return URLs to those artifacts only.

**Accepted.** This is the only option that makes the sub-2-second requirement structurally achievable at scale:

| Factor | On-demand (1–2) | Pre-generated (3) |
|--------|-------------------|-------------------|
| Read-time work | O(PDF size) parse/render | O(1) DB + URL |
| API CPU on page view | High | Minimal |
| CDN edge cache | Poor (dynamic or first-hit miss) | Strong (immutable blobs) |
| Tail latency under load | Unbounded | Bounded by artifact size + CDN RTT |

Upload and processing absorb cost asynchronously (aligned with ~2k sets/hour as a queue/worker problem). The browser fetches a small, cacheable asset—typically well under 2 seconds from CDN edge after the API returns the URL in milliseconds.

The original PDF remains stored for 7-year retention and for reprocessing when the renderer or output format changes. The prototype uses placeholder SVGs via `PlaceholderPageArtifactGenerator`; production replaces this with PDFium, MuPDF, Ghostscript, or a cloud renderer behind the same interface.

## References

- [README.md](../../README.md) — flows and prototype vs production table
- [docs/architecture.md](../architecture.md) — sequences, ERD, scalability
- [System design diagram](../images/large-pdf-processing-system-design.png) — production target architecture (PNG)
