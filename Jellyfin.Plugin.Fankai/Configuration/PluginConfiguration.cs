using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Fankai.Configuration;

/// <summary>
/// Classe de configuration du plugin Fankai.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// </summary>

    /// <summary>
    /// Obtient ou définit la clé API pour l'API Fankai (si je pense a l'implémenter un jour, on sais jamais si il y en aura besoin (floooooooddd powa)).
    /// </summary>
    public string FankaiApiKey { get; set; }

    /// <summary>
    /// Initialise une nouvelle instance de la classe <see cref="PluginConfiguration"/>.
    /// </summary>
    public PluginConfiguration()
    {
        FankaiApiKey = string.Empty;
    }
}
