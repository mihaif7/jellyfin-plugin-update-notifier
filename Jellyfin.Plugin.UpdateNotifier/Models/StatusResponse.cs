using System.Collections.Generic;

namespace Jellyfin.Plugin.UpdateNotifier.Models;

/// <summary>
/// The payload returned by the status endpoint.
/// </summary>
public class StatusResponse
{
    /// <summary>Gets or sets a value indicating whether the server needs a restart.</summary>
    public bool PendingRestart { get; set; }

    /// <summary>Gets or sets a value indicating whether the notice was dismissed.</summary>
    public bool Dismissed { get; set; }

    /// <summary>Gets or sets the recorded updates, newest first.</summary>
    public IReadOnlyList<PluginUpdateRecord> Updates { get; set; } = new List<PluginUpdateRecord>();
}
