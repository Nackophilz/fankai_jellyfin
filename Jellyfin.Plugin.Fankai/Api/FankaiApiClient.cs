using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fankai.Configuration; 
using Jellyfin.Plugin.Fankai.Model;
using Microsoft.Extensions.Logging;

#if __EMBY__
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
using ILogger = MediaBrowser.Model.Logging.ILogger;
#else
using System.Net.Http;
using System.Net.Http.Headers;
#endif

namespace Jellyfin.Plugin.Fankai.Api;

/// <summary>
/// Client pour interagir avec l'API Fankai.
/// </summary>
public class FankaiApiClient
{
#if __EMBY__
    private readonly IHttpClient _httpClient;
    private readonly ILogger _logger;
#else
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FankaiApiClient> _logger;
#endif
    private readonly PluginConfiguration _configuration; 

    public const string FankaiApiBaseUrl = "https://metadata.fankai.fr";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

#if __EMBY__
    public FankaiApiClient(IHttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        
        if (Plugin.Instance == null)
        {
            _logger.Warn("Plugin.Instance est null dans le constructeur FankaiApiClient.");
        }
    }
#else
    public FankaiApiClient(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = loggerFactory?.CreateLogger<FankaiApiClient>() ?? throw new ArgumentNullException(nameof(loggerFactory));
        _configuration = Plugin.Instance?.Configuration ?? new PluginConfiguration(); 
        if (Plugin.Instance == null)
        {
            _logger.LogWarning("Plugin.Instance est null dans le constructeur FankaiApiClient. La clé d'API Fankai pourrait ne pas être chargée correctement si elle a été définie par l'utilisateur."); 
        }
    }
#endif

#if __EMBY__
    private async Task<T?> GetAsync<T>(string requestUri, CancellationToken cancellationToken) where T : class
    {
        var url = FankaiApiBaseUrl.TrimEnd('/') + "/" + requestUri;
        try
        {
            _logger.Debug("Fankai API Request: GET {0}", url);

            var options = new MediaBrowser.Common.Net.HttpRequestOptions
            {
                Url = url,
                CancellationToken = cancellationToken,
                AcceptHeader = "application/json"
            };

            if (!string.IsNullOrWhiteSpace(_configuration.FankaiApiKey))
            {
                options.RequestHeaders["X-API-Key"] = _configuration.FankaiApiKey;
            }

            using (var response = await _httpClient.GetResponse(options).ConfigureAwait(false))
            {
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    _logger.Error("Fankai API Erreur: Status={0}, Uri={1}", response.StatusCode, url);
                    return null;
                }

                return await JsonSerializer.DeserializeAsync<T>(response.Content, JsonOptions, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (JsonException ex) 
        {
            _logger.Error("Fankai API Erreur de désérialisation JSON pour {0}: {1}", url, ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.Error("Erreur inattendue dans le client Fankai API pour {0}: {1}", url, ex.Message);
            return null;
        }
    }
#else
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
#endif
    
    public Task<FankaiSerie?> GetSerieByIdAsync(string serieId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serieId))
        {
#if __EMBY__
            _logger.Warn("GetSerieByIdAsync: serieId est null ou vide.");
#else
            _logger.LogWarning("GetSerieByIdAsync: serieId est null ou vide.");
#endif
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
#if __EMBY__
            _logger.Warn("GetSeasonsForSerieAsync: serieId est null ou vide.");
#else
            _logger.LogWarning("GetSeasonsForSerieAsync: serieId est null ou vide.");
#endif
            return Task.FromResult<SerieSeasonsResponse?>(null);
        }
        return GetAsync<SerieSeasonsResponse>($"series/{serieId}/seasons", cancellationToken);
    }

    public Task<SeasonEpisodesResponse?> GetEpisodesForSeasonAsync(string seasonId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(seasonId))
        {
#if __EMBY__
            _logger.Warn("GetEpisodesForSeasonAsync: seasonId est null ou vide.");
#else
            _logger.LogWarning("GetEpisodesForSeasonAsync: seasonId est null ou vide.");
#endif
            return Task.FromResult<SeasonEpisodesResponse?>(null);
        }
        return GetAsync<SeasonEpisodesResponse>($"seasons/{seasonId}/episodes", cancellationToken);
    }
    
    public Task<SerieActorsResponse?> GetActorsForSerieAsync(string serieId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serieId))
        {
#if __EMBY__
            _logger.Warn("GetActorsForSerieAsync: serieId est null ou vide.");
#else
            _logger.LogWarning("GetActorsForSerieAsync: serieId est null ou vide.");
#endif
            return Task.FromResult<SerieActorsResponse?>(null);
        }
        return GetAsync<SerieActorsResponse>($"series/{serieId}/actors", cancellationToken);
    }
}
