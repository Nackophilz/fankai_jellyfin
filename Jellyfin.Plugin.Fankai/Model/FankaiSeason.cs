using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fankai.Model;

/// <summary>
/// Représente les données d'une saison provenant de l'API Fankai.
/// </summary>
public class FankaiSeason
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("serie_id")]
    public int SerieId { get; set; }

    [JsonPropertyName("season_number")]
    public int? SeasonNumber { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("sort_title")]
    public string? SortTitle { get; set; }

    [JsonPropertyName("plot")]
    public string? Plot { get; set; }

    [JsonPropertyName("premiered")]
    public string? Premiered { get; set; } // Date (format yyyy-MM-dd)

    [JsonPropertyName("poster_image")]
    public string? PosterImageUrl { get; set; }

    [JsonPropertyName("fanart_image")]
    public string? FanartImageUrl { get; set; }


    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tmdb_id")]
    public string? TmdbId { get; set; }

    [JsonPropertyName("tvdb_id")]
    public string? TvdbId { get; set; }

    // Champs additionnels de l'endpoint /series/{serie_id}/seasons
    [JsonPropertyName("serie_title")]
    public string? SerieTitle { get; set; }

    [JsonPropertyName("statistics")]
    public SeasonStatistics? Statistics { get; set; }

    [JsonPropertyName("links")]
    public SeasonLinks? Links { get; set; }
}

public class SeasonStatistics
{
    [JsonPropertyName("episode_count")]
    public int? EpisodeCount { get; set; }

    [JsonPropertyName("first_aired")]
    public string? FirstAired { get; set; }

    [JsonPropertyName("last_aired")]
    public string? LastAired { get; set; }

    [JsonPropertyName("episodes_with_thumbnails")]
    public int? EpisodesWithThumbnails { get; set; }
}

public class SeasonLinks
{
    [JsonPropertyName("episodes")]
    public string? EpisodesApiUrl { get; set; } // ex: "/seasons/1/episodes"

    [JsonPropertyName("serie")]
    public string? SerieApiUrl { get; set; } // ex: "/series/1"

    [JsonPropertyName("images")]
    public SeasonImageLinks? Images { get; set; }
}

public class SeasonImageLinks
{
    [JsonPropertyName("poster")]
    public string? PosterApiUrl { get; set; } // ex: "/seasons/1/image/poster"

    [JsonPropertyName("fanart")]
    public string? FanartApiUrl { get; set; }
}
