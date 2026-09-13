# Jellyfin Plugin Update Notifier

Shows a notification badge on the admin's profile avatar and an entry in the
profile menu when plugins have been installed or updated and the server needs a
restart. The entry opens **Plugin Updates** in the admin dashboard, listing what
changed, the old and new versions, and each release's changelog.

> **Jellyfin 12 only.** It uses APIs that do not exist in 10.x.

## Install

Add the repository in **Dashboard → Plugins → Repositories**:

```
https://raw.githubusercontent.com/mihaif7/jellyfin-plugin-updatenotifier/main/manifest.json
```

Then install **Plugin Update Notifier** from the catalogue and restart Jellyfin.

To install manually, download the zip from the
[latest release](https://github.com/mihaif7/jellyfin-plugin-updatenotifier/releases/latest),
extract it into `<config>/plugins/Plugin Update Notifier_<version>/`, and restart.

### Optional dependency

The dashboard page works on its own. The **avatar badge and profile-menu entry**
additionally require the
[JavaScript Injector](https://github.com/n00bcodr/Jellyfin-JavaScript-Injector)
plugin (4.0.0.0 or newer, for Jellyfin 12), which this plugin registers its
script with automatically. Without it the plugin logs a warning and the page
still works.

## How it works

| Piece | Mechanism |
| --- | --- |
| Detecting changes | `IEventConsumer<PluginInstalling/Updated/InstalledEventArgs>` |
| Pending-restart state | `IApplicationHost.HasPendingRestart`, maintained by the server |
| Startup work | `IScheduledTask` with `TaskTriggerInfoType.StartupTrigger` |
| Dashboard page | `IHasWebPages` with `EnableInMainMenu = true` |
| Badge and menu entry | JavaScript Injector's `PluginInterface.RegisterScript` |

State is written to `updates.json` in the plugin data folder, so a dismissal is
shared across browsers and devices rather than living in one browser. It is
cleared on the next startup, since a restart is what the notice was asking for.

### API

| Endpoint | Purpose |
| --- | --- |
| `GET /PluginUpdateNotifier/status` | `{ PendingRestart, Dismissed, Updates[] }` |
| `POST /PluginUpdateNotifier/dismiss` | Dismiss for all admin sessions |

Both require administrator privileges.

## Notes on Jellyfin 12

Things that changed from 10.x and will break a plugin ported naively:

- `Jellyfin.Controller` 12.0.0 targets **net10.0**. The plugin template's
  `renovate/jellyfin.controller-12.x` branch pairs it with `net9.0` and a stale
  `Jellyfin.Model 10.11.5`, and does not build.
- `IInstallationManager` **no longer exposes events**. They moved to
  `IEventConsumer<T>` under `MediaBrowser.Controller.Events.Updates`.
- `IServerEntryPoint` is gone. Use `IScheduledTask` + `StartupTrigger`.
- `IHasWebPages` moved from `MediaBrowser.Common.Plugins` to
  `MediaBrowser.Model.Plugins`.
- Plugin manifests declare `"targetAbi": "12.0.0.0"`.

### `PluginUpdatedEventArgs` does not fire for upgrades

`InstallPackageInternal` decides between "installed" and "updated" like this:

```csharp
LocalPlugin? plugin = _pluginManager.Plugins.FirstOrDefault(
    p => p.Id.Equals(package.Id) && p.Version.Equals(package.Version));
return plugin is not null;   // isUpdate
```

It matches on id *and the same version*, so a real upgrade (1.1.1.0 → 1.2.0.0)
finds nothing and is published as `PluginInstalledEventArgs`.
`PluginUpdatedEventArgs` only fires when the identical version is reinstalled.
A plugin listening for "updated" therefore never sees real upgrades, and the
flag cannot be trusted — this plugin consumes `PluginInstallingEventArgs` to
snapshot the previous version first, and derives the transition from that.

### Client-side traps

- The avatar button carries `aria-controls="app-user-menu"` and the dropdown is
  a `keepMounted` MUI `Menu` with `id="app-user-menu"`. Emotion class names
  (`css-mbeig7` and friends) are build-generated, so the injected entry clones
  its classes from a real menu item rather than hardcoding them.
- Jellyfin's `.fieldDescription` sets `white-space: normal !important`, which
  defeats an inline `white-space: pre-wrap`. Multi-line changelogs are rendered
  into real elements under plugin-scoped class names instead.
- Debouncing a `MutationObserver` with `requestAnimationFrame` looks right, but
  rAF is suspended in hidden tabs and latches the pending flag. A timer is used
  instead.

## Building

Requires the .NET 10 SDK.

```sh
dotnet build -c Release
```

The build runs StyleCop and the .NET analyzers with warnings as errors, so a
clean build is also the lint gate.

## Releasing

Push a tag and the release workflow does the rest — it builds, generates
`meta.json`, packages the zip, attaches it to the GitHub release, and prepends
the new version to `manifest.json`:

```sh
git tag v1.0.0.0 && git push origin v1.0.0.0
```

`manifest.json` is the single source of truth for the plugin's descriptive
metadata; `meta.json` is generated from it at release time.

## License

GPL-3.0, matching Jellyfin itself.
