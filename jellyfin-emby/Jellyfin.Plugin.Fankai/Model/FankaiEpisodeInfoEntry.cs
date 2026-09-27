using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fankai.Model;

/// <summary>
/// Représente une entrée du point de terminaison /episodes/infos
/// </summary>
public class FankaiEpisodeInfoEntry
{
    [JsonPropertyName("formatted_name")]
    public string? FormattedName { get; set; }

    [JsonPropertyName("nfo_path")]
    public string? NfoPath { get; set; }
}
