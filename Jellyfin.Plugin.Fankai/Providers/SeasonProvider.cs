using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fankai.Api;
using Jellyfin.Plugin.Fankai.Model;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
#if __EMBY__
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
#endif

namespace Jellyfin.Plugin.Fankai.Providers;

/// <summary>
/// Fournit les métadonnées pour les saisons depuis l'API Fankai.
/// </summary>
public class SeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>, IHasOrder
{
    public string Name => "Fankai Season Provider";
    
    // S'exécute après le SeriesProvider (Order = 3) pour s'assurer que l'ID de série est disponible.
    public int Order => 4; 

#if __EMBY__
    private readonly MediaBrowser.Model.Logging.ILogger _logger;
    private readonly IHttpClient _httpClient;
#else
    private readonly ILogger<SeasonProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
#endif
    private readonly FankaiApiClient _apiClient;

    // Nous réutilisons la même clé que FankaiImageProvider pour la consistance.
    public const string ProviderIdName = FankaiImageProvider.FankaiSeasonIdProviderKey;

#if __EMBY__
    public SeasonProvider(IHttpClient httpClient, ILogManager logManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logManager.GetLogger(GetType().Name);
        _apiClient = new FankaiApiClient(httpClient, _logger);
    }
#else
    public SeasonProvider(IHttpClientFactory httpClientFactory, ILogger<SeasonProvider> logger, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiClient = new FankaiApiClient(httpClientFactory, loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)));
    }
