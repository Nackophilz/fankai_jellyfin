using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO; 
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions; 
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Fankai.Api;
using Jellyfin.Plugin.Fankai.Model; 
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers; 
using MediaBrowser.Model.Providers; 
using MediaBrowser.Model.Entities;
#if __EMBY__
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Logging;
#endif
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fankai.Providers
{
    public class EpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
    {
        public string Name => "Fankai Episode Provider";

#if __EMBY__
        private readonly MediaBrowser.Model.Logging.ILogger _logger;
        private readonly IHttpClient _httpClient;
#else
        private readonly ILogger<EpisodeProvider> _logger;
        private readonly IHttpClientFactory _httpClientFactory; 
#endif
        private readonly FankaiApiClient _apiClient;

        public const string ProviderIdName = "FankaiEpisodeId";

#if __EMBY__
        public EpisodeProvider(IHttpClient httpClient, ILogManager logManager)
        {
             _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
             _logger = logManager.GetLogger(GetType().Name);
             _apiClient = new FankaiApiClient(httpClient, _logger);
        }
#else
        public EpisodeProvider(
            IHttpClientFactory httpClientFactory,
            ILogger<EpisodeProvider> logger,
            ILoggerFactory loggerFactory)
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
        
        private void LogTrace(string message, params object[] args)
        {
#if __EMBY__
             // Emby logger usually doesn't expose Trace easily, map to Debug
            _logger.Debug(message, args);
#else
            _logger.LogTrace(message, args);
#endif
        }

        private string NormalizeFilenameForComparison(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename)) return string.Empty;

            string normalized = filename.ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"(\[.*?\]|\(.*?\))", " "); 
            normalized = Regex.Replace(normalized, @"\b(1080p|720p|480p|multi|x264|x265|h264|h265|hevc|bdrip|dvdrip|webrip|webdl|vostfr|vf|truefrench|aac|dts|ac3|opus|flac|complete|uncut|bluray|hddvd|remux|hdr|sdr)\b", " ", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"[\s\.\-_']+", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            LogTrace("Normalisation de '{0}' à '{1}'", filename, normalized);
            return normalized;
        }

        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            LogInfo(
                "Fankai GetMetadata pour Episode: Nom='{0}', Chemin='{1}', JellyfinSeasonNum={2}, JellyfinEpisodeNum={3}", 
                info.Name, 
                info.Path,
                info.ParentIndexNumber, 
                info.IndexNumber);
                
            var result = new MetadataResult<Episode>();
            
            if (string.IsNullOrWhiteSpace(info.Path))
            {
                LogWarn("EpisodeInfo.Path est null ou vide. Impossible de faire correspondre l'épisode.");
                return result;
            }

            string? fankaiSeriesId = info.SeriesProviderIds.GetValueOrDefault(SeriesProvider.ProviderIdName);
            if (string.IsNullOrWhiteSpace(fankaiSeriesId))
            {
                 LogWarn("Fankai Series ID n'est pas disponible dans EpisodeInfo pour Path: {0}. Impossible d'identifier la série parente.", info.Path);
                return result;
            }

            LogDebug("Tentative de correspondance de l'épisode pour Fankai Series ID '{0}' en utilisant le chemin de fichier '{1}'", fankaiSeriesId, info.Path);

            var seasonsResponse = await _apiClient.GetSeasonsForSerieAsync(fankaiSeriesId, cancellationToken).ConfigureAwait(false);
            if (seasonsResponse?.Seasons == null || !seasonsResponse.Seasons.Any())
            {
                LogWarn("Aucune saison trouvée dans l'API Fankai pour l'ID de série '{0}'.", fankaiSeriesId);
                return result;
            }

            FankaiEpisode? matchedFankaiEpisode = null;
            FankaiSeason? parentFankaiSeason = null;
            string jellyfinMediaFilename = Path.GetFileName(info.Path); 
            string normalizedJellyfinFilename = NormalizeFilenameForComparison(Path.GetFileNameWithoutExtension(info.Path));
            LogDebug("Nom de fichier Jellyfin normalisé pour la correspondance : '{0}' (depuis : '{1}')", normalizedJellyfinFilename, jellyfinMediaFilename);

            var potentialMatches = new List<(FankaiEpisode episode, FankaiSeason season)>();

            // Correspondance par nom de fichier
            foreach (var fankaiSeason in seasonsResponse.Seasons)
            {
                var episodesResponse = await _apiClient.GetEpisodesForSeasonAsync(fankaiSeason.Id.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
                if (episodesResponse?.Episodes == null) continue;

                foreach (var apiEpisode in episodesResponse.Episodes)
                {
                    string normalizedApiOriginalFilename = NormalizeFilenameForComparison(Path.GetFileNameWithoutExtension(apiEpisode.OriginalFilename));
                    string normalizedApiNfoFilename = NormalizeFilenameForComparison(Path.GetFileNameWithoutExtension(apiEpisode.NfoFilename));

                    if ((!string.IsNullOrWhiteSpace(normalizedApiOriginalFilename) && normalizedApiOriginalFilename == normalizedJellyfinFilename)
                        || (!string.IsNullOrWhiteSpace(normalizedApiNfoFilename) && normalizedApiNfoFilename == normalizedJellyfinFilename))
                    {
                        potentialMatches.Add((apiEpisode, fankaiSeason));
                    }
                }
            }
            
            // Sélectionner la meilleure correspondance par nom de fichier
            if (potentialMatches.Any())
            {
                // Priorise les correspondances avec un 'episode_number' valide.
                var bestMatch = potentialMatches.FirstOrDefault(m => m.episode.EpisodeNumber.HasValue);
                
                // Si aucune correspondance n'a un 'episode_number', prendre la première trouvée.
                if (bestMatch.episode == null)
                {
                    bestMatch = potentialMatches.First();
                }
                
                matchedFankaiEpisode = bestMatch.episode;
                parentFankaiSeason = bestMatch.season;
                LogInfo("CORRESPONDANCE TROUVEE (NOM DE FICHIER): Fichier Jellyfin '{0}' correspond à l'épisode Fankai ID {1}.", jellyfinMediaFilename, matchedFankaiEpisode.Id);
            }
            
            // Correspondance par numéro, si la première a échoué
            if (matchedFankaiEpisode == null && info.IndexNumber.HasValue && info.ParentIndexNumber.HasValue)
            {
                 LogDebug("Aucune correspondance par nom de fichier. Tentative de correspondance par numéro S{0}E{1}.", info.ParentIndexNumber, info.IndexNumber);
                 var seasonByNumber = seasonsResponse.Seasons.FirstOrDefault(s => s.SeasonNumber == info.ParentIndexNumber.Value);
                 if (seasonByNumber != null)
                 {
                     var episodesResponse = await _apiClient.GetEpisodesForSeasonAsync(seasonByNumber.Id.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
                     if (episodesResponse?.Episodes != null)
                     {
                         // Prioriser la correspondance avec le numéro d'épisode puis le numéro d'affichage.
                         matchedFankaiEpisode = episodesResponse.Episodes
                            .FirstOrDefault(e => e.EpisodeNumber == info.IndexNumber.Value)
                            ?? episodesResponse.Episodes
                            .FirstOrDefault(e => int.TryParse(e.DisplayEpisode, out int dispEp) && dispEp == info.IndexNumber.Value);

                         if (matchedFankaiEpisode != null)
                         {
                              parentFankaiSeason = seasonByNumber;
                              LogInfo("CORRESPONDANCE TROUVEE (NUMERO): Fichier Jellyfin S{0}E{1} correspond à l'épisode Fankai ID {2}.", info.ParentIndexNumber, info.IndexNumber, matchedFankaiEpisode.Id);
                         }
                     }
                 }
            }

            // Vérifier si une correspondance a été trouvée
            if (matchedFankaiEpisode == null || parentFankaiSeason == null)
            {
                 LogWarn("AUCUNE CORRESPONDANCE TROUVÉE pour le fichier Jellyfin '{0}' dans la série ID '{1}'.", jellyfinMediaFilename, fankaiSeriesId);
                return result;
            }

            // Priorité à `episode_number`, fallback sur `display_episode`
            int? finalEpisodeNumber = matchedFankaiEpisode.EpisodeNumber;
            if (!finalEpisodeNumber.HasValue && !string.IsNullOrWhiteSpace(matchedFankaiEpisode.DisplayEpisode))
            {
                if (int.TryParse(matchedFankaiEpisode.DisplayEpisode, out int displayEpNum))
                {
                    finalEpisodeNumber = displayEpNum;
                }
            }

            // Priorité à `season_number` de l'objet saison parent, fallback sur `display_season` de l'objet épisode
            int? finalSeasonNumber = parentFankaiSeason.SeasonNumber;
            if (!finalSeasonNumber.HasValue && !string.IsNullOrWhiteSpace(matchedFankaiEpisode.DisplaySeason))
            {
                if (int.TryParse(matchedFankaiEpisode.DisplaySeason, out int displaySeasonNum))
                {
                    finalSeasonNumber = displaySeasonNum;
                }
            }

            LogDebug("Application des métadonnées : Titre='{0}', S={1}, E={2}", matchedFankaiEpisode.Title, finalSeasonNumber, finalEpisodeNumber);

            result.Item = new Episode
            {
                Name = matchedFankaiEpisode.Title,
                Overview = matchedFankaiEpisode.Plot,
                PremiereDate = TryParseDate(matchedFankaiEpisode.Aired), 
                IndexNumber = finalEpisodeNumber, 
                ParentIndexNumber = finalSeasonNumber, 
                OfficialRating = matchedFankaiEpisode.Mpaa,
                ProductionYear = info.Year 
            };
            
            result.Item.SetProviderId(ProviderIdName, matchedFankaiEpisode.Id.ToString(CultureInfo.InvariantCulture));

            if (!string.IsNullOrWhiteSpace(matchedFankaiEpisode.Studio))
            {
                result.Item.Studios = new[] { matchedFankaiEpisode.Studio };
            }
            
            result.HasMetadata = true;
            LogInfo("Récupération OK des métadonnées pour l'épisode Fankai ID: {0}, Titre: {1}", matchedFankaiEpisode.Id, matchedFankaiEpisode.Title);
            return result;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            LogInfo("La recherche d'épisode n'est pas supportée. Retour d'une liste vide.");
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
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
            var client = _httpClientFactory.CreateClient("FankaiEpisodeImageClient");
            return client.GetAsync(new Uri(url), cancellationToken);
        }
#endif
    }
}
