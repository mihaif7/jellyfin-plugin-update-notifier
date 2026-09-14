namespace Jellyfin.Plugin.UpdateNotifier.Models;

/// <summary>
/// The payload returned by the summary endpoint.
/// </summary>
/// <remarks>
/// The badge only needs to know whether to show a dot and what number to put
/// beside it, so this carries no changelog text.
/// </remarks>
public class SummaryResponse
{
    /// <summary>Gets or sets a value indicating whether the server needs a restart.</summary>
    public bool PendingRestart { get; set; }

    /// <summary>Gets or sets a value indicating whether the notice was dismissed.</summary>
    public bool Dismissed { get; set; }

    /// <summary>Gets or sets the number of recorded updates.</summary>
    public int Count { get; set; }
}
