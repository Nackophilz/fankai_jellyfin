using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.Fankai.Model;

// Pour l'endpoint GET /series
public class PaginatedSeriesResponse
{
    [JsonPropertyName("series")]
    public List<FankaiSerie>? Series { get; set; }

    [JsonPropertyName("pagination")]
    public PaginationInfo? Pagination { get; set; }
}

public class PaginationInfo
{
    [JsonPropertyName("current_page")]
    public int CurrentPage { get; set; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; set; }

    [JsonPropertyName("total_items")]
    public int TotalItems { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
}

// Pour l'endpoint GET /series/{serie_id}/seasons
public class SerieSeasonsResponse
{
    [JsonPropertyName("serie_title")]
    public string? SerieTitle { get; set; }

    [JsonPropertyName("seasons_count")]
    public int SeasonsCount { get; set; }

    [JsonPropertyName("seasons")]
    public List<FankaiSeason>? Seasons { get; set; }
}

// Pour l'endpoint GET /seasons/{season_id}/episodes
public class SeasonEpisodesResponse
{
    [JsonPropertyName("serie_title")]
    public string? SerieTitle { get; set; }

    [JsonPropertyName("season_number")]
    public int? SeasonNumber { get; set; }

    [JsonPropertyName("episodes_count")]
    public int EpisodesCount { get; set; }

    [JsonPropertyName("episodes")]
    public List<FankaiEpisode>? Episodes { get; set; }
}

// Pour l'endpoint GET /series/{serie_id}/actors
public class SerieActorsResponse
{
    [JsonPropertyName("actors_count")]
    public int ActorsCount { get; set; }

    [JsonPropertyName("actors")]
    public List<FankaiActor>? Actors { get; set; }
}

// Modèle générique pour les erreurs API
public class ApiError
{
    [JsonPropertyName("error")]
    public string? Message { get; set; }

    [JsonPropertyName("details")]
    public string? Details { get; set; }
}
