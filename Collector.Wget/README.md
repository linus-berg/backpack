# Collector.Wget

Native C# website mirroring collector that recursively downloads and archives websites to S3 storage.

## Overview

This collector provides the equivalent of `wget --mirror -k -p -E --no-parent` implemented
entirely in managed C# — no external binaries required. When an `ArtifactCollectRequest`
arrives on the `collector-wget` queue, it:

1. **Crawls** the target URL recursively, discovering linked pages and embedded resources
2. **Downloads** all page requisites (CSS, JS, images, fonts, media)
3. **Rewrites links** in HTML and CSS files to point to local relative paths
4. **Adjusts extensions** (e.g., adds `.html` to extensionless URLs serving HTML)
5. **Uploads** everything to S3/Minio via the `FileSystem` abstraction

## Feature Parity with `wget`

| wget Flag | C# Equivalent | Description |
|-----------|---------------|-------------|
| `--mirror` | Recursive BFS crawl with visited-URL tracking | Recursive download with infinite depth |
| `-k` | `RewriteHtmlLinks` / `RewriteCssLinks` | Convert absolute URLs to relative local paths |
| `-p` | Full HTML tag extraction (img, link, script, etc.) | Download all page requisites |
| `-E` | `UriToLocalPath` extension adjustment | Save `.html` extension for HTML content |
| `--no-parent` | `IsWithinScope` boundary check | Stay within the base URL path |

## Architecture

```
ArtifactCollectRequest (RabbitMQ)
        │
        ▼
   Consumer.cs ──── receives message, delegates to WebMirror
        │
        ▼
   WebMirror.cs ─── crawl engine
        │
        ├── Phase 1: Recursive crawl + download
        │   ├── HTML parsing (AngleSharp)
        │   ├── CSS url()/import extraction
        │   └── Concurrent HTTP fetching (8 parallel)
        │
        └── Phase 2: Link rewriting + S3 upload
            ├── HTML link conversion
            ├── CSS url() rewriting
            └── FileSystem.PutFile() → Minio S3
```

## Dependencies

- **AngleSharp** — HTML parsing and link extraction
- **Collector.Kernel** — S3/Minio `FileSystem` abstraction
- **Core.Kernel** — MassTransit registration, configuration, messaging
