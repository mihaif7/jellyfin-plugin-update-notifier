using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.UpdateNotifier.Services;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Updates;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.UpdateNotifier.EventConsumers;

/// <summary>
/// Consumes plugin installation lifecycle events.
/// </summary>
public class PluginUpdateEventConsumer :
    IEventConsumer<PluginInstallingEventArgs>,
    IEventConsumer<PluginUpdatedEventArgs>,
    IEventConsumer<PluginInstalledEventArgs>,
    IEventConsumer<PluginUninstalledEventArgs>
{
    private readonly UpdateTracker _tracker;
    private readonly IPluginManager _pluginManager;
    private readonly ILogger<PluginUpdateEventConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginUpdateEventConsumer"/> class.
    /// </summary>
    /// <param name="tracker">The update tracker.</param>
    /// <param name="pluginManager">The plugin manager.</param>
    /// <param name="logger">The logger.</param>
    public PluginUpdateEventConsumer(
        UpdateTracker tracker,
        IPluginManager pluginManager,
        ILogger<PluginUpdateEventConsumer> logger)
    {
        _tracker = tracker;
        _pluginManager = pluginManager;
        _logger = logger;
    }

    /// <summary>
    /// Captures the installed version before the install overwrites it.
    /// </summary>
    /// <param name="eventArgs">The event arguments.</param>
    /// <returns>A task.</returns>
    public Task OnEvent(PluginInstallingEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var info = eventArgs.Argument;
        string? current = null;
        foreach (var p in _pluginManager.Plugins)
        {
            if (p.Id.Equals(info.Id))
            {
                current = p.Version.ToString();
                break;
            }
        }

        _tracker.RecordPreInstall(info.Id, current);
        _logger.LogDebug("Pre-install version for {Name}: {Version}", info.Name, current);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnEvent(PluginUpdatedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var info = eventArgs.Argument;
        _tracker.RecordUpdate(info.Id, info.Name, info.Version.ToString(), info.Changelog, info.SourceUrl);
        _logger.LogInformation("Recorded plugin change: {Name} {Version}", info.Name, info.Version);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnEvent(PluginInstalledEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var info = eventArgs.Argument;
        _tracker.RecordUpdate(info.Id, info.Name, info.Version.ToString(), info.Changelog, info.SourceUrl);
        _logger.LogInformation("Recorded plugin change: {Name} {Version}", info.Name, info.Version);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records a removal, which leaves a restart pending just as an install does.
    /// </summary>
    /// <param name="eventArgs">The event arguments.</param>
    /// <returns>A task.</returns>
    public Task OnEvent(PluginUninstalledEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        var info = eventArgs.Argument;
        _tracker.RecordUninstall(info.Id, info.Name, info.Version.ToString());
        _logger.LogInformation("Recorded plugin removal: {Name} {Version}", info.Name, info.Version);
        return Task.CompletedTask;
    }
}
