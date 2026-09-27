using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fankai.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
#if __EMBY__
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
#endif
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fankai.Providers;

public class FankaiImageProvider : IRemoteImageProvider
{
    public string Name => "Fankai Image Provider";

#if __EMBY__
    private readonly MediaBrowser.Model.Logging.ILogger _logger;
    private readonly IHttpClient _httpClient;
#else
    private readonly ILogger<FankaiImageProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
#endif
    private readonly FankaiApiClient _apiClient;

    public const string FankaiSeasonIdProviderKey = "FankaiSeasonId"; 

#if __EMBY__
    public FankaiImageProvider(IHttpClient httpClient, ILogManager logManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logManager.GetLogger(GetType().Name);
        _apiClient = new FankaiApiClient(httpClient, _logger);
    }
#else
    public FankaiImageProvider(IHttpClientFactory httpClientFactory, ILogger<FankaiImageProvider> logger, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiClient = new FankaiApiClient(httpClientFactory, loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)));
    }
#endif

    private void LogInfo(string message, params object?[] args)
    {
#if __EMBY__
        _logger.Info(message, args);
#else
        _logger.LogInformation(message, args);
#endif
    }

    private void LogWarn(string message, params object?[] args)
    {
#if __EMBY__
        _logger.Warn(message, args);
#else
        _logger.LogWarning(message, args);
#endif
    }

    private void LogDebug(string message, params object?[] args)
    {
#if __EMBY__
        _logger.Debug(message, args);
#else
        _logger.LogDebug(message, args);
#endif
    }

    // Explicit implementation to avoid ambiguity
    public bool Supports(MediaBrowser.Controller.Entities.BaseItem item)
    {
        return item is Series || item is Season || item is Episode;
    }

    public IEnumerable<MediaBrowser.Model.Entities.ImageType> GetSupportedImages(MediaBrowser.Controller.Entities.BaseItem item)
    {
        if (item is Series)
        {
            return new List<MediaBrowser.Model.Entities.ImageType>
            {
                MediaBrowser.Model.Entities.ImageType.Primary,
                MediaBrowser.Model.Entities.ImageType.Backdrop,
                MediaBrowser.Model.Entities.ImageType.Banner,
                MediaBrowser.Model.Entities.ImageType.Logo,
                MediaBrowser.Model.Entities.ImageType.Thumb
            };
        }
        if (item is Season)
        {
            return new List<MediaBrowser.Model.Entities.ImageType>
            {
                MediaBrowser.Model.Entities.ImageType.Primary,
                MediaBrowser.Model.Entities.ImageType.Backdrop
            };
        }
        if (item is Episode)
        {
            return new List<MediaBrowser.Model.Entities.ImageType>
            {
                MediaBrowser.Model.Entities.ImageType.Primary
            };
        }
        return Enumerable.Empty<MediaBrowser.Model.Entities.ImageType>();
    }

