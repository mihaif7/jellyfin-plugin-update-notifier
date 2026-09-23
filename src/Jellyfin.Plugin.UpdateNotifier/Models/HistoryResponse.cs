using System.Collections.Generic;

namespace Jellyfin.Plugin.UpdateNotifier.Models;

/// <summary>
/// The payload returned by the history endpoint.
/// </summary>
/// <remarks>
/// Kept apart from the status payload because the page only asks for it when
/// the history section is opened, and every record carries a full changelog.
/// </remarks>
public class HistoryResponse
{
    /// <summary>Gets or sets the archived records, newest first.</summary>
    public IReadOnlyList<PluginUpdateRecord> Records { get; set; } = new List<PluginUpdateRecord>();
}
