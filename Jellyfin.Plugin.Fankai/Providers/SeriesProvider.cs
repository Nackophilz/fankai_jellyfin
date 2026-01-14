using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if !__EMBY__
using Jellyfin.Data.Enums;
using System.Net.Http;
#endif
using Jellyfin.Plugin.Fankai.Api;
using Jellyfin.Plugin.Fankai.Model;
using MediaBrowser.Controller.Entities.TV; 
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;      
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.MediaEncoding;
#if __EMBY__
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
#endif
using Microsoft.Extensions.Logging;


namespace Jellyfin.Plugin.Fankai.Providers;

public class SeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
{
    public string Name => "Fankai Series Provider";
    public int Order => 3; 

#if __EMBY__
    private readonly MediaBrowser.Model.Logging.ILogger _logger;
    private readonly IHttpClient _httpClient;
    private readonly IFfmpegManager _ffmpegManager;
#else
    private readonly ILogger<SeriesProvider> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediaEncoder _mediaEncoder;
#endif
    private readonly FankaiApiClient _apiClient;

    public const string ProviderIdName = "FankaiSerieId";

#if __EMBY__
    public SeriesProvider(IHttpClient httpClient, ILogManager logManager, IFfmpegManager ffmpegManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logManager.GetLogger(GetType().Name);
        _ffmpegManager = ffmpegManager ?? throw new ArgumentNullException(nameof(ffmpegManager));
        _apiClient = new FankaiApiClient(httpClient, _logger);
    }
#else
    public SeriesProvider(IHttpClientFactory httpClientFactory, ILogger<SeriesProvider> logger, ILoggerFactory loggerFactory, IMediaEncoder mediaEncoder)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiClient = new FankaiApiClient(httpClientFactory, loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)));
        _mediaEncoder = mediaEncoder ?? throw new ArgumentNullException(nameof(mediaEncoder));
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

    private void LogDebug(string message, params object[] args)
    {
#if __EMBY__
        _logger.Debug(message, args);
#else
        _logger.LogDebug(message, args);
#endif
    }

    private void LogError(Exception ex, string message, params object[] args)
    {
#if __EMBY__
        _logger.ErrorException(string.Format(message, args), ex);
#else
        _logger.LogError(ex, message, args);
#endif
    }

    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        LogInfo("Fankai GetMetadata pour Series: Nom='{0}', Année={1}, Path='{2}'", info.Name, info.Year, info.Path);
        var result = new MetadataResult<Series>();

        string? fankaiId = info.ProviderIds.GetValueOrDefault(ProviderIdName);
        var folderName = Path.GetFileName(info.Path);
        string nameForSearch = info.Name;

        if (!string.IsNullOrWhiteSpace(fankaiId))
        {
            var initialData = await _apiClient.GetSerieByIdAsync(fankaiId, cancellationToken).ConfigureAwait(false);
            
            if (initialData != null)
            {
                var normalizedApiTitle = NormalizeTitle(initialData.Title);
                var normalizedItemName = NormalizeTitle(info.Name);
                var normalizedFolderName = NormalizeTitle(folderName);

                if (!normalizedApiTitle.Equals(normalizedItemName) || !normalizedApiTitle.Equals(normalizedFolderName))
                {
                     LogWarn(
                        "INCOHÉRENCE DÉTECTÉE ! L'ID '{0}' ('{1}') ne correspond pas au nom de l'item ('{2}') OU au nom du dossier ('{3}'). Forçage de la ré-identification en utilisant le nom du dossier.",
                        fankaiId, initialData.Title, info.Name, folderName);
                    
                    fankaiId = null; 
                    nameForSearch = folderName; 
                }
            }
        }
        
        if (string.IsNullOrWhiteSpace(fankaiId))
        {
            LogDebug("Aucun ID Fankai valide ou ré-identification forcée. Lancement de la recherche pour : Nom='{0}', Année={1}", nameForSearch, info.Year);
            
            var correctSearchInfo = new SeriesInfo
            {
                Name = nameForSearch,
                Path = info.Path,
                Year = info.Year
            };
            
            var searchResults = await GetSearchResults(correctSearchInfo, cancellationToken).ConfigureAwait(false);
            
            var bestMatch = searchResults.FirstOrDefault();

            if (bestMatch != null && bestMatch.ProviderIds.TryGetValue(ProviderIdName, out var foundId))
            {
                fankaiId = foundId;
                LogInfo("ID Fankai trouvé/corrigé via recherche : {0} pour la série {1}", fankaiId, bestMatch.Name);
            }
            else
            {
                 LogWarn("Aucun ID Fankai trouvé via recherche pour la série : {0}.", nameForSearch);
            }
        }
        
        if (string.IsNullOrWhiteSpace(fankaiId))
        {
            LogWarn("Aucun ID Fankai disponible pour la série: {0}. Impossible de récupérer les métadonnées.", info.Name);
            return result;
        }

        var serieData = await _apiClient.GetSerieByIdAsync(fankaiId, cancellationToken).ConfigureAwait(false);
        if (serieData == null)
        {
            LogWarn("Aucune donnée retournée par l'API Fankai pour l'ID: {0}", fankaiId);
            return result;
        }

        LogInfo(
            "Données Fankai reçues pour ID {0}: ImdbId='{1}', TmdbId='{2}', TvdbId='{3}'",
            fankaiId,
            serieData.ImdbId ?? "null",
            serieData.TmdbId ?? "null",
            serieData.TvdbId ?? "null");

        result.Item = new Series 
        {
            Name = serieData.Title,
            OriginalTitle = serieData.OriginalTitle,
            Overview = serieData.Plot,
            PremiereDate = TryParseDate(serieData.Premiered), 
            ProductionYear = serieData.Year, 
            OfficialRating = serieData.Mpaa,
            Studios = !string.IsNullOrWhiteSpace(serieData.Studio) ? new[] { serieData.Studio } : Array.Empty<string>(),
            Path = info.Path,
            Tagline = serieData.Tagline,
            Status = ParseSeriesStatus(serieData.Status),
#if __EMBY__
            SortName = serieData.SortTitle,
            DisplayOrder = serieData.OriginalTitle?.Contains("One piece", StringComparison.OrdinalIgnoreCase) == true 
                ? MediaBrowser.Model.Entities.SeriesDisplayOrder.Absolute 
                : MediaBrowser.Model.Entities.SeriesDisplayOrder.Aired,
#else
            ForcedSortName = serieData.SortTitle,
            DisplayOrder = serieData.OriginalTitle?.Contains("One piece", StringComparison.OrdinalIgnoreCase) == true 
                ? "absolute" 
                : "",
#endif
        };
        
        result.Item.SetProviderId(ProviderIdName, fankaiId);
        if (!string.IsNullOrWhiteSpace(serieData.ImdbId)) {
#if __EMBY__
            result.Item.SetProviderId("Imdb", serieData.ImdbId);
#else
            result.Item.SetProviderId(MetadataProvider.Imdb, serieData.ImdbId);
#endif
        }
        if (!string.IsNullOrWhiteSpace(serieData.TmdbId)) {
#if __EMBY__
            result.Item.SetProviderId("Tmdb", serieData.TmdbId);
#else
            result.Item.SetProviderId(MetadataProvider.Tmdb, serieData.TmdbId);
#endif
        }
        if (!string.IsNullOrWhiteSpace(serieData.TvdbId)) {
#if __EMBY__
            result.Item.SetProviderId("Tvdb", serieData.TvdbId);
#else
            result.Item.SetProviderId(MetadataProvider.Tvdb, serieData.TvdbId);
#endif
        }

        if (serieData.RatingValue.HasValue)
        {
            result.Item.CommunityRating = serieData.RatingValue.Value;
        }

        if (!string.IsNullOrWhiteSpace(serieData.Genres))
        {
            result.Item.Genres = serieData.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        
        if (!string.IsNullOrWhiteSpace(serieData.ThemeMusicUrl) && !string.IsNullOrWhiteSpace(info.Path))
        {
            string themeFileName = "theme.mp3";
            string localThemePath = Path.Combine(info.Path, themeFileName);
            string tempThemePath = localThemePath + ".tmp";

            if (!File.Exists(localThemePath))
            {
                LogInfo("Tentative de téléchargement du thème musical pour '{0}' depuis {1}", 
                                       serieData.Title, serieData.ThemeMusicUrl);
                try
                {
#if __EMBY__
                    var options = new MediaBrowser.Common.Net.HttpRequestOptions
                    {
                        Url = serieData.ThemeMusicUrl,
                        CancellationToken = cancellationToken
                    };
                    using (var response = await _httpClient.GetResponse(options).ConfigureAwait(false))
                    {
                         using (var fileStream = new FileStream(tempThemePath, FileMode.Create, FileAccess.Write, FileShare.None))
                         {
                             await response.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                         }
                    }
#else
                    using (var httpClient = _httpClientFactory.CreateClient())
                    {
                        var response = await httpClient.GetAsync(serieData.ThemeMusicUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                        response.EnsureSuccessStatusCode();

                        using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                        using (var fileStream = new FileStream(tempThemePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await stream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                        }
                    }
#endif
                    LogInfo("Thème musical temporaire téléchargé vers {0}", tempThemePath);

                    await RemuxAudioAsync(tempThemePath, localThemePath, cancellationToken);
                    
                    result.HasMetadata = true;
                }
                catch (Exception ex)
                {
                    LogError(ex, "Erreur lors du téléchargement ou de la réparation du thème musical pour '{0}'.", 
                                     serieData.Title);
                }
                finally
                {
                    if (File.Exists(tempThemePath))
                    {
                        try { File.Delete(tempThemePath); }
                        catch (Exception delEx) { LogWarn("Échec de la suppression du fichier de thème temporaire : {0}", tempThemePath); }
                    }
                }
            }
            else
            {
                LogDebug("Le fichier theme.mp3 existe déjà pour '{0}'. Pas de téléchargement.", 
                                 serieData.Title);
            }
        }
        else if (!string.IsNullOrWhiteSpace(serieData.ThemeMusicUrl) && string.IsNullOrWhiteSpace(info.Path))
        {
            LogWarn("Impossible de télécharger le thème musical pour '{0}' car info.Path n'est pas disponible.", serieData.Title);
        }

        var actorsResponse = await _apiClient.GetActorsForSerieAsync(fankaiId, cancellationToken).ConfigureAwait(false);
        if (actorsResponse?.Actors != null)
        {
            foreach (var actorData in actorsResponse.Actors)
            {
                if (!string.IsNullOrWhiteSpace(actorData.Name))
                {
                    var person = new PersonInfo
                    {
                        Name = actorData.Name,
                        Role = actorData.Role,
#if __EMBY__
                        Type = MediaBrowser.Model.Entities.PersonType.Actor
#else
                        Type = PersonKind.Actor 
#endif
                    };
                    result.AddPerson(person);
                }
            }
        }

        if (result.Item != null && result.Item.Name == serieData.Title) 
        {
             result.HasMetadata = true;
        }
       
        LogInfo("Métadonnées récupérées et traitées pour l'ID Fankai: {0}, Série: {1}", fankaiId, serieData.Title);
        return result;
    }
    
    private async Task RemuxAudioAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        LogInfo("Tentative de réparation du fichier audio '{0}' vers '{1}' avec FFmpeg.", inputPath, outputPath);
        
#if __EMBY__
        string ffmpegPath = _ffmpegManager.FfmpegConfiguration.EncoderPath;
#else
        string ffmpegPath = _mediaEncoder.EncoderPath;
#endif
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-i \"{inputPath}\" -c:a copy -map_metadata -1 \"{outputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            }
        };

        cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(); }
            catch (Exception ex) { LogWarn("Erreur lors de la tentative d'arrêt du processus FFmpeg."); }
        });

        process.Start();

        string errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode == 0 && File.Exists(outputPath))
        {
            LogInfo("Réparation du thème musical réussie pour '{0}'.", outputPath);
        }
        else
        {
            if (File.Exists(outputPath))
            {
                try { File.Delete(outputPath); } catch (Exception ex) { LogWarn("Échec de la suppression du fichier de sortie FFmpeg partiel : {0}", outputPath); }
            }
            
            var ffmpegException = new Exception("Sortie d'erreur de FFmpeg : " + errorOutput);
            LogError(ffmpegException, "Échec de la réparation FFmpeg pour '{0}'. Code de sortie : {1}", inputPath, process.ExitCode);
            
            throw new Exception($"Échec du processus FFmpeg pour '{inputPath}' avec le code de sortie {process.ExitCode}.", ffmpegException);
        }
    }
    
    private static string NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }
        
        string decomposed = title.Normalize(NormalizationForm.FormD);
        
        var sb = new StringBuilder();
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        
        string almostNormalized = sb.ToString().ToLowerInvariant();
        sb.Clear();
        foreach(char c in almostNormalized)
        {
            if (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
            {
                sb.Append(c);
            }
        }

        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }
    
    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++);
        for (int j = 0; j <= m; d[0, j] = j++);

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
    {
        LogInfo("Fankai GetSearchResults pour la série : Nom='{0}', Année={1}", searchInfo.Name, searchInfo.Year);
        
        if (string.IsNullOrWhiteSpace(searchInfo.Name))
        {
            return Enumerable.Empty<RemoteSearchResult>();
        }

        var allSeries = await _apiClient.GetAllSeriesAsync(cancellationToken).ConfigureAwait(false);
        if (allSeries == null)
        {
            LogWarn("La liste complète des séries n'a pas pu être récupérée depuis l'API Fankai.");
            return Enumerable.Empty<RemoteSearchResult>();
        }

        var potentialMatches = new List<(FankaiSerie serie, int score)>();
        var searchName = searchInfo.Name;
        var normalizedSearchName = NormalizeTitle(searchName);

        LogDebug("Début de la recherche par similarité pour '{0}' (Normalisé: '{1}')", searchName, normalizedSearchName);

        foreach (var serie in allSeries)
        {
            var normalizedApiTitle = NormalizeTitle(serie.Title);
            
            int distance = LevenshteinDistance(normalizedSearchName, normalizedApiTitle);
            
            int score = 100 - (distance * 10);

            if (score > 75) 
            {
                if (searchInfo.Year.HasValue && serie.Year.HasValue && searchInfo.Year.Value == serie.Year.Value)
                {
                    score += 15;
                }
                LogDebug("Correspondance potentielle pour '{0}' avec une distance de {1} et un score de {2}.", serie.Title, distance, score);
                potentialMatches.Add((serie, score));
            }
        }

        if (!potentialMatches.Any())
        {
            LogWarn("Aucune correspondance de titre trouvée pour '{0}'.", searchName);
            return Enumerable.Empty<RemoteSearchResult>();
        }

        var orderedMatches = potentialMatches.OrderByDescending(m => m.score).ToList();

        LogInfo("Meilleure correspondance pour '{0}' est '{1}' avec un score de {2}",
            searchName, orderedMatches.First().serie.Title, orderedMatches.First().score);

        return orderedMatches.Select(m =>
            {
                var searchResult = new RemoteSearchResult
                {
                    Name = m.serie.Title,
                    ProductionYear = m.serie.Year,
                    Overview = m.serie.Plot,
                    ImageUrl = m.serie.PosterImageUrl
                };
                searchResult.ProviderIds.Add(ProviderIdName, m.serie.Id.ToString(CultureInfo.InvariantCulture));
                return searchResult;
            });
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
    
    private SeriesStatus? ParseSeriesStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.ToLowerInvariant() switch
        {
            "continuing" => SeriesStatus.Continuing,
            "ended" => SeriesStatus.Ended,
            _ => null
        };
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
        // Emby doesn't always throw on error for GetResponse? 
        // HttpResponseInfo has StatusCode
        if (response.StatusCode != System.Net.HttpStatusCode.OK)
        {
             throw new Exception($"Failed to get image: {response.StatusCode}");
        }
        return response;
    }
#else
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("FankaiSeriesImageClient");
        return client.GetAsync(new Uri(url), cancellationToken);
    }
#endif
}
