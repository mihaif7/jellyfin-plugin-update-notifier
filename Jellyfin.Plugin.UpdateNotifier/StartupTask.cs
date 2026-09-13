using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.UpdateNotifier;

/// <summary>
/// Runs at server startup to register the dashboard page and the client script.
/// </summary>
/// <remarks>
/// Jellyfin 12 removed <c>IServerEntryPoint</c>; a scheduled task with a startup
/// trigger is the supported way to run code once the server is up.
/// </remarks>
public class StartupTask : IScheduledTask
{
    private readonly UpdateTracker _tracker;
    private readonly ILogger<StartupTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupTask"/> class.
    /// </summary>
    /// <param name="tracker">The update tracker.</param>
    /// <param name="logger">The logger.</param>
    public StartupTask(UpdateTracker tracker, ILogger<StartupTask> logger)
    {
        _tracker = tracker;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Plugin Update Notifier Startup";

    /// <inheritdoc />
    public string Key => "Jellyfin.Plugin.UpdateNotifier.Startup";

    /// <inheritdoc />
    public string Description => "Registers the update notifier page and client script.";

    /// <inheritdoc />
    public string Category => "Startup Services";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger };
    }

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        // Whatever was recorded before this restart is now live, so the notice
        // clears on the first startup after it.
        _tracker.Clear();

        RegisterClientScript();

        progress?.Report(100);
        return Task.CompletedTask;
    }

    private static Assembly? FindAssembly(string fragment)
        => AssemblyLoadContext.All
            .SelectMany(x => x.Assemblies)
            .FirstOrDefault(x => x.FullName?.Contains(fragment, StringComparison.OrdinalIgnoreCase) ?? false);

    private void RegisterClientScript()
    {
        try
        {
            var assembly = FindAssembly("Jellyfin.Plugin.JavaScriptInjector");
            var interfaceType = assembly?.GetType("Jellyfin.Plugin.JavaScriptInjector.PluginInterface");
            if (interfaceType is null)
            {
                _logger.LogWarning("JavaScript Injector not found; the avatar badge will not be shown.");
                return;
            }

            var plugin = UpdateNotifierPlugin.Instance;
            if (plugin is null)
            {
                return;
            }

            var script = EmbeddedResource.Read("Web.inject.js");
            if (script is null)
            {
                _logger.LogError("Embedded client script could not be read.");
                return;
            }

            var payload = new JObject
            {
                { "id", $"{plugin.Id}-badge" },
                { "name", "Plugin Update Notifier Badge" },
                { "script", script },
                { "enabled", true },
                { "requiresAuthentication", true },
                { "pluginId", plugin.Id.ToString() },
                { "pluginName", plugin.Name },
                { "pluginVersion", plugin.Version.ToString() },
            };

            var result = interfaceType.GetMethod("RegisterScript")?.Invoke(null, new object[] { payload });
            if (result is bool ok && ok)
            {
                _logger.LogInformation("Registered the badge script with JavaScript Injector.");
            }
            else
            {
                _logger.LogWarning("JavaScript Injector rejected the badge script registration.");
            }
        }
        catch (Exception ex) when (ex is TargetInvocationException or MissingMethodException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to register the script with JavaScript Injector.");
        }
    }
}
