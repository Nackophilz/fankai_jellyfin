using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO; 
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions; 
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fankai.Api;
using Jellyfin.Plugin.Fankai.Model; 
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers; 
using MediaBrowser.Model.Providers; 
using MediaBrowser.Model.Entities;  
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fankai.Providers;

public class EpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
{
    public string Name => "Fankai Episode Provider";

    private readonly ILogger<EpisodeProvider> _logger;
    private readonly FankaiApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory; 

    public const string ProviderIdName = "FankaiEpisodeId";

    public EpisodeProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<EpisodeProvider> logger,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiClient = new FankaiApiClient(httpClientFactory, loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)));
    }
    
    private string NormalizeFilenameForComparison(string? filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return string.Empty;

        string normalized = filename.ToLowerInvariant();
        
        // 1. Supprimer le contenu entre crochets et parenthèses
        normalized = Regex.Replace(normalized, @"(\[.*?\]|\(.*?\))", " "); 

        // 2. Supprimer les tags de qualité, de source, d'audio et de codec
        normalized = Regex.Replace(normalized, @"\b(1080p|720p|480p|multi|x264|x265|h264|h265|hevc|bdrip|dvdrip|webrip|webdl|vostfr|vf|truefrench|aac|dts|ac3|opus|flac|complete|uncut|bluray|hddvd|remux|hdr|sdr)\b", " ", RegexOptions.IgnoreCase);

        // 3. Remplacer espace, point, soulignement, trait d'union, apostrophe par un seul espace
        normalized = Regex.Replace(normalized, @"[\s\.\-_']+", " ");

        // 5. Consolider les espaces multiples en un seul et supprimer les espaces en début et fin
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        _logger.LogTrace("Normalisation de '{OriginalFilename}' à '{NormalizedFilename}'", filename, normalized);
        return normalized;
    }

    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Fankai GetMetadata pour Episode: Nom='{EpisodeName}', Chemin='{Path}', JellyfinSeasonNum={SeasonNum}, JellyfinEpisodeNum={EpisodeNum}", 
            info.Name, 
            info.Path,
            info.ParentIndexNumber, 
            info.IndexNumber);
            
        var result = new MetadataResult<Episode>();
        
        if (string.IsNullOrWhiteSpace(info.Path))
        {
            _logger.LogWarning("EpisodeInfo.Path est null ou vide. Impossible de faire correspondre l'épisode.");
            return result;
        }

        string? fankaiSeriesId = null;
        if (info.SeriesProviderIds.TryGetValue(SeriesProvider.ProviderIdName, out var seriesIdFromEpisode))
        {
            fankaiSeriesId = seriesIdFromEpisode;
        }
        else if (info.ProviderIds.TryGetValue(SeriesProvider.ProviderIdName, out var seriesIdFromSelf))
        {
             fankaiSeriesId = seriesIdFromSelf;
        }

        if (string.IsNullOrWhiteSpace(fankaiSeriesId))
        {
             _logger.LogWarning("Fankai Series ID n'est pas disponible dans EpisodeInfo pour Path: {Path}. Impossible d'identifier la série parente.", info.Path);
            return result;
        }

        _logger.LogDebug("Tentative de correspondance de l'épisode pour Fankai Series ID '{FankaiSeriesId}' en utilisant le chemin de fichier '{FilePath}'", fankaiSeriesId, info.Path);

        var seasonsResponse = await _apiClient.GetSeasonsForSerieAsync(fankaiSeriesId, cancellationToken).ConfigureAwait(false);
        if (seasonsResponse?.Seasons == null || !seasonsResponse.Seasons.Any())
        {
            _logger.LogWarning("Aucune saison trouvée dans l'API Fankai pour l'ID de série '{FankaiSeriesId}'.", fankaiSeriesId);
            return result;
        }

        FankaiEpisode? matchedFankaiEpisode = null;
        FankaiSeason? parentFankaiSeason = null;
        string jellyfinMediaFilename = Path.GetFileName(info.Path); 
        string normalizedJellyfinFilename = NormalizeFilenameForComparison(Path.GetFileNameWithoutExtension(info.Path));
        _logger.LogDebug("Nom de fichier Jellyfin normalisé pour la correspondance : '{NormalizedJellyfinFilename}' (depuis : '{OriginalJellyfinFile}')", normalizedJellyfinFilename, jellyfinMediaFilename);

        foreach (var fankaiSeason in seasonsResponse.Seasons)
        {
            if (fankaiSeason.Id == 0) continue;

            if (info.ParentIndexNumber.HasValue && info.ParentIndexNumber.Value != fankaiSeason.SeasonNumber)
            {
                 _logger.LogTrace("La saison Jellyfin ({JellyfinSeasonNum}) diffère de la saison actuelle de l'API ({ApiSeasonNum}). Poursuite de la recherche, mais cette saison de l'API est moins susceptible de correspondre.", info.ParentIndexNumber.Value, fankaiSeason.SeasonNumber);
            }

            var episodesResponse = await _apiClient.GetEpisodesForSeasonAsync(fankaiSeason.Id.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
            if (episodesResponse?.Episodes != null)
            {
                foreach (var apiEpisode in episodesResponse.Episodes)
                {
                    bool isMatch = false;
                    string? matchedUsing = null;
                    
                    string normalizedApiOriginalFilename = NormalizeFilenameForComparison(Path.GetFileNameWithoutExtension(apiEpisode.OriginalFilename));
                    string normalizedApiNfoFilename = NormalizeFilenameForComparison(Path.GetFileNameWithoutExtension(apiEpisode.NfoFilename));

                    _logger.LogTrace("Comparaison de NormJellyfin='{NormJellyfin}' avec API S{ApiS}E{ApiE} (ID:{ApiEpId}): NormApiOrig='{NormApiOrig}', NormApiNfo='{NormApiNfo}'",
                        normalizedJellyfinFilename,
                        fankaiSeason.SeasonNumber, apiEpisode.EpisodeNumber, apiEpisode.Id,
                        normalizedApiOriginalFilename, normalizedApiNfoFilename);

                    if (!string.IsNullOrWhiteSpace(normalizedApiOriginalFilename) && normalizedApiOriginalFilename == normalizedJellyfinFilename)
                    {
                        isMatch = true;
                        matchedUsing = "OriginalFilename normalisé";
                    }

                    if (!isMatch && !string.IsNullOrWhiteSpace(normalizedApiNfoFilename) && normalizedApiNfoFilename == normalizedJellyfinFilename)
                    {
                        isMatch = true;
                        matchedUsing = "NfoFilename normalisé";
                    }

                    if (!isMatch &&
                        info.ParentIndexNumber.HasValue && info.ParentIndexNumber.Value == fankaiSeason.SeasonNumber &&
                        info.IndexNumber.HasValue && info.IndexNumber.Value == apiEpisode.EpisodeNumber && 
                        info.IndexNumber.Value < 200) 
                    {
                        isMatch = true;
                        matchedUsing = "Saison/Numéro d'épisode (Match API x Jellyfin)";
                    }

                    if (isMatch)
                    {
                        matchedFankaiEpisode = apiEpisode;
                        parentFankaiSeason = fankaiSeason; 
                        _logger.LogInformation("CORRESPONDANCE TROUVEE: Fichier Jellyfin '{JellyfinFile}' avec l'API Fankai en utilisant {MatchedUsingCriteria}. API Episode: S{ApiS}E{ApiE} (ID: {FankaiEpisodeId}). API OriginalFilename: '{ApiOrigFile}', API NfoFilename: '{ApiNfoFile}'", 
                            jellyfinMediaFilename, matchedUsing,
                            parentFankaiSeason.SeasonNumber, matchedFankaiEpisode.EpisodeNumber, matchedFankaiEpisode.Id,
                            apiEpisode.OriginalFilename, apiEpisode.NfoFilename);
                        break; 
                    }
                }
            }
            if (matchedFankaiEpisode != null) break; 
        }

        if (matchedFankaiEpisode == null || parentFankaiSeason == null)
        {
             _logger.LogWarning("AUCUNE CORRESPONDANCE: Impossible de trouver un épisode Fankai pour le fichier Jellyfin '{JellyfinFile}' (Normalisé : '{NormalizedJellyfinFile}') dans l'ID de série '{FankaiSeriesId}'. {NumSeasons} saisons ont été recherchées dans l'API.", 
                jellyfinMediaFilename, normalizedJellyfinFilename, fankaiSeriesId, seasonsResponse.Seasons.Count);
            return result;
        }

        _logger.LogDebug("ID de l'épisode Fankai final correspondant : {FankaiEpisodeId} (API S{ApiSeasonNum}E{ApiEpisodeNum}) pour le fichier {JellyfinFile}", 
            matchedFankaiEpisode.Id, parentFankaiSeason.SeasonNumber, matchedFankaiEpisode.EpisodeNumber, jellyfinMediaFilename);

        result.Item = new Episode
        {
            Name = matchedFankaiEpisode.Title,
            Overview = matchedFankaiEpisode.Plot,
            PremiereDate = TryParseDate(matchedFankaiEpisode.Aired), 
            IndexNumber = matchedFankaiEpisode.EpisodeNumber, 
            ParentIndexNumber = parentFankaiSeason.SeasonNumber, 
            OfficialRating = matchedFankaiEpisode.Mpaa,
            ProductionYear = info.Year 
        };
        
        result.Item.ProviderIds.TryAdd(ProviderIdName, matchedFankaiEpisode.Id.ToString(CultureInfo.InvariantCulture));

        if (!string.IsNullOrWhiteSpace(matchedFankaiEpisode.Studio))
        {
            result.Item.Studios = new[] { matchedFankaiEpisode.Studio };
        }
        
        result.HasMetadata = true;
        _logger.LogInformation("Récupération OK des métadonnées pour l'épisode Fankai ID: {FankaiEpisodeId}, Titre: {Title}", matchedFankaiEpisode.Id, matchedFankaiEpisode.Title);
        return result;
    }

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fankai GetSearchResults pour l'épisode: '{EpisodeName}' - Pas encore implémenté, retour d'une liste vide.", searchInfo.Name);
        return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
    }
    
    private DateTime? TryParseDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString)) return null;
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            return date.ToUniversalTime();
        }
        _logger.LogWarning("Impossible de parser la chaîne de date : {DateString}", dateString);
        return null;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("FankaiEpisodeImageClient");
        return client.GetAsync(new Uri(url), cancellationToken);
    }
}
