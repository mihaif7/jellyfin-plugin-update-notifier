using System;
using System.Diagnostics.CodeAnalysis;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.UpdateNotifier.Configuration;

/// <summary>
/// Plugin configuration, edited on the plugin's Settings page.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the non-admin users who also get the avatar badge.
    /// </summary>
    /// <remarks>
    /// They see only that updates are pending and how many; the details and
    /// every action stay with administrators.
    /// </remarks>
    [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Both the XML and the JSON serializers of the configuration need a settable array.")]
    public Guid[] NotifyUserIds { get; set; } = Array.Empty<Guid>();
}
