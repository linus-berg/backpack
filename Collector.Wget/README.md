# Collector.Wget

Mirrors a website with a built-in crawler (AngleSharp parses the HTML; no `wget` binary is
used), rewrites links between the downloaded files to relative paths, and stores the
files in S3 under `wget/`.

## Queue

| Queue            | Concurrency |
|------------------|-------------|
| `collector-wget` | 10          |

**Routing caveat.** Collectors are picked by URL scheme, so only `wget://...` locations
reach this queue, but the crawler fetches only `http` and `https` URLs. A `wget://`
location is skipped straight away and logged as a successful mirror of 0 resources. The
crawler does real work only for messages put on `collector-wget` directly with an
`http(s)://` URL (for example by Backpack.Tester's `WebMirrorTest`).

## How a request is handled

For a start URL such as `https://example.com/docs/`:

1. Scope: the same host (any scheme or port), and paths under the start URL's directory,
   i.e. its path up to the last `/`. `https://example.com/docs` (no trailing slash)
   therefore covers the whole host.
2. Each URL is fetched once with `GET` (fragment ignored, query string kept),
   `User-Agent: Backpack/1.0`, at most 10 redirects, gzip/deflate/brotli decoding, and
   60 seconds to receive the headers. At most 8 requests run at a time and the crawl
   stops at depth 50.
3. HTML and CSS up to 10 MB (or of unknown size) are kept in memory. HTML means a
   `text/html` or `application/xhtml+xml` content type, or a URL ending in `.html`,
   `.htm`, `.xhtml`, `.asp`, `.aspx`, `.php`, `.jsp`, `.shtml` or with no extension. CSS
   means `text/css` or `.css`. Links are taken from `a[href]`, `link[href]`,
   `script[src]`, `img[src|srcset]`, `source[src|srcset]`, `video[src|poster]`,
   `audio[src]`, `embed[src]`, `object[data]`, `iframe[src]`, `style` attributes,
   `<style>` blocks, and CSS `url()` and `@import "..."`. Links in scope are crawled.
4. Everything else is uploaded to S3 while the crawl runs (via a temp file).
5. When the crawl is done, quoted `href`, `src`, `action`, `poster`, `data` and `srcset`
   attributes and CSS `url()` values that point to downloaded URLs are rewritten to
   relative paths, and the HTML and CSS files are uploaded.

There is no existence check: every request downloads and overwrites everything. `module`
and `force` are ignored, no delta links are written, and `robots.txt` is not read.

## Storage layout

`wget/<host>/<path>`, where the path is percent-decoded, a path ending in `/` gets
`index.html`, HTML whose extension is not one of the HTML extensions above gets `.html`
appended, a query string is folded into the file name (`?` and `&` become `@`, `=` becomes
`_`) and `<>:"|?*` become `_`.

```text
https://example.com/docs/                      -> wget/example.com/docs/index.html
https://example.com/docs/guide   (text/html)   -> wget/example.com/docs/guide.html
https://example.com/docs/search?q=a&p=2        -> wget/example.com/docs/search@q_a@p_2.html
https://example.com/docs/img/logo.png          -> wget/example.com/docs/img/logo.png
```

## Configuration

No variables of its own. Needs the S3 variables in
[Collector.Kernel](../Collector.Kernel/README.md) and the shared variables in
[docs/Configuration.md](../docs/Configuration.md). Proxies follow the .NET defaults
(`HTTP_PROXY`, `HTTPS_PROXY`, `NO_PROXY`). Logs go to the console only.

## Failure behaviour

- HTTP errors, timeouts and connection failures for single URLs are logged as warnings
  and the URL is skipped.
- A failed upload of a file during the crawl is logged as a warning; the crawl goes on.
- An HTML/CSS upload that still fails after the storage retries stops the remaining
  uploads and the mirror is logged as failed.
- In every case the message completes normally; nothing is retried or faulted.

## Limitations

- A page keeps its request slot until every page it links to has been crawled. Once 8
  pages are waiting like this, no request can start and the crawl hangs until the
  180-minute consumer timeout cancels it. HTML and CSS are uploaded only at the end, so
  a cancelled crawl stores only the other files.
- URLs without an extension are treated as HTML even when they aren't: a binary download
  such as `/download/latest` is held in memory (with no size limit if the server sends no
  `Content-Length`) and stored with `.html` appended.
- All HTML and CSS stays in memory until the crawl ends.
- Pages are decoded and re-encoded as UTF-8 regardless of their declared charset.
- Requisites must be in scope too: images, CSS or scripts on other hosts or outside the
  start directory are not downloaded, and links to them are left unchanged.
- Once headers have arrived, a download has no time limit.
- Links built by JavaScript are not found.
