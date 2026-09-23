using System;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;
using Jellyfin.Data;
using Jellyfin.Database.Implementations.Enums;
using Jellyfin.Plugin.UpdateNotifier.Models;
using Jellyfin.Plugin.UpdateNotifier.Services;
using MediaBrowser.Common;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.UpdateNotifier.Controllers;

/// <summary>
/// The badge endpoint, open to administrators and to the users they chose to notify.
/// </summary>
/// <remarks>
/// Unlike <see cref="UpdateNotifierController"/>, this controller admits any
/// signed-in user, so every action here must call <see cref="IsNotifiedAsync"/>
/// and must return nothing beyond counts.
/// </remarks>
[ApiController]
[Route("PluginUpdateNotifier")]
[Authorize]
[Produces(MediaTypeNames.Application.Json)]
public class NoticeController : ControllerBase
{
    private readonly UpdateTracker _tracker;
    private readonly IApplicationHost _applicationHost;
    private readonly IAuthorizationContext _authContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoticeController"/> class.
    /// </summary>
    /// <param name="tracker">The update tracker.</param>
    /// <param name="applicationHost">The application host.</param>
    /// <param name="authContext">The authorization context.</param>
    public NoticeController(UpdateTracker tracker, IApplicationHost applicationHost, IAuthorizationContext authContext)
    {
        _tracker = tracker;
        _applicationHost = applicationHost;
        _authContext = authContext;
    }

    /// <summary>
    /// Gets just the counts the avatar badge needs.
    /// </summary>
    /// <remarks>
    /// The badge is polled; the status endpoint carries every changelog in
    /// full, which the badge would only discard.
    /// </remarks>
    /// <response code="200">Summary returned.</response>
    /// <response code="403">The caller is neither an administrator nor a notified user.</response>
    /// <returns>The summary payload.</returns>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SummaryResponse>> GetSummary()
    {
        if (!await IsNotifiedAsync().ConfigureAwait(false))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return new SummaryResponse
        {
            PendingRestart = _applicationHost.HasPendingRestart,
            Dismissed = _tracker.IsDismissed(),
            Count = _tracker.GetUpdateCount(),
        };
    }

    private async Task<bool> IsNotifiedAsync()
    {
        var auth = await _authContext.GetAuthorizationInfo(HttpContext).ConfigureAwait(false);

        // An API key is elevated, as it is on the admin controller.
        if (auth.IsApiKey)
        {
            return true;
        }

        var user = auth.User;
        if (user is null)
        {
            return false;
        }

        if (user.HasPermission(PermissionKind.IsAdministrator))
        {
            return true;
        }

        var ids = UpdateNotifierPlugin.Instance?.Configuration.NotifyUserIds ?? Array.Empty<Guid>();
        return ids.Contains(user.Id);
    }
}
