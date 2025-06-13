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
public class Plugin : BasePlugin<PluginConfiguration>
{
    /// <summary>
    /// Obtient l'instance actuelle du plugin.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    private readonly ILogger<Plugin> _logger;

    /// <summary>
    /// Initialise une nouvelle instance de la classe <see cref="Plugin"/>.
    /// </summary>
    /// <param name="applicationPaths">Instance de l'interface <see cref="IApplicationPaths"/>.</param>
    /// <param name="xmlSerializer">Instance de l'interface <see cref="IXmlSerializer"/>.</param>
    /// <param name="logger">Instance de l'interface <see cref="ILogger"/>.</param>
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

    /// <inheritdoc />
    public override string Name => "Fankai";

    /// <inheritdoc />
    // GUID que vous avez fourni
    public override Guid Id => Guid.Parse("4b725b9a-9063-4cb7-9533-ceaf32c6c86d");

    /// <inheritdoc />
    public override string Description => "Fournit des métadonnées et des images depuis une API Fankai personnalisée.";

}
