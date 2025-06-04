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
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fankai.Providers;

public class FankaiImageProvider : IRemoteImageProvider
{
    public string Name => "Fankai Image Provider";

    private readonly ILogger<FankaiImageProvider> _logger;
    private readonly FankaiApiClient _apiClient;
    private readonly IHttpClientFactory _httpClientFactory;

    public const string FankaiSeasonIdProviderKey = "FankaiSeasonId"; // Clé pour stocker/récupérer l'ID de saison Fankai

    public FankaiImageProvider(IHttpClientFactory httpClientFactory, ILogger<FankaiImageProvider> logger, ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _apiClient = new FankaiApiClient(httpClientFactory, loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)));
    }

    public bool Supports(BaseItem item)
    {
        return item is Series || item is Season || item is Episode;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        if (item is Series)
        {
            return new List<ImageType>
            {
                ImageType.Primary,
                ImageType.Backdrop,
                ImageType.Banner,
                ImageType.Logo,
                ImageType.Thumb
            };
        }
        if (item is Season)
        {
            return new List<ImageType>
            {
                ImageType.Primary,
                ImageType.Backdrop
            };
        }
        if (item is Episode)
        {
            return new List<ImageType>
            {
                ImageType.Primary
            };
        }
        return Enumerable.Empty<ImageType>();
    }

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var images = new List<RemoteImageInfo>();
        string? fankaiSpecificId = null; // ID Fankai pour l'item actuel (série, saison ou épisode)

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

        _logger.LogDebug("Tentative de récupération des images pour l'objet: {ItemName} (ID: {ItemId}), Type: {ItemType}, ID Fankai direct (si disponible): {FankaiSpecificId}",
            item.Name, item.Id, item.GetType().Name, fankaiSpecificId);

        if (item is Series series)
        {
            var currentFankaiSeriesId = series.GetProviderId(SeriesProvider.ProviderIdName);
            if (!string.IsNullOrWhiteSpace(currentFankaiSeriesId))
            {
                var serieData = await _apiClient.GetSerieByIdAsync(currentFankaiSeriesId, cancellationToken).ConfigureAwait(false);
                if (serieData != null)
                {
                    AddImageIfUrlValid(images, serieData.PosterImageUrl, ImageType.Primary);
                    AddImageIfUrlValid(images, serieData.FanartImageUrl, ImageType.Backdrop);
                    AddImageIfUrlValid(images, serieData.BannerImageUrl, ImageType.Banner);
                    AddImageIfUrlValid(images, serieData.LogoImageUrl, ImageType.Logo);
                    AddImageIfUrlValid(images, serieData.PosterImageUrl, ImageType.Thumb);
                }
            }
        }
        else if (item is Season season)
        {
            var parentSeriesFankaiId = season.Series?.GetProviderId(SeriesProvider.ProviderIdName);
            if (string.IsNullOrWhiteSpace(parentSeriesFankaiId))
            {
                _logger.LogWarning("Impossible de trouver l'ID Fankai de la série parente pour la saison {SeasonName} (ID: {SeasonId})", season.Name, season.Id);
                return images;
            }

            var seasonsResponse = await _apiClient.GetSeasonsForSerieAsync(parentSeriesFankaiId, cancellationToken).ConfigureAwait(false);
            Model.FankaiSeason? seasonData = null;
            if (!string.IsNullOrWhiteSpace(fankaiSpecificId)) // fankaiSpecificId est l'ID de la saison Fankai
            {
                 seasonData = seasonsResponse?.Seasons?.FirstOrDefault(s => s.Id.ToString(CultureInfo.InvariantCulture) == fankaiSpecificId);
            }
            if (seasonData == null && season.IndexNumber.HasValue) // Fallback: chercher par numéro de saison
            {
                seasonData = seasonsResponse?.Seasons?.FirstOrDefault(s => s.SeasonNumber == season.IndexNumber.Value);
            }
            
            if (seasonData != null)
            {
                // Stocker l'ID Fankai de la saison si ce n'est pas déjà fait et qu'on l'a trouvé par numéro
                if (string.IsNullOrWhiteSpace(season.GetProviderId(FankaiSeasonIdProviderKey)))
                {
                     season.SetProviderId(FankaiSeasonIdProviderKey, seasonData.Id.ToString(CultureInfo.InvariantCulture));
                     _logger.LogDebug("Stockage de FankaiSeasonIdProviderKey {FankaiSeasonId} pour la Saison {SeasonName}", seasonData.Id, season.Name);
                }
                AddImageIfUrlValid(images, seasonData.PosterImageUrl, ImageType.Primary);
                AddImageIfUrlValid(images, seasonData.FanartImageUrl, ImageType.Backdrop);
            }
            else
            {
                _logger.LogWarning("Impossible de trouver les données de saison correspondantes pour la Saison {IndexNumber} dans l'ID de Série {ParentSeriesFankaiId}", season.IndexNumber, parentSeriesFankaiId);
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
                        _logger.LogDebug("ID de saison Fankai déduit et stocké {SeasonFankaiId} pour la saison parente de l'épisode {EpisodeName}", seasonFankaiIdToUse, episode.Name);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(seasonFankaiIdToUse))
            {
                _logger.LogWarning("Impossible de déterminer l'ID Fankai de la saison parente pour l'épisode {EpisodeName} (ID: {EpisodeId})", episode.Name, episode.Id);
                return images;
            }
            
            var episodesResponse = await _apiClient.GetEpisodesForSeasonAsync(seasonFankaiIdToUse, cancellationToken).ConfigureAwait(false);
            Model.FankaiEpisode? episodeData = null;
            if(!string.IsNullOrWhiteSpace(fankaiSpecificId)) // fankaiSpecificId est l'ID de l'épisode Fankai
            {
                episodeData = episodesResponse?.Episodes?.FirstOrDefault(e => e.Id.ToString(CultureInfo.InvariantCulture) == fankaiSpecificId);
            }
            if (episodeData == null && episode.IndexNumber.HasValue) // Fallback: chercher par numéro d'épisode
            {
                 episodeData = episodesResponse?.Episodes?.FirstOrDefault(e => e.EpisodeNumber == episode.IndexNumber.Value);
            }

            if (episodeData != null)
            {
                // Stocker l'ID Fankai de l'épisode si ce n'est pas déjà fait et qu'on l'a trouvé par numéro
                 if (string.IsNullOrWhiteSpace(episode.GetProviderId(EpisodeProvider.ProviderIdName)))
                {
                     episode.SetProviderId(EpisodeProvider.ProviderIdName, episodeData.Id.ToString(CultureInfo.InvariantCulture));
                     _logger.LogDebug("ID de l'épisode Fankai stocké {FankaiEpisodeId} pour l'épisode {EpisodeName}", episodeData.Id, episode.Name);
                }
                AddImageIfUrlValid(images, episodeData.ThumbImageUrl, ImageType.Primary);
            }
            else
            {
                 _logger.LogWarning("Impossible de trouver les données de l'épisode correspondant pour l'épisode {IndexNumber} dans l'ID de saison {SeasonFankaiId}", episode.IndexNumber, seasonFankaiIdToUse);
            }
        }

        _logger.LogInformation("Trouvé {Count} images distantes pour l'objet {ItemName}", images.Count, item.Name);
        return images;
    }

    private void AddImageIfUrlValid(List<RemoteImageInfo> images, string? imageUrl, ImageType type)
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
    
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("FankaiImageClient");
        return client.GetAsync(new Uri(url), cancellationToken);
    }
}
