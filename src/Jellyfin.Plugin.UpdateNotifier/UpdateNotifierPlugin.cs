using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.UpdateNotifier.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.UpdateNotifier;

/// <summary>
/// The main plugin class for the update notifier.
/// </summary>
public class UpdateNotifierPlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateNotifierPlugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public UpdateNotifierPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static UpdateNotifierPlugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "Plugin Update Notifier";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("b6a1c3f2-5d84-4e77-9d2a-7f1c8e4b3a91");

    /// <inheritdoc />
    public override string Description =>
        "Badges the admin avatar and adds a profile-menu entry when plugins are installed, "
        + "updated or removed and the server needs a restart, with a dashboard page listing "
        + "what changed, each release's changelog, and a history of what earlier restarts applied. "
        + "Admins can also show the badge to chosen non-admin users. "
        + "Source, docs and issues: https://github.com/mihaif7/jellyfin-plugin-update-notifier";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            // Name is what lands in #/configurationpage?name=...
            Name = "UpdateNotifier",
            DisplayName = "Plugin Updates",
            // Without this the page is not listed in the dashboard sidebar:
            // PluginDrawerSection only queries pages with enableInMainMenu=true.
            EnableInMainMenu = true,
            MenuIcon = "system_update_alt",
            EmbeddedResourcePath = string.Format(
                CultureInfo.InvariantCulture,
                "{0}.Configuration.configPage.html",
                GetType().Namespace),
        };
    }
}
