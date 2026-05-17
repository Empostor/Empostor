# Hello Page (Home Page)

Empostor serves a customisable home page at `http://your-server:22023/`.

## How it works

On startup, the server checks for a `Pages/` directory next to the executable. If it does not exist it is created automatically, and a default `index.html` is written into it.

```
Impostor.Server (binary)
Pages/
  index.html   ← served at GET /
```

Every request to `/` reads the file from disk at that moment, so edits take effect without restarting the server.

## Customising the page

Edit `Pages/index.html` with any HTML content you want. Common uses:

- Server rules and information
- Discord invite link
- Region file download instructions
- Link to the admin panel

If the file is deleted, a plain fallback response is returned until the server is restarted and regenerates the default file.

## Default page

The generated default page contains:

- A status indicator showing the server is online
- A link to the admin panel (`/admin`)
- A link to the [region file generator](https://impostor.github.io/Impostor)
- The server start time

## Notes

- The page is served as `text/html; charset=utf-8`.
- There is no template engine. The file is served as-is.
- For HTTPS access, configure a reverse proxy. See [Http-server.md](Http-server.md).
