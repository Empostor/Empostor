# Plugin Marketplace Format

The admin panel plugin marketplace reads `marketplace/plugins.json` at startup.

## JSON Schema

```jsonc
[
  {
    // Unique plugin identifier (reverse-domain style recommended)
    "id": "cn.hayashiume.welcome",

    // Display name shown in the marketplace
    "name": "Welcome Messages",

    // Short description (shown as subtitle)
    "description": "Sends a localised welcome message when players join a room.",

    // Author attribution
    "author": "BunchHanpiDev & HayashiUme",

    // Version history — latest version should be LAST in the array
    "versions": [
      {
        // Semantic version of this plugin release
        "version": "1.0.0",

        // Minimum Empostor / Impostor version required
        "impostor_version": "2.0.0",

        // Direct download URL for the .dll file
        "download_url": "https://raw.githubusercontent.com/Empostor/Empostor/master/marketplace/Impostor.Plugins.Welcome.dll"
      }
    ]
  }
]
```

## Versioning Rules

- **`versions` array**: Each entry represents one downloadable release. List older versions first; the **last entry** is treated as the latest and is selected by default in the admin panel UI.
- **Single-version plugins**: An array with one entry is valid. The version dropdown is hidden and the sole version is used directly.
- **Multi-version plugins**: When two or more entries exist, the admin panel shows a `<select>` dropdown letting the operator pick which version to install.
- **`download_url`**: Must point directly to a compiled `.dll` plugin file. The admin panel downloads this URL and saves it to the plugins directory.

## Adding a Plugin

1. Place the `.dll` file in `marketplace/` (or host it at an accessible URL).
2. Add a new entry to `plugins.json` with the fields above.
3. The admin panel reads `GET /api/admin/marketplace/plugins` which returns the full `plugins.json` contents — no server restart needed for the list to appear, but a restart is required after a plugin is installed.
