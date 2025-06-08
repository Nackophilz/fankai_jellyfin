using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Fankai.Api;
using Jellyfin.Plugin.Fankai.Model;
using MediaBrowser.Controller.Entities.TV; 
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;      
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;


namespace Jellyfin.Plugin.Fankai.Providers;

public class SeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
{
    public string Name => "Fankai Series Provider";
    public int Order => 3; 

    private readonly ILogger<SeriesProvider> _logger;
    private readonly FankaiApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediaEncoder _mediaEncoder;

    public const string ProviderIdName = "FankaiSerieId";


    public SeriesProvider(IHttpClientFactory httpClientFactory, ILogger<SeriesProvider> logger, ILoggerFactory loggerFactory, IMediaEncoder mediaEncoder)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiClient = new FankaiApiClient(httpClientFactory, loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)));
        _mediaEncoder = mediaEncoder ?? throw new ArgumentNullException(nameof(mediaEncoder));
    }

    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fankai GetMetadata pour Series: Nom='{Name}', Année={Year}, Path='{Path}'", info.Name, info.Year, info.Path);
        var result = new MetadataResult<Series>();

        string? fankaiId = info.ProviderIds.GetValueOrDefault(ProviderIdName);

        if (!string.IsNullOrWhiteSpace(fankaiId) && !string.IsNullOrWhiteSpace(info.Name))
        {
            var initialData = await _apiClient.GetSerieByIdAsync(fankaiId, cancellationToken).ConfigureAwait(false);
            
            if (initialData != null && !NormalizeTitle(initialData.Title).Equals(NormalizeTitle(info.Name)))
            {
                _logger.LogWarning(
                    "INCOHÉRENCE DÉTECTÉE ! L'ID Fankai stocké '{FankaiId}' correspond à '{ApiTitle}', mais le nom de la série est '{SeriesName}'. Forçage de la ré-identification.",
                    fankaiId,
                    initialData.Title,
                    info.Name);
                fankaiId = null;
            }
        }
        
        if (string.IsNullOrWhiteSpace(fankaiId) && !string.IsNullOrWhiteSpace(info.Name))
        {
            _logger.LogDebug("Aucun ID Fankai valide. Lancement de la recherche pour : Nom='{Name}', Année={Year}", info.Name, info.Year);
            var searchResults = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
            
            var bestMatch = searchResults.FirstOrDefault();

            if (bestMatch != null && bestMatch.ProviderIds.TryGetValue(ProviderIdName, out var foundId))
            {
                fankaiId = foundId;
                _logger.LogInformation("ID Fankai trouvé/corrigé via recherche : {FankaiId} pour la série {SeriesName}", fankaiId, bestMatch.Name);
            }
            else
            {
                 _logger.LogWarning("Aucun ID Fankai trouvé via recherche pour la série : {Name}. Les métadonnées pourraient être incomplètes.", info.Name);
            }
        }
        
        if (string.IsNullOrWhiteSpace(fankaiId))
        {
            _logger.LogWarning("Aucun ID Fankai disponible pour la série: {Name}. Impossible de récupérer les métadonnées.", info.Name);
            return result;
        }

        var serieData = await _apiClient.GetSerieByIdAsync(fankaiId, cancellationToken).ConfigureAwait(false);
        if (serieData == null)
        {
            _logger.LogWarning("Aucune donnée retournée par l'API Fankai pour l'ID: {FankaiId}", fankaiId);
            return result;
        }

        _logger.LogInformation(
            "Données Fankai reçues pour ID {FankaiId}: ImdbId='{ImdbId}', TmdbId='{TmdbId}', TvdbId='{TvdbId}'",
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
            ForcedSortName = serieData.SortTitle
        };
        
        result.Item.SetProviderId(ProviderIdName, fankaiId);
        if (!string.IsNullOrWhiteSpace(serieData.ImdbId)) {
            result.Item.SetProviderId(MetadataProvider.Imdb, serieData.ImdbId);
        }
        if (!string.IsNullOrWhiteSpace(serieData.TmdbId)) {
            result.Item.SetProviderId(MetadataProvider.Tmdb, serieData.TmdbId);
        }
        if (!string.IsNullOrWhiteSpace(serieData.TvdbId)) {
            result.Item.SetProviderId(MetadataProvider.Tvdb, serieData.TvdbId);
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
                _logger.LogInformation("Tentative de téléchargement du thème musical pour '{SeriesName}' depuis {ThemeUrl}", 
                                       serieData.Title, serieData.ThemeMusicUrl);
                try
                {
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
                    _logger.LogInformation("Thème musical temporaire téléchargé vers {TempPath}", tempThemePath);

                    await RemuxAudioAsync(tempThemePath, localThemePath, cancellationToken);
                    
                    result.HasMetadata = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors du téléchargement ou de la réparation du thème musical pour '{SeriesName}'.", 
                                     serieData.Title);
                }
                finally
                {
                    if (File.Exists(tempThemePath))
                    {
                        try { File.Delete(tempThemePath); }
                        catch (Exception delEx) { _logger.LogWarning(delEx, "Échec de la suppression du fichier de thème temporaire : {TempPath}", tempThemePath); }
                    }
                }
            }
            else
            {
                _logger.LogDebug("Le fichier theme.mp3 existe déjà pour '{SeriesName}'. Pas de téléchargement.", 
                                 serieData.Title);
            }
        }
        else if (!string.IsNullOrWhiteSpace(serieData.ThemeMusicUrl) && string.IsNullOrWhiteSpace(info.Path))
        {
            _logger.LogWarning("Impossible de télécharger le thème musical pour '{SeriesName}' car info.Path n'est pas disponible.", serieData.Title);
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
                        Type = PersonKind.Actor 
                    };
                    result.AddPerson(person);
                }
            }
        }

        if (result.Item != null && result.Item.Name == serieData.Title) 
        {
             result.HasMetadata = true;
        }
       
        _logger.LogInformation("Métadonnées récupérées et traitées pour l'ID Fankai: {FankaiId}, Série: {Title}", fankaiId, serieData.Title);
        return result;
    }
    
    private async Task RemuxAudioAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Tentative de réparation du fichier audio '{InputPath}' vers '{OutputPath}' avec FFmpeg.", inputPath, outputPath);
        
        string ffmpegPath = _mediaEncoder.EncoderPath;
        
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
            catch (Exception ex) { _logger.LogWarning(ex, "Erreur lors de la tentative d'arrêt du processus FFmpeg."); }
        });

        process.Start();

        string errorOutput = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode == 0 && File.Exists(outputPath))
        {
            _logger.LogInformation("Réparation du thème musical réussie pour '{OutputPath}'.", outputPath);
        }
        else
        {
            if (File.Exists(outputPath))
            {
                try { File.Delete(outputPath); } catch (Exception ex) { _logger.LogWarning(ex, "Échec de la suppression du fichier de sortie FFmpeg partiel : {OutputPath}", outputPath); }
            }
            
            var ffmpegException = new Exception("Sortie d'erreur de FFmpeg : " + errorOutput);
            _logger.LogError(ffmpegException, "Échec de la réparation FFmpeg pour '{InputPath}'. Code de sortie : {ExitCode}", inputPath, process.ExitCode);
            
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
        _logger.LogInformation("Fankai GetSearchResults pour la série : Nom='{Name}', Année={Year}", searchInfo.Name, searchInfo.Year);

        
        if (string.IsNullOrWhiteSpace(searchInfo.Name))
        {
            return Enumerable.Empty<RemoteSearchResult>();
        }

        var allSeries = await _apiClient.GetAllSeriesAsync(cancellationToken).ConfigureAwait(false);
        if (allSeries == null)
        {
            _logger.LogWarning("La liste complète des séries n'a pas pu être récupérée depuis l'API Fankai.");
            return Enumerable.Empty<RemoteSearchResult>();
        }

        var potentialMatches = new List<(FankaiSerie serie, int score)>();
        var searchName = searchInfo.Name;
        var normalizedSearchName = NormalizeTitle(searchName);

        _logger.LogDebug("Début de la recherche par similarité pour '{SearchName}' (Normalisé: '{NormalizedSearchName}')", searchName, normalizedSearchName);

        foreach (var serie in allSeries)
        {
            int titleDistance = LevenshteinDistance(normalizedSearchName, NormalizeTitle(serie.Title));
            int plexTitleDistance = LevenshteinDistance(normalizedSearchName, NormalizeTitle(serie.TitleForPlex));
            int originalTitleDistance = LevenshteinDistance(normalizedSearchName, NormalizeTitle(serie.OriginalTitle));
            
            int bestDistance = Math.Min(titleDistance, Math.Min(plexTitleDistance, originalTitleDistance));

            int score = 100 - (bestDistance * 5); 

            if (score > 50) 
            {
                if (searchInfo.Year.HasValue && serie.Year.HasValue && searchInfo.Year.Value == serie.Year.Value)
                {
                    score += 20;
                }
                _logger.LogDebug("Correspondance potentielle pour '{ApiTitle}' avec une distance de {Distance} et un score de {Score}.", serie.Title, bestDistance, score);
                potentialMatches.Add((serie, score));
            }
        }

        if (!potentialMatches.Any())
        {
            _logger.LogWarning("Aucune correspondance de titre trouvée pour '{SearchName}'.", searchName);
            return Enumerable.Empty<RemoteSearchResult>();
        }

        var orderedMatches = potentialMatches.OrderByDescending(m => m.score).ToList();

        _logger.LogInformation("Meilleure correspondance pour '{SearchName}' est '{BestMatchTitle}' avec un score de {BestMatchScore}",
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
        _logger.LogWarning("Impossible de parser la chaîne de date : {DateString}", dateString);
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

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("FankaiSeriesImageClient");
        return client.GetAsync(new Uri(url), cancellationToken);
    }
}
