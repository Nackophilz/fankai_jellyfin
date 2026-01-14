using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
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
                    AddImageIfUrlValid(images, serieData.PosterImageUrl, MediaBrowser.Model.Entities.ImageType.Primary);
                    AddImageIfUrlValid(images, serieData.FanartImageUrl, MediaBrowser.Model.Entities.ImageType.Backdrop);
                    AddImageIfUrlValid(images, serieData.BannerImageUrl, MediaBrowser.Model.Entities.ImageType.Banner);
                    AddImageIfUrlValid(images, serieData.LogoImageUrl, MediaBrowser.Model.Entities.ImageType.Logo);
                    AddImageIfUrlValid(images, serieData.PosterImageUrl, MediaBrowser.Model.Entities.ImageType.Thumb);
                }
            }
        }
        else if (item is Season season)
        {
            var parentSeriesFankaiId = season.Series?.GetProviderId(SeriesProvider.ProviderIdName);
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
            }
            if (seasonData == null && season.IndexNumber.HasValue)
            {
                seasonData = seasonsResponse?.Seasons?.FirstOrDefault(s => s.SeasonNumber == season.IndexNumber.Value);
            }
            
            if (seasonData != null)
            {
                if (string.IsNullOrWhiteSpace(season.GetProviderId(FankaiSeasonIdProviderKey)))
                {
                     season.SetProviderId(FankaiSeasonIdProviderKey, seasonData.Id.ToString(CultureInfo.InvariantCulture));
                     LogDebug("Stockage de FankaiSeasonIdProviderKey {0} pour la Saison {1}", seasonData.Id, season.Name);
                }
                AddImageIfUrlValid(images, seasonData.PosterImageUrl, MediaBrowser.Model.Entities.ImageType.Primary);
                AddImageIfUrlValid(images, seasonData.FanartImageUrl, MediaBrowser.Model.Entities.ImageType.Backdrop);
            }
            else
            {
                LogWarn("Impossible de trouver les données de saison correspondantes pour la Saison {0} dans l'ID de Série {1}", season.IndexNumber, parentSeriesFankaiId);
            }
        }
        else if (item is Episode episode)
        {
            string? seasonFankaiIdToUse = episode.Season?.GetProviderId(FankaiSeasonIdProviderKey);

            if (string.IsNullOrWhiteSpace(seasonFankaiIdToUse))
            {
                var parentSeriesFankaiId = episode.Series?.GetProviderId(SeriesProvider.ProviderIdName);
                if (!string.IsNullOrWhiteSpace(parentSeriesFankaiId) && episode.ParentIndexNumber.HasValue)
                {
                    var seasonsResponse = await _apiClient.GetSeasonsForSerieAsync(parentSeriesFankaiId, cancellationToken).ConfigureAwait(false);
                    var foundSeason = seasonsResponse?.Seasons?.FirstOrDefault(s => s.SeasonNumber == episode.ParentIndexNumber.Value);
                    if (foundSeason != null)
                    {
                        seasonFankaiIdToUse = foundSeason.Id.ToString(CultureInfo.InvariantCulture);
                        episode.Season?.SetProviderId(FankaiSeasonIdProviderKey, seasonFankaiIdToUse);
                        LogDebug("ID de saison Fankai déduit et stocké {0} pour la saison parente de l'épisode {1}", seasonFankaiIdToUse, episode.Name);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(seasonFankaiIdToUse))
            {
                LogWarn("Impossible de déterminer l'ID Fankai de la saison parente pour l'épisode {0} (ID: {1})", episode.Name, episode.Id);
                return images;
            }
            
            var episodesResponse = await _apiClient.GetEpisodesForSeasonAsync(seasonFankaiIdToUse, cancellationToken).ConfigureAwait(false);
            Model.FankaiEpisode? episodeData = null;
            if(!string.IsNullOrWhiteSpace(fankaiSpecificId))
            {
                episodeData = episodesResponse?.Episodes?.FirstOrDefault(e => e.Id.ToString(CultureInfo.InvariantCulture) == fankaiSpecificId);
            }
            if (episodeData == null && episode.IndexNumber.HasValue)
            {
                 episodeData = episodesResponse?.Episodes?.FirstOrDefault(e => e.EpisodeNumber == episode.IndexNumber.Value);
            }

            if (episodeData != null)
            {
                 if (string.IsNullOrWhiteSpace(episode.GetProviderId(EpisodeProvider.ProviderIdName)))
                {
                     episode.SetProviderId(EpisodeProvider.ProviderIdName, episodeData.Id.ToString(CultureInfo.InvariantCulture));
                     LogDebug("ID de l'épisode Fankai stocké {0} pour l'épisode {1}", episodeData.Id, episode.Name);
                }
                AddImageIfUrlValid(images, episodeData.ThumbImageUrl, MediaBrowser.Model.Entities.ImageType.Primary);
            }
            else
            {
                 LogWarn("Impossible de trouver les données de l'épisode correspondant pour l'épisode {0} dans l'ID de saison {1}", episode.IndexNumber, seasonFankaiIdToUse);
            }
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
