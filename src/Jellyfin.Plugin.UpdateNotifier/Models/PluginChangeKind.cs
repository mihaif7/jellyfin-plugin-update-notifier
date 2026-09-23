namespace Jellyfin.Plugin.UpdateNotifier.Models;

/// <summary>
/// The kind of change a recorded entry describes.
/// </summary>
public enum PluginChangeKind
{
    /// <summary>A plugin that was not installed before.</summary>
    Installed = 0,

    /// <summary>A plugin whose installed version was replaced.</summary>
    Updated = 1,

    /// <summary>A plugin that was uninstalled.</summary>
    Removed = 2,
}
