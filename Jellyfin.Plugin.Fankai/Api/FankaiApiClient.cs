using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fankai.Configuration; 
using Jellyfin.Plugin.Fankai.Model;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fankai.Api;

/// <summary>
/// Client pour interagir avec l'API Fankai.
/// </summary>
public class FankaiApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FankaiApiClient> _logger;
    private readonly PluginConfiguration _configuration; 


    public const string FankaiApiBaseUrl = "https://metadata.fankai.fr";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public FankaiApiClient(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = loggerFactory?.CreateLogger<FankaiApiClient>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        _configuration = Plugin.Instance?.Configuration ?? new PluginConfiguration(); 
        if (Plugin.Instance == null)
        {
            _logger.LogWarning("Plugin.Instance est null dans le constructeur FankaiApiClient. La clé d'API Fankai pourrait ne pas être chargée correctement si elle a été définie par l'utilisateur."); // Voir implémentation de Clef API peut eetre
        }
    }

    private HttpClient GetHttpClient()
    {
        var client = _httpClientFactory.CreateClient("FankaiApiClient");
        
        client.BaseAddress = new Uri(FankaiApiBaseUrl.TrimEnd('/') + "/"); 

        if (!string.IsNullOrWhiteSpace(_configuration.FankaiApiKey))
        {
            client.DefaultRequestHeaders.Remove("X-API-Key"); 
            client.DefaultRequestHeaders.Add("X-API-Key", _configuration.FankaiApiKey);
        }
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private async Task<T?> GetAsync<T>(string requestUri, CancellationToken cancellationToken) where T : class
    {
        HttpClient httpClient = null!;
        try
        {
            httpClient = GetHttpClient(); 
            _logger.LogDebug("Fankai API Request: GET {BaseAddress}{RequestUri}", httpClient.BaseAddress, requestUri);

            var response = await httpClient.GetAsync(requestUri, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError("Fankai API Erreur: Status={StatusCode}, Uri={RequestUri}, Response={ErrorContent}", response.StatusCode, requestUri, errorContent);
                return null;
            }

            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(contentStream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) 
        {
            _logger.LogError(ex, "Fankai API Client Erreur de config: {Message}", ex.Message);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Fankai API Erreur de requête HTTP pour {RequestUri}: {Message}", requestUri, ex.Message);
            return null;
        }
        catch (JsonException ex) 
        {
            _logger.LogError(ex, "Fankai API Erreur de désérialisation JSON pour {RequestUri}: {Message}", requestUri, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur inattendue dans le client Fankai API pour {RequestUri}: {Message}", requestUri, ex.Message);
            return null;
        }
    }
    
    public Task<FankaiSerie?> GetSerieByIdAsync(string serieId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serieId))
        {
            _logger.LogWarning("GetSerieByIdAsync: serieId est null ou vide.");
            return Task.FromResult<FankaiSerie?>(null);
        }
        return GetAsync<FankaiSerie>($"series/{serieId}", cancellationToken);
    }

    public Task<List<FankaiSerie>?> GetAllSeriesAsync(CancellationToken cancellationToken)
    {
        return GetAsync<List<FankaiSerie>>("series?paginate=false", cancellationToken);
    }
    
    public Task<SerieSeasonsResponse?> GetSeasonsForSerieAsync(string serieId, CancellationToken cancellationToken)
    {
         if (string.IsNullOrWhiteSpace(serieId))
        {
            _logger.LogWarning("GetSeasonsForSerieAsync: serieId est null ou vide.");
            return Task.FromResult<SerieSeasonsResponse?>(null);
        }
        return GetAsync<SerieSeasonsResponse>($"series/{serieId}/seasons", cancellationToken);
    }

    public Task<SeasonEpisodesResponse?> GetEpisodesForSeasonAsync(string seasonId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(seasonId))
        {
            _logger.LogWarning("GetEpisodesForSeasonAsync: seasonId est null ou vide.");
            return Task.FromResult<SeasonEpisodesResponse?>(null);
        }
        return GetAsync<SeasonEpisodesResponse>($"seasons/{seasonId}/episodes", cancellationToken);
    }
    
    public Task<SerieActorsResponse?> GetActorsForSerieAsync(string serieId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serieId))
        {
            _logger.LogWarning("GetActorsForSerieAsync: serieId est null ou vide.");
            return Task.FromResult<SerieActorsResponse?>(null);
        }
        return GetAsync<SerieActorsResponse>($"series/{serieId}/actors", cancellationToken);
    }
}