#if __EMBY__
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(MediaBrowser.Controller.Entities.BaseItem item, MediaBrowser.Model.Configuration.LibraryOptions options, CancellationToken cancellationToken)
#else
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(MediaBrowser.Controller.Entities.BaseItem item, CancellationToken cancellationToken)
#endif
    {
        var images = new List<RemoteImageInfo>();
        string? fankaiSpecificId = null;

        if (item is Series seriesItem && seriesItem.ProviderIds.TryGetValue(SeriesProvider.ProviderIdName, out var seriesProviderIdVal))
        {
            fankaiSpecificId = seriesProviderIdVal;
        }
        else if (item is Season seasonItem && seasonItem.ProviderIds.TryGetValue(FankaiSeasonIdProviderKey, out var seasonProviderIdVal))
        {
            fankaiSpecificId = seasonProviderIdVal;
        }
        else if (item is Episode episodeItem && episodeItem.ProviderIds.TryGetValue(EpisodeProvider.ProviderIdName, out var episodeProviderIdVal))
        {
            fankaiSpecificId = episodeProviderIdVal;
        }

        LogDebug("Tentative de récupération des images pour l'objet: {0} (ID: {1}), Type: {2}, ID Fankai direct (si disponible): {3}",
            item.Name, item.Id, item.GetType().Name, fankaiSpecificId);

        if (item is Series series)
        {
            var currentFankaiSeriesId = series.GetProviderId(SeriesProvider.ProviderIdName);
            if (!string.IsNullOrWhiteSpace(currentFankaiSeriesId))
            {
                var serieData = await _apiClient.GetSerieByIdAsync(currentFankaiSeriesId, cancellationToken).ConfigureAwait(false);
                if (serieData != null)
                {
                    AddImageIfUrlValid(images, serieData.Images?.PosterApiUrl ?? serieData.PosterImageUrl, MediaBrowser.Model.Entities.ImageType.Primary);
                    AddImageIfUrlValid(images, serieData.Images?.FanartApiUrl ?? serieData.FanartImageUrl, MediaBrowser.Model.Entities.ImageType.Backdrop);
                    AddImageIfUrlValid(images, serieData.Images?.BannerApiUrl ?? serieData.BannerImageUrl, MediaBrowser.Model.Entities.ImageType.Banner);
                    AddImageIfUrlValid(images, serieData.Images?.LogoApiUrl ?? serieData.LogoImageUrl, MediaBrowser.Model.Entities.ImageType.Logo);
                    AddImageIfUrlValid(images, serieData.Images?.PosterApiUrl ?? serieData.PosterImageUrl, MediaBrowser.Model.Entities.ImageType.Thumb);
                }
            }
        }
        else if (item is Season season)
        {
            // Chercher l'ID de la série parente dans son objet Series
            var parentSeriesFankaiId = season.Series?.GetProviderId(SeriesProvider.ProviderIdName);

            // Fallback : si la série parente n'a pas encore son ID persisté (premier scan),
            // tenter de le résoudre via le nom.
            if (string.IsNullOrWhiteSpace(parentSeriesFankaiId))
            {
                var seriesName = season.Series?.Name;
                if (!string.IsNullOrWhiteSpace(seriesName))
                {
                    LogInfo("FankaiImageProvider: ID série absent pour la saison '{0}'. Tentative de résolution via le nom de série '{1}'.", season.Name, seriesName);
                    parentSeriesFankaiId = await ResolveSeriesIdByNameAsync(seriesName, cancellationToken).ConfigureAwait(false);
                }
            }

            if (string.IsNullOrWhiteSpace(parentSeriesFankaiId))
            {
                LogWarn("Impossible de trouver l'ID Fankai de la série parente pour la saison {0} (ID: {1})", season.Name, season.Id);
                return images;
            }

            var seasonsResponse = await _apiClient.GetSeasonsForSerieAsync(parentSeriesFankaiId, cancellationToken).ConfigureAwait(false);
            Model.FankaiSeason? seasonData = null;
            if (!string.IsNullOrWhiteSpace(fankaiSpecificId))
            {
                 seasonData = seasonsResponse?.Seasons?.FirstOrDefault(s => s.Id.ToString(CultureInfo.InvariantCulture) == fankaiSpecificId);

                 if (seasonData != null && season.IndexNumber.HasValue && seasonData.SeasonNumber != season.IndexNumber.Value)
                 {
                     LogWarn("L'ID Fankai {0} stocké sur la saison {1} désigne la saison {2} ('{3}'). ID ignoré.",
                         fankaiSpecificId, season.IndexNumber, seasonData.SeasonNumber, seasonData.Title);
                     seasonData = null;
                 }
            }
            if (seasonData == null && season.IndexNumber.HasValue)
            {
                seasonData = seasonsResponse?.Seasons?.FirstOrDefault(s => s.SeasonNumber == season.IndexNumber.Value);
            }

            if (seasonData != null)
            {
                StoreProviderId(season, FankaiSeasonIdProviderKey, seasonData.Id.ToString(CultureInfo.InvariantCulture));
                AddImageIfUrlValid(images, seasonData.Images?.PosterApiUrl ?? seasonData.PosterImageUrl, MediaBrowser.Model.Entities.ImageType.Primary);
                AddImageIfUrlValid(images, seasonData.Images?.FanartApiUrl ?? seasonData.FanartImageUrl, MediaBrowser.Model.Entities.ImageType.Backdrop);
            }
            else
            {
                LogWarn("Impossible de trouver les données de saison correspondantes pour la Saison {0} dans l'ID de Série {1}", season.IndexNumber, parentSeriesFankaiId);
            }
        }
        else if (item is Episode episode)
        {
            var (episodeData, resolvedSeasonId) = await ResolveEpisodeAsync(episode, fankaiSpecificId, cancellationToken).ConfigureAwait(false);

            if (episodeData == null || resolvedSeasonId == null)
            {
                LogWarn("Impossible de trouver les données de l'épisode correspondant pour l'épisode {0} ('{1}') dans la série {2}",
                    episode.IndexNumber, episode.Name, episode.Series?.GetProviderId(SeriesProvider.ProviderIdName));
                return images;
            }

            StoreProviderId(episode, EpisodeProvider.ProviderIdName, episodeData.Id.ToString(CultureInfo.InvariantCulture));
            StoreProviderId(episode, FankaiSeasonIdProviderKey, resolvedSeasonId);

            // Back-fill du premier scan uniquement : c'est SeasonProvider qui corrige un ID de saison erroné.
            if (episode.Season != null && string.IsNullOrWhiteSpace(episode.Season.GetProviderId(FankaiSeasonIdProviderKey)))
            {
                StoreProviderId(episode.Season, FankaiSeasonIdProviderKey, resolvedSeasonId);
            }

            AddImageIfUrlValid(images, episodeData.Links?.ThumbnailApiUrl ?? episodeData.ThumbImageUrl, MediaBrowser.Model.Entities.ImageType.Primary);
        }

        LogInfo("Trouvé {0} images distantes pour l'objet {1}", images.Count, item.Name);
        return images;
    }

    private void AddImageIfUrlValid(List<RemoteImageInfo> images, string? imageUrl, MediaBrowser.Model.Entities.ImageType type)
    {
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            images.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Url = imageUrl,
                Type = type,
            });
        }
    }

    /// <summary>
    /// Pose un ID Fankai sur un objet, en écrasant une valeur différente déjà présente :
    /// sans cela un ID erroné ne serait jamais corrigé.
    /// </summary>
    private void StoreProviderId(MediaBrowser.Controller.Entities.BaseItem item, string key, string value)
    {
        if (string.Equals(item.GetProviderId(key), value, StringComparison.Ordinal)) return;

        item.SetProviderId(key, value);
        LogDebug("Stockage de {0}={1} sur '{2}'", key, value, item.Name);
    }

    /// <summary>
    /// Localise un épisode dans l'API, en essayant d'abord les saisons connues de l'objet puis, en dernier
    /// recours, toutes les saisons de la série. Les IDs de saison de l'API ne suivant pas l'ordre des saisons,
    /// un ID obsolète stocké sur la saison parente désigne une autre saison de la même série au lieu d'échouer.
    /// </summary>
    private async Task<(Model.FankaiEpisode? Episode, string? SeasonId)> ResolveEpisodeAsync(
        Episode episode,
        string? fankaiEpisodeId,
        CancellationToken cancellationToken)
    {
        var triedSeasonIds = new HashSet<string>(StringComparer.Ordinal);

        async Task<Model.FankaiEpisode?> SearchSeasonAsync(string? seasonId, bool allowNumberMatch)
        {
            if (string.IsNullOrWhiteSpace(seasonId) || !triedSeasonIds.Add(seasonId)) return null;

            var episodesResponse = await _apiClient.GetEpisodesForSeasonAsync(seasonId, cancellationToken).ConfigureAwait(false);
            if (episodesResponse?.Episodes == null) return null;

            if (!string.IsNullOrWhiteSpace(fankaiEpisodeId))
            {
                var byId = episodesResponse.Episodes.FirstOrDefault(e => e.Id.ToString(CultureInfo.InvariantCulture) == fankaiEpisodeId);
                if (byId != null) return byId;
            }

            if (!allowNumberMatch || !episode.IndexNumber.HasValue) return null;

            return episodesResponse.Episodes.FirstOrDefault(e => e.EpisodeNumber == episode.IndexNumber.Value)
                ?? episodesResponse.Episodes.FirstOrDefault(e => int.TryParse(e.DisplayEpisode, out int displayEpNum) && displayEpNum == episode.IndexNumber.Value);
        }

        // L'ID posé par EpisodeProvider sur l'épisode lui-même, puis celui de l'objet Saison parent.
        foreach (var seasonId in new[] { episode.GetProviderId(FankaiSeasonIdProviderKey), episode.Season?.GetProviderId(FankaiSeasonIdProviderKey) })
        {
            var found = await SearchSeasonAsync(seasonId, true).ConfigureAwait(false);
            if (found != null) return (found, seasonId);
        }

        var parentSeriesFankaiId = episode.Series?.GetProviderId(SeriesProvider.ProviderIdName);
        if (string.IsNullOrWhiteSpace(parentSeriesFankaiId)) return (null, null);

        var seasonsResponse = await _apiClient.GetSeasonsForSerieAsync(parentSeriesFankaiId, cancellationToken).ConfigureAwait(false);
        if (seasonsResponse?.Seasons == null) return (null, null);

        if (episode.ParentIndexNumber.HasValue)
        {
            var seasonIdByNumber = seasonsResponse.Seasons
                .FirstOrDefault(s => s.SeasonNumber == episode.ParentIndexNumber.Value)?
                .Id.ToString(CultureInfo.InvariantCulture);

            var found = await SearchSeasonAsync(seasonIdByNumber, true).ConfigureAwait(false);
            if (found != null) return (found, seasonIdByNumber);
        }

        // Balayage des saisons restantes sur le seul ID d'épisode : une série dont la numérotation repart à 1
        // à chaque saison donnerait une fausse correspondance dans n'importe quelle saison balayée.
        if (!string.IsNullOrWhiteSpace(fankaiEpisodeId))
        {
            foreach (var season in seasonsResponse.Seasons)
            {
                var seasonId = season.Id.ToString(CultureInfo.InvariantCulture);
                var found = await SearchSeasonAsync(seasonId, false).ConfigureAwait(false);
                if (found != null)
                {
                    LogWarn("L'épisode Fankai {0} ('{1}') n'était pas dans la saison attendue, retrouvé dans la saison {2} ('{3}').",
                        fankaiEpisodeId, episode.Name, seasonId, season.Title);
                    return (found, seasonId);
                }
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Tente de résoudre l'ID Fankai d'une série à partir de son nom.
    /// Fallback utilisé quand SeriesProvider n'a pas encore persisté son ID.
    /// </summary>
    private async Task<string?> ResolveSeriesIdByNameAsync(string seriesName, CancellationToken cancellationToken)
    {
        var allSeries = await _apiClient.GetAllSeriesAsync(cancellationToken).ConfigureAwait(false);
        if (allSeries == null || !allSeries.Any()) return null;

        var normalizedSearch = NormalizeTitle(seriesName);
        string? bestId = null;
        int bestScore = 0;

        foreach (var serie in allSeries)
        {
            var normalizedApi = NormalizeTitle(serie.Title);
            if (string.IsNullOrWhiteSpace(normalizedApi)) continue;

            if (normalizedApi == normalizedSearch)
                return serie.Id.ToString(CultureInfo.InvariantCulture);

            int maxLen = Math.Max(normalizedSearch.Length, normalizedApi.Length);
            int dist = LevenshteinDistance(normalizedSearch, normalizedApi);
            int score = maxLen == 0 ? 0 : 100 - (dist * 100 / maxLen);
            if (score > 80 && score > bestScore)
            {
                bestScore = score;
                bestId = serie.Id.ToString(CultureInfo.InvariantCulture);
            }
        }
        return bestId;
    }

    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;
        string decomposed = title.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (char c in decomposed)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        string lower = sb.ToString().ToLowerInvariant();
        sb.Clear();
        foreach (char c in lower)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
                sb.Append(c);
        }
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length, m = t.Length;
        int[,] d = new int[n + 1, m + 1];
        if (n == 0) return m;
        if (m == 0) return n;
        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        return d[n, m];
    }
    
#if __EMBY__
    public async Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var options = new MediaBrowser.Common.Net.HttpRequestOptions
        {
             Url = url,
             CancellationToken = cancellationToken,
             BufferContent = false 
        };
        var response = await _httpClient.GetResponse(options).ConfigureAwait(false);
        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
             throw new Exception($"Failed to get image: {response.StatusCode}");
        }
        return response;
    }
#else
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("FankaiImageClient");
        return client.GetAsync(new Uri(url), cancellationToken);
    }
#endif
}
