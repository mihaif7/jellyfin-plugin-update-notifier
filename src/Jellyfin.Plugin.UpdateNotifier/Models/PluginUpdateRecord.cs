using System;

namespace Jellyfin.Plugin.UpdateNotifier.Models;

/// <summary>
/// A single recorded plugin install, update or removal.
/// </summary>
public class PluginUpdateRecord
{
    /// <summary>Gets or sets the plugin id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the plugin name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the version installed before the update, if any.</summary>
    public string? OldVersion { get; set; }

    /// <summary>Gets or sets the newly installed version, or the removed version.</summary>
    public string NewVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the changelog supplied by the repository.</summary>
    public string? Changelog { get; set; }

    /// <summary>Gets or sets the URL the package was installed from, if known.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Gets or sets the kind of change this entry describes.</summary>
    public PluginChangeKind Kind { get; set; }

    /// <summary>Gets or sets the UTC time the change was recorded.</summary>
    public DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the restart that made this change live.
    /// </summary>
    /// <remarks>
    /// Null while the change is still pending; set when the record is archived
    /// on the next startup, which is the moment the new version took effect.
    /// </remarks>
    public DateTime? RestartedAtUtc { get; set; }

    /// <summary>
    /// Returns a detached copy, so a reader holds a record no later change can
    /// rewrite under it.
    /// </summary>
    /// <returns>The copy.</returns>
    public PluginUpdateRecord Copy() => (PluginUpdateRecord)MemberwiseClone();
}
