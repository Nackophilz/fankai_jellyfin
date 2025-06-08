using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
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

        string? fankaiId = null;
        if (info.ProviderIds.TryGetValue(ProviderIdName, out var id))
        {
            fankaiId = id;
            _logger.LogDebug("Fankai ID depuis ProviderIds: {FankaiId}", fankaiId);
        }

        if (string.IsNullOrWhiteSpace(fankaiId) && !string.IsNullOrWhiteSpace(info.Name))
        {
            _logger.LogDebug("Tentative de trouver l'ID Fankai pour la série : Nom='{Name}' Année={Year}", info.Name, info.Year);
            var searchResults = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
            
            var bestMatch = searchResults.FirstOrDefault();

            if (bestMatch != null && bestMatch.ProviderIds.TryGetValue(ProviderIdName, out var foundId))
            {
                fankaiId = foundId;
                _logger.LogInformation("ID Fankai trouvé via recherche : {FankaiId} pour la série {SeriesName}", fankaiId, bestMatch.Name);
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

        // Ajout d'un log de diagnostic pour vérifier les données reçues de l'API
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
        
        // CORRECTION : Utilisation de la méthode d'extension SetProviderId, qui est la pratique standardisée.
        // Cette méthode gère correctement l'ajout ou la mise à jour de la valeur.
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

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fankai GetSearchResults pour la série : Nom='{Name}', Année={Year}", searchInfo.Name, searchInfo.Year);

        string? fankaiIdToSearch = null;
        if (searchInfo.ProviderIds.TryGetValue(ProviderIdName, out var idFromProvider))
        {
            fankaiIdToSearch = idFromProvider;
        }

        if (!string.IsNullOrWhiteSpace(fankaiIdToSearch))
        {
            var serie = await _apiClient.GetSerieByIdAsync(fankaiIdToSearch, cancellationToken).ConfigureAwait(false);
            if (serie != null)
            {
                var searchResult = new RemoteSearchResult
                {
                    Name = serie.Title,
                    ProductionYear = serie.Year, 
                    Overview = serie.Plot,
                    ImageUrl = serie.PosterImageUrl 
                };
                searchResult.ProviderIds.Add(ProviderIdName, serie.Id.ToString(CultureInfo.InvariantCulture));
                return new[] { searchResult };
            }
        }
        
        if (string.IsNullOrWhiteSpace(searchInfo.Name))
        {
            return Enumerable.Empty<RemoteSearchResult>();
        }

        var allSeries = await _apiClient.GetAllSeriesAsync(cancellationToken).ConfigureAwait(false);
        if (allSeries == null)
        {
            return Enumerable.Empty<RemoteSearchResult>();
        }

        var potentialMatches = new List<(FankaiSerie serie, int score)>();
        var searchName = searchInfo.Name;

        foreach (var serie in allSeries)
        {
            int score = 0;
            if (string.Equals(serie.Title, searchName, StringComparison.OrdinalIgnoreCase))
            {
                score = 10;
            }
            else if (string.Equals(serie.TitleForPlex, searchName, StringComparison.OrdinalIgnoreCase))
            {
                score = 9;
            }
            else if (serie.Title != null && serie.Title.Contains(searchName, StringComparison.OrdinalIgnoreCase))
            {
                score = 5;
            }
            else if (serie.TitleForPlex != null && serie.TitleForPlex.Contains(searchName, StringComparison.OrdinalIgnoreCase))
            {
                score = 4;
            }

            if (score > 0)
            {
                if (searchInfo.Year.HasValue && serie.Year.HasValue && searchInfo.Year.Value == serie.Year.Value)
                {
                    score++;
                }
                potentialMatches.Add((serie, score));
            }
        }

        _logger.LogDebug("Trouvé {Count} correspondances potentielles pour '{Name}'.", potentialMatches.Count, searchName);

        return potentialMatches
            .OrderByDescending(m => m.score)
            .Select(m =>
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
