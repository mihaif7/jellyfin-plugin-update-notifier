<h1 align="center">Plugin Update Notifier</h1>
<h2 align="center">A Jellyfin Plugin</h2>

<p align="center">
  <img alt="Plugin Update Notifier" src="docs/images/logo.png" width="70%" />
</p>

<p align="center">
  Badges the admin avatar and adds a profile-menu entry when plugins have been
  updated and the server needs a restart, with a dashboard page listing what
  changed and each release's changelog.
</p>

<p align="center">
  <a href="https://github.com/mihaif7/jellyfin-plugin-updatenotifier/blob/main/LICENSE"><img alt="License" src="https://img.shields.io/github/license/mihaif7/jellyfin-plugin-updatenotifier?labelColor=black&color=00A4DC&cacheSeconds=3600" /></a>
  <a href="https://github.com/mihaif7/jellyfin-plugin-updatenotifier/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/mihaif7/jellyfin-plugin-updatenotifier?labelColor=black&color=AA5CC3&cacheSeconds=3600" /></a>
  <img alt="Jellyfin version" src="https://img.shields.io/badge/Jellyfin-12.x-AA5CC3?logo=jellyfin&logoColor=00A4DC&labelColor=black" />
  <a href="https://github.com/mihaif7/jellyfin-plugin-updatenotifier/actions/workflows/build.yml"><img alt="Build" src="https://img.shields.io/github/actions/workflow/status/mihaif7/jellyfin-plugin-updatenotifier/build.yml?branch=main&labelColor=black&color=00A4DC&cacheSeconds=3600" /></a>
  <img alt="Downloads" src="https://img.shields.io/github/downloads/mihaif7/jellyfin-plugin-updatenotifier/total?labelColor=black&color=AA5CC3&cacheSeconds=3600" />
</p>

> [!NOTE]
> **Jellyfin 12 only.** The plugin uses APIs that do not exist in 10.x.

<p align="center">
  <img alt="Avatar badge and profile-menu entry" src="docs/images/badge.jpg" width="45%" />
  <img alt="Plugin Updates dashboard page" src="docs/images/dashboard.png" width="45%" />
</p>

## ✨ Features

- **Avatar badge**: a dot on the admin's profile avatar the moment a plugin change leaves a restart pending.
- **Profile-menu entry**: *Plugin Updates (N)* in the user dropdown, shown only to administrators.
- **Dashboard page**: lists every changed plugin, its old → new version, and the release changelog, themed to match your server.
- **One-click dismiss**: dismissing is shared across every browser and device, not stored per-browser.
- **Self-clearing**: the notice clears automatically on the next restart, since a restart is exactly what it was asking for.

## 🚀 Installation

1. In Jellyfin, open **Dashboard → Plugins → Repositories**.
2. Click **➕** and add the repository:

> [!NOTE]
> ```
> https://raw.githubusercontent.com/mihaif7/jellyfin-plugin-updatenotifier/main/manifest.json
> ```

3. Open the **Catalog** tab, find **Plugin Update Notifier**, and click **Install**.
4. **Restart** your Jellyfin server.

> [!IMPORTANT]
> The **avatar badge and profile-menu entry** additionally require the
> [JavaScript Injector](https://github.com/n00bcodr/Jellyfin-JavaScript-Injector)
> plugin (4.0.0.0 or newer, for Jellyfin 12), which this plugin registers its
> script with automatically. The dashboard page works without it; the plugin
> just logs a warning and skips the badge.

<details>
<summary>Manual installation</summary>

Download the zip from the
[latest release](https://github.com/mihaif7/jellyfin-plugin-updatenotifier/releases/latest),
extract it into `<config>/plugins/Plugin Update Notifier_<version>/`, and restart.
</details>

## 🤝 Contributing

Bug reports and feature requests are welcome via the
[issue tracker](https://github.com/mihaif7/jellyfin-plugin-updatenotifier/issues).
Pull requests that keep within the plugin's scope are happily reviewed.

Build with the .NET 10 SDK:

```sh
dotnet build src -c Release
```

## 📝 License

[GPL-3.0](LICENSE), matching Jellyfin itself.
