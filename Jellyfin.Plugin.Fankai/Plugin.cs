using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Fankai.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fankai;

/// <summary>
/// Plugin principal Fankai.
/// </summary>
#if __EMBY__
public class Plugin : BasePlugin, IHasPluginConfiguration
#else
public class Plugin : BasePlugin<PluginConfiguration>
#endif
{
    /// <summary>
    /// Obtient l'instance actuelle du plugin.
    /// </summary>
    public static Plugin? Instance { get; private set; }

#if __EMBY__
    // Emby logger interface
    private readonly MediaBrowser.Model.Logging.ILogger _logger;
#else
    private readonly ILogger<Plugin> _logger;
#endif

    /// <summary>
    /// Initialise une nouvelle instance de la classe <see cref="Plugin"/>.
    /// </summary>
    /// <param name="applicationPaths">Instance de l'interface <see cref="IApplicationPaths"/>.</param>
    /// <param name="xmlSerializer">Instance de l'interface <see cref="IXmlSerializer"/>.</param>
    /// <param name="logger">Instance de l'interface <see cref="ILogger"/>.</param>
#if __EMBY__
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        MediaBrowser.Model.Logging.ILogManager logManager)
        : base()
    {
        Instance = this;
        _logger = logManager.GetLogger(Name);

        Configuration = new PluginConfiguration();
        try
        {
            var configPath = System.IO.Path.Combine(applicationPaths.PluginConfigurationsPath, "Jellyfin.Plugin.Fankai.xml");
            if (System.IO.File.Exists(configPath))
            {
                Configuration = (PluginConfiguration)xmlSerializer.DeserializeFromFile(typeof(PluginConfiguration), configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.ErrorException("Failed to load configuration", ex);
        }
        
        _logger.Info("Jellyfin.Plugin.Fankai initialisé.");
    }
#else
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _logger = logger;
        _logger.LogInformation("Jellyfin.Plugin.Fankai initialisé.");
    }
#endif

#if __EMBY__
    /// <summary>
    /// Gets the plugin configuration.
    /// </summary>
    public PluginConfiguration Configuration { get; private set; }

    /// <inheritdoc />
    BasePluginConfiguration IHasPluginConfiguration.Configuration => Configuration;

    /// <inheritdoc />
    public Type ConfigurationType => typeof(PluginConfiguration);

    /// <inheritdoc />
    public void SetStartupInfo(Action<string> startupInfo)
    {
    }

    /// <inheritdoc />
    public void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        Configuration = (PluginConfiguration)configuration;
    }
#endif

    /// <inheritdoc />
    public override string Name => "Fankai";

    /// <inheritdoc />
    // GUID
    public override Guid Id => Guid.Parse("4b725b9a-9063-4cb7-9533-ceaf32c6c86d");

    /// <inheritdoc />
    public override string Description => "Fournit des métadonnées et des images depuis une API Fankai personnalisée.";

}
