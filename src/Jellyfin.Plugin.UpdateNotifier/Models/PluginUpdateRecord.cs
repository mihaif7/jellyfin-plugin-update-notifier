using System;

namespace Jellyfin.Plugin.UpdateNotifier.Models;

/// <summary>
/// A single recorded plugin install or update.
/// </summary>
public class PluginUpdateRecord
{
    /// <summary>Gets or sets the plugin id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the plugin name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the version installed before the update, if any.</summary>
    public string? OldVersion { get; set; }

    /// <summary>Gets or sets the newly installed version.</summary>
    public string NewVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the changelog supplied by the repository.</summary>
    public string? Changelog { get; set; }

    /// <summary>Gets or sets the URL the package was installed from, if known.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Gets or sets a value indicating whether this replaced an existing install.</summary>
    public bool IsUpdate { get; set; }

    /// <summary>Gets or sets the UTC time the install completed.</summary>
    public DateTime TimestampUtc { get; set; }
}
