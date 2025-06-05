using System;
using System.Collections.Generic;
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
using Microsoft.Extensions.Logging;


namespace Jellyfin.Plugin.Fankai.Providers;

public class SeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
{
    public string Name => "Fankai Series Provider";
    public int Order => 3; 

    private readonly ILogger<SeriesProvider> _logger;
    private readonly FankaiApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory; 

    public const string ProviderIdName = "FankaiSerieId";

    public SeriesProvider(IHttpClientFactory httpClientFactory, ILogger<SeriesProvider> logger, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiClient = new FankaiApiClient(httpClientFactory, loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)));
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
            _logger.LogDebug("Tentative de trouver l'ID Fankai pour la série: Nom='{Name}' Année={Year}", info.Name, info.Year);
            var searchResults = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
            var bestMatch = searchResults.FirstOrDefault(r => 
                                string.Equals(r.Name, info.Name, StringComparison.OrdinalIgnoreCase) &&
                                (!info.Year.HasValue || r.ProductionYear == info.Year))
                            ?? searchResults.FirstOrDefault(r => 
                                r.Name != null && info.Name != null && r.Name.Contains(info.Name, StringComparison.OrdinalIgnoreCase) &&
                                (!info.Year.HasValue || r.ProductionYear == info.Year))
                            ?? searchResults.FirstOrDefault();

            if (bestMatch != null && bestMatch.ProviderIds.TryGetValue(ProviderIdName, out var foundId))
            {
                fankaiId = foundId;
                _logger.LogDebug("ID Fankai trouvé via recherche: {FankaiId} pour la série {SeriesName}", fankaiId, bestMatch.Name);
            }
            else
            {
                 _logger.LogWarning("Aucun ID Fankai trouvé via recherche pour la série: {Name}. Les métadonnées pourraient être incomplètes.", info.Name);
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

        result.Item = new Series 
        {
            Name = serieData.Title,
            OriginalTitle = serieData.OriginalTitle,
            Overview = serieData.Plot,
            PremiereDate = TryParseDate(serieData.Premiered), 
            ProductionYear = serieData.Year, 
            OfficialRating = serieData.Mpaa,
            Studios = !string.IsNullOrWhiteSpace(serieData.Studio) ? new[] { serieData.Studio } : Array.Empty<string>(),
            Path = info.Path // Assigner le chemin depuis SeriesInfo à l'item Series en cours de création
        };
        
        result.Item.ProviderIds.TryAdd(ProviderIdName, fankaiId);

        if (serieData.RatingValue.HasValue)
        {
            result.Item.CommunityRating = serieData.RatingValue.Value;
        }

        if (!string.IsNullOrWhiteSpace(serieData.Genres))
        {
            result.Item.Genres = serieData.Genres.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        
        // **Logique de téléchargement du thème musical**
        if (!string.IsNullOrWhiteSpace(serieData.ThemeMusicUrl) && !string.IsNullOrWhiteSpace(info.Path))
        {
            string themeFileName = "theme.mp3";
            // Construire le chemin local en utilisant info.Path
            string localThemePath = Path.Combine(info.Path, themeFileName);

            if (!File.Exists(localThemePath))
            {
                _logger.LogInformation("Tentative de téléchargement du thème musical pour '{SeriesName}' depuis {ThemeUrl} vers {LocalPath}", 
                                       serieData.Title, serieData.ThemeMusicUrl, localThemePath);
                try
                {
                    using var httpClient = _httpClientFactory.CreateClient();
                    // HttpCompletionOption.ResponseHeadersRead
                    var response = await httpClient.GetAsync(serieData.ThemeMusicUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode(); // Lève une exception si le statut n'est pas succès

                    // Utiliser un FileStream))
                    using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    using var fileStream = new FileStream(localThemePath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await stream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                    
                    _logger.LogInformation("Thème musical téléchargé avec succès pour '{SeriesName}' à l'emplacement {LocalPath}", 
                                           serieData.Title, localThemePath);
                    result.HasMetadata = true; // Indiquer qu'une mise à jour (locale) a eu lieu
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, "Erreur HTTP lors du téléchargement du thème musical pour '{SeriesName}' depuis {ThemeUrl}.", 
                                     serieData.Title, serieData.ThemeMusicUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erreur lors du téléchargement ou de la sauvegarde du thème musical pour '{SeriesName}' vers {LocalPath}.", 
                                     serieData.Title, localThemePath);
                    // Essayer de supprimer un fichier en cas d'erreur.
                    if (File.Exists(localThemePath))
                    {
                        try { File.Delete(localThemePath); } catch (Exception delEx) { _logger.LogWarning(delEx, "Échec de la suppression du fichier de thème partiel : {LocalPath}", localThemePath); }
                    }
                }
            }
            else
            {
                _logger.LogDebug("Le fichier theme.mp3 existe déjà pour '{SeriesName}' à l'emplacement {LocalPath}. Pas de téléchargement.", 
                                 serieData.Title, localThemePath);
            }
        }
        // Log si l'URL du thème existe mais le chemin de la série n'est pas disponible via info.Path
        else if (!string.IsNullOrWhiteSpace(serieData.ThemeMusicUrl) && string.IsNullOrWhiteSpace(info.Path))
        {
            _logger.LogWarning("Impossible de télécharger le thème musical pour '{SeriesName}' car info.Path (chemin de la série) n'est pas disponible.", serieData.Title);
        }

        var actorsResponse = await _apiClient.GetActorsForSerieAsync(fankaiId, cancellationToken).ConfigureAwait(false);
        if (actorsResponse?.Actors != null)
        {
            foreach (var actorData in actorsResponse.Actors)
            {
                if (!string.IsNullOrWhiteSpace(actorData.Name))
                {
                    var person = new MediaBrowser.Controller.Entities.PersonInfo
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

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fankai GetSearchResults pour la série: Nom='{Name}', Année={Year}", searchInfo.Name, searchInfo.Year);
        var results = new List<RemoteSearchResult>();
        
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
                results.Add(searchResult);
                _logger.LogDebug("Série trouvée par ID Fankai {FankaiId}: {Title}", fankaiIdToSearch, serie.Title);
            }
        }
        else if (!string.IsNullOrWhiteSpace(searchInfo.Name))
        {
            var allSeries = await _apiClient.GetAllSeriesAsync(cancellationToken).ConfigureAwait(false);
            if (allSeries != null)
            {
                foreach (var serie in allSeries)
                {
                    bool nameMatch = serie.Title != null && searchInfo.Name != null &&
                                     serie.Title.Contains(searchInfo.Name, StringComparison.OrdinalIgnoreCase);
                    bool yearMatch = !searchInfo.Year.HasValue || serie.Year == searchInfo.Year.Value;

                    if (nameMatch && yearMatch)
                    {
                        var searchResult = new RemoteSearchResult
                        {
                            Name = serie.Title,
                            ProductionYear = serie.Year, 
                            Overview = serie.Plot,
                            ImageUrl = serie.PosterImageUrl
                        };
                        searchResult.ProviderIds.Add(ProviderIdName, serie.Id.ToString(CultureInfo.InvariantCulture));
                        results.Add(searchResult);
                    }
                }
                _logger.LogDebug("Trouvé {Count} séries correspondant à '{Name}' (Année: {Year}) par filtrage côté client.", results.Count, searchInfo.Name, searchInfo.Year);
            }
        }

        return results;
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
        var client = _httpClientFactory.CreateClient("FankaiSeriesImageClient");
        return client.GetAsync(new Uri(url), cancellationToken);
    }
}
