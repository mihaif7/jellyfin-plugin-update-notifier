using System.Net.Mime;
using Jellyfin.Plugin.UpdateNotifier.Models;
using Jellyfin.Plugin.UpdateNotifier.Services;
using MediaBrowser.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.UpdateNotifier.Controllers;

/// <summary>
/// API endpoints for the plugin update notifier.
/// </summary>
/// <remarks>
/// Elevation is required at the controller level so an endpoint added later
/// cannot accidentally be served anonymously.
/// </remarks>
[ApiController]
[Route("PluginUpdateNotifier")]
[Authorize(Policy = "RequiresElevation")]
[Produces(MediaTypeNames.Application.Json)]
public class UpdateNotifierController : ControllerBase
{
    private readonly UpdateTracker _tracker;
    private readonly IApplicationHost _applicationHost;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNotifierController"/> class.
    /// </summary>
    /// <param name="tracker">The update tracker.</param>
    /// <param name="applicationHost">The application host.</param>
    public UpdateNotifierController(UpdateTracker tracker, IApplicationHost applicationHost)
    {
        _tracker = tracker;
        _applicationHost = applicationHost;
    }

    /// <summary>
    /// Gets the pending-restart status and the recorded plugin updates.
    /// </summary>
    /// <response code="200">Status returned.</response>
    /// <returns>The status payload.</returns>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<StatusResponse> GetStatus()
    {
        return new StatusResponse
        {
            // Maintained by the server: InstallationManager calls
            // NotifyPendingRestart() after every successful install.
            PendingRestart = _applicationHost.HasPendingRestart,
            Dismissed = _tracker.IsDismissed(),
            Updates = _tracker.GetUpdates(),
        };
    }

    /// <summary>
    /// Dismisses the current notification for every admin session.
    /// </summary>
    /// <response code="204">Notification dismissed.</response>
    /// <returns>No content.</returns>
    [HttpPost("dismiss")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult Dismiss()
    {
        _tracker.Dismiss();
        return NoContent();
    }
}
