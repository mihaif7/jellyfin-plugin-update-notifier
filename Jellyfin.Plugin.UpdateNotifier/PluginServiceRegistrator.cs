using MediaBrowser.Controller;
using MediaBrowser.Controller.Events;
using MediaBrowser.Controller.Events.Updates;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.UpdateNotifier;

/// <summary>
/// Registers the plugin's services with the host.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<UpdateTracker>();
        serviceCollection.AddSingleton<IEventConsumer<PluginInstallingEventArgs>, PluginUpdateEventConsumer>();
        serviceCollection.AddSingleton<IEventConsumer<PluginUpdatedEventArgs>, PluginUpdateEventConsumer>();
        serviceCollection.AddSingleton<IEventConsumer<PluginInstalledEventArgs>, PluginUpdateEventConsumer>();
        serviceCollection.AddSingleton<IScheduledTask, StartupTask>();
    }
}
