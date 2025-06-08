using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace Jellyfin.Plugin.Fankai.Model;

/// <summary>
/// Représente les données d'une série provenant de l'API Fankai.
/// </summary>
public class FankaiSerie
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    // Ajout de la propriété SortTitle
    [JsonPropertyName("sort_title")]
    public string? SortTitle { get; set; }

    [JsonPropertyName("original_title")]
    public string? OriginalTitle { get; set; }

    [JsonPropertyName("show_title")]
    public string? ShowTitle { get; set; }

    [JsonPropertyName("title_for_plex")]
    public string? TitleForPlex { get; set; }

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("plot")]
    public string? Plot { get; set; }

    [JsonPropertyName("rating_value")]
    public float? RatingValue { get; set; }

    [JsonPropertyName("rating_votes")]
    public int? RatingVotes { get; set; }

    [JsonPropertyName("rating_name")]
    public string? RatingName { get; set; }

    [JsonPropertyName("mpaa")]
    public string? Mpaa { get; set; } // Classification

    [JsonPropertyName("premiered")]
    public string? Premiered { get; set; } // Date de première diffusion (format yyyy-MM-dd)

    [JsonPropertyName("studio")]
    public string? Studio { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("genres")]
    public string? Genres { get; set; } // Chaîne de genres séparés par des virgules

    [JsonPropertyName("banner_image")]
    public string? BannerImageUrl { get; set; }

    [JsonPropertyName("fanart_image")]
    public string? FanartImageUrl { get; set; }

    [JsonPropertyName("logo_image")]
    public string? LogoImageUrl { get; set; }

    [JsonPropertyName("poster_image")]
    public string? PosterImageUrl { get; set; }

    [JsonPropertyName("theme_music")]
    public string? ThemeMusicUrl { get; set; }
    
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("tagline")]
    public string? Tagline { get; set; }

    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tmdb_id")]
    public string? TmdbId { get; set; }

    [JsonPropertyName("tvdb_id")]
    public string? TvdbId { get; set; }

    [JsonPropertyName("statistics")]
    public SerieStatistics? Statistics { get; set; }

    [JsonPropertyName("links")]
    public SerieLinks? Links { get; set; }
}

public class SerieStatistics
{
    [JsonPropertyName("seasons_count")]
    public int? SeasonsCount { get; set; }

    [JsonPropertyName("episodes_count")]
    public int? EpisodesCount { get; set; }

    [JsonPropertyName("first_aired")]
    public string? FirstAired { get; set; }

    [JsonPropertyName("last_aired")]
    public string? LastAired { get; set; }
}

public class SerieLinks
{
    [JsonPropertyName("seasons")]
    public string? SeasonsApiUrl { get; set; }

    [JsonPropertyName("actors")]
    public string? ActorsApiUrl { get; set; }

    [JsonPropertyName("images")]
    public SerieImageLinks? Images { get; set; }
}

public class SerieImageLinks
{
    [JsonPropertyName("banner")]
    public string? BannerApiUrl { get; set; }

    [JsonPropertyName("fanart")]
    public string? FanartApiUrl { get; set; }

    [JsonPropertyName("poster")]
    public string? PosterApiUrl { get; set; }

    [JsonPropertyName("logo")]
    public string? LogoApiUrl { get; set; }
}