#endif

    private void LogInfo(string message, params object[] args)
    {
#if __EMBY__
        _logger.Info(message, args);
#else
        _logger.LogInformation(message, args);
#endif
    }

    private void LogWarn(string message, params object[] args)
    {
#if __EMBY__
        _logger.Warn(message, args);
#else
        _logger.LogWarning(message, args);
#endif
    }

    public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
    {
        LogInfo("Fankai GetMetadata pour Season: Nom='{0}', Numéro de saison={1}", 
            info.Name, info.IndexNumber);
        
        var result = new MetadataResult<Season>();

        // 1. Obtenir l'ID Fankai de la série parente. C'est indispensable.
        string? fankaiSeriesId = info.SeriesProviderIds.GetValueOrDefault(SeriesProvider.ProviderIdName);
        if (string.IsNullOrWhiteSpace(fankaiSeriesId))
        {
            // Fallback : tenter de résoudre l'ID de série depuis le nom de la série parente.
            // Cela arrive lors d'un scan initial où SeriesProvider n'a pas encore persisté son ID.
            if (!string.IsNullOrWhiteSpace(info.SeriesName))
            {
                LogInfo("ID de série Fankai absent pour la saison '{0}'. Tentative de résolution via le nom de série '{1}'.", info.Name, info.SeriesName);
                fankaiSeriesId = await ResolveSeriesIdByNameAsync(info.SeriesName, cancellationToken).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(fankaiSeriesId))
            {
                LogWarn("Impossible de trouver l'ID de série Fankai pour la saison '{0}' (SeriesName='{1}'). Aucune correspondance trouvée.", info.Name, info.SeriesName);
                return result;
            }
        }

        // 2. Récupérer toutes les saisons pour cette série depuis l'API.
        var seasonsResponse = await _apiClient.GetSeasonsForSerieAsync(fankaiSeriesId, cancellationToken).ConfigureAwait(false);
        if (seasonsResponse?.Seasons == null || !seasonsResponse.Seasons.Any())
        {
            LogWarn("Aucune saison retournée par l'API Fankai pour l'ID de série : {0}", fankaiSeriesId);
            return result;
        }

        // 3. Trouver la saison correspondante dans la liste retournée par l'API.
        FankaiSeason? matchedSeason = null;
        
        // Priorité 1 : Chercher avec un ID de saison Fankai déjà stocké.
        if (info.ProviderIds.TryGetValue(ProviderIdName, out var seasonId))
        {
            matchedSeason = seasonsResponse.Seasons.FirstOrDefault(s => s.Id.ToString(CultureInfo.InvariantCulture) == seasonId);
        }

        // Priorité 2 (Fallback) : Chercher par le numéro de saison si aucun ID n'est trouvé.
        if (matchedSeason == null && info.IndexNumber.HasValue)
        {
            matchedSeason = seasonsResponse.Seasons.FirstOrDefault(s => s.SeasonNumber == info.IndexNumber.Value);
        }

        if (matchedSeason == null)
        {
            LogWarn("Impossible de faire correspondre la saison (Numéro: {0}) avec les données de l'API Fankai pour la série ID {1}", 
                info.IndexNumber, fankaiSeriesId);
            return result;
        }
        
        LogInfo("Correspondance trouvée pour la saison : {0} (ID Fankai: {1})", matchedSeason.Title, matchedSeason.Id);

        // 4. Remplir l'objet Season avec les métadonnées.
        result.Item = new Season
        {
            Name = matchedSeason.Title,
            Overview = matchedSeason.Plot,
            IndexNumber = matchedSeason.SeasonNumber,
            PremiereDate = TryParseDate(matchedSeason.Premiered),
#if __EMBY__
            SortName = matchedSeason.SortTitle
#else
            ForcedSortName = matchedSeason.SortTitle
#endif
        };
        
        // Logique pour l'année de production
        if (matchedSeason.Year.HasValue)
        {
            result.Item.ProductionYear = matchedSeason.Year.Value;
        }
        else if (DateTime.TryParse(matchedSeason.Premiered, out var premiereDate))
        {
            result.Item.ProductionYear = premiereDate.Year;
        }

        // 5. Stocker les IDs de fournisseurs (Fankai, IMDb, TMDB, TVDB).
        // CORRECTION : Utilisation de l'indexeur '[]' pour garantir que les IDs sont définis (ajoutés ou écrasés).
        result.Item.ProviderIds[ProviderIdName] = matchedSeason.Id.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(matchedSeason.ImdbId)) {
#if __EMBY__
             result.Item.SetProviderId("Imdb", matchedSeason.ImdbId);
#else
             result.Item.SetProviderId(MetadataProvider.Imdb, matchedSeason.ImdbId);
#endif
        }
        if (!string.IsNullOrWhiteSpace(matchedSeason.TmdbId)) {
#if __EMBY__
            result.Item.SetProviderId("Tmdb", matchedSeason.TmdbId);
#else
            result.Item.SetProviderId(MetadataProvider.Tmdb, matchedSeason.TmdbId);
#endif
        }
        if (!string.IsNullOrWhiteSpace(matchedSeason.TvdbId))
        {
#if __EMBY__
            result.Item.SetProviderId("Tvdb", matchedSeason.TvdbId);
#else
            result.Item.SetProviderId(MetadataProvider.Tvdb, matchedSeason.TvdbId);
#endif
        }
        
        result.HasMetadata = true;
        return result;
    }

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
    {
        LogInfo("La recherche de saison n'est pas supportée et n'est généralement pas nécessaire. Elle est identifiée via la série parente.");
        return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
    }

    /// <summary>
    /// Tente de résoudre l'ID Fankai d'une série à partir de son nom, en utilisant la liste complète des séries.
    /// Utilisé comme fallback quand SeriesProvider n'a pas encore persisté son ID.
    /// </summary>
    private async Task<string?> ResolveSeriesIdByNameAsync(string seriesName, CancellationToken cancellationToken)
    {
        var allSeries = await _apiClient.GetAllSeriesAsync(cancellationToken).ConfigureAwait(false);
        if (allSeries == null || !allSeries.Any())
        {
            LogWarn("ResolveSeriesIdByName: Impossible de récupérer la liste des séries depuis l'API.");
            return null;
        }

        var normalizedSearch = NormalizeTitle(seriesName);
        FankaiSerie? bestMatch = null;
        int bestScore = 0;

        foreach (var serie in allSeries)
        {
            var normalizedApi = NormalizeTitle(serie.Title);
            if (string.IsNullOrWhiteSpace(normalizedApi)) continue;

            // Correspondance exacte en priorité
            if (normalizedApi == normalizedSearch)
            {
                LogDebug("ResolveSeriesIdByName: Correspondance exacte trouvée pour '{0}' -> ID {1}", seriesName, serie.Id);
                return serie.Id.ToString(CultureInfo.InvariantCulture);
            }

            // Score de similarité simple
            int maxLen = Math.Max(normalizedSearch.Length, normalizedApi.Length);
            int dist = LevenshteinDistance(normalizedSearch, normalizedApi);
            int score = maxLen == 0 ? 0 : 100 - (dist * 100 / maxLen);
            if (score > 80 && score > bestScore)
            {
                bestScore = score;
                bestMatch = serie;
            }
        }

        if (bestMatch != null)
        {
            LogInfo("ResolveSeriesIdByName: Meilleure correspondance pour '{0}' -> '{1}' (score={2}, ID={3})",
                seriesName, bestMatch.Title, bestScore, bestMatch.Id);
            return bestMatch.Id.ToString(CultureInfo.InvariantCulture);
        }

        LogWarn("ResolveSeriesIdByName: Aucune correspondance trouvée pour '{0}'.", seriesName);
        return null;
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
        string almostNormalized = sb.ToString().ToLowerInvariant();
        sb.Clear();
        foreach (char c in almostNormalized)
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

    private void LogDebug(string message, params object[] args)
    {
#if __EMBY__
        _logger.Debug(message, args);
#else
        _logger.LogDebug(message, args);
#endif
    }

    private DateTime? TryParseDate(string? dateString)
    {
        if (string.IsNullOrWhiteSpace(dateString)) return null;
        if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            return date.ToUniversalTime();
        }
        LogWarn("Impossible de parser la chaîne de date : {0}", dateString);
        return null;
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
        var client = _httpClientFactory.CreateClient("FankaiSeasonImageClient");
        return client.GetAsync(new Uri(url), cancellationToken);
    }
#endif
}
