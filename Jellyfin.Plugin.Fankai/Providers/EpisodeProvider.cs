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
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Fankai.Providers
{
    public class EpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
    {
        public string Name => "Fankai Episode Provider";

        private readonly ILogger<EpisodeProvider> _logger;
        private readonly FankaiApiClient _apiClient;
        private readonly IHttpClientFactory _httpClientFactory; 

        public const string ProviderIdName = "FankaiEpisodeId";

        public EpisodeProvider(
            IHttpClientFactory httpClientFactory,
            ILogger<EpisodeProvider> logger,
            ILoggerFactory loggerFactory)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiClient = new FankaiApiClient(httpClientFactory, loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory)));
        }
        
        private string NormalizeFilenameForComparison(string? filename)
        {
            if (string.IsNullOrWhiteSpace(filename)) return string.Empty;

            string normalized = filename.ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"(\[.*?\]|\(.*?\))", " "); 
            normalized = Regex.Replace(normalized, @"\b(1080p|720p|480p|multi|x264|x265|h264|h265|hevc|bdrip|dvdrip|webrip|webdl|vostfr|vf|truefrench|aac|dts|ac3|opus|flac|complete|uncut|bluray|hddvd|remux|hdr|sdr)\b", " ", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"[\s\.\-_']+", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
            _logger.LogTrace("Normalisation de '{OriginalFilename}' à '{NormalizedFilename}'", filename, normalized);
            return normalized;
        }

        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Fankai GetMetadata pour Episode: Nom='{EpisodeName}', Chemin='{Path}', JellyfinSeasonNum={SeasonNum}, JellyfinEpisodeNum={EpisodeNum}", 
                info.Name, 
                info.Path,
                info.ParentIndexNumber, 
                info.IndexNumber);
                
            var result = new MetadataResult<Episode>();
            
            if (string.IsNullOrWhiteSpace(info.Path))
            {
                _logger.LogWarning("EpisodeInfo.Path est null ou vide. Impossible de faire correspondre l'épisode.");
                return result;
            }

            string? fankaiSeriesId = info.SeriesProviderIds.GetValueOrDefault(SeriesProvider.ProviderIdName);
            if (string.IsNullOrWhiteSpace(fankaiSeriesId))
            {
                 _logger.LogWarning("Fankai Series ID n'est pas disponible dans EpisodeInfo pour Path: {Path}. Impossible d'identifier la série parente.", info.Path);
                return result;
            }

            _logger.LogDebug("Tentative de correspondance de l'épisode pour Fankai Series ID '{FankaiSeriesId}' en utilisant le chemin de fichier '{FilePath}'", fankaiSeriesId, info.Path);

            var seasonsResponse = await _apiClient.GetSeasonsForSerieAsync(fankaiSeriesId, cancellationToken).ConfigureAwait(false);
            if (seasonsResponse?.Seasons == null || !seasonsResponse.Seasons.Any())
            {
                _logger.LogWarning("Aucune saison trouvée dans l'API Fankai pour l'ID de série '{FankaiSeriesId}'.", fankaiSeriesId);
                return result;
            }

            FankaiEpisode? matchedFankaiEpisode = null;
            FankaiSeason? parentFankaiSeason = null;
            string jellyfinMediaFilename = Path.GetFileName(info.Path); 
            string normalizedJellyfinFilename = NormalizeFilenameForComparison(Path.GetFileNameWithoutExtension(info.Path));
            _logger.LogDebug("Nom de fichier Jellyfin normalisé pour la correspondance : '{NormalizedJellyfinFilename}' (depuis : '{OriginalJellyfinFile}')", normalizedJellyfinFilename, jellyfinMediaFilename);

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
                _logger.LogInformation("CORRESPONDANCE TROUVEE (NOM DE FICHIER): Fichier Jellyfin '{JellyfinFile}' correspond à l'épisode Fankai ID {FankaiEpisodeId}.", jellyfinMediaFilename, matchedFankaiEpisode.Id);
            }
            
            // Correspondance par numéro, si la première a échoué
            if (matchedFankaiEpisode == null && info.IndexNumber.HasValue && info.ParentIndexNumber.HasValue)
            {
                 _logger.LogDebug("Aucune correspondance par nom de fichier. Tentative de correspondance par numéro S{S}E{E}.", info.ParentIndexNumber, info.IndexNumber);
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
                              _logger.LogInformation("CORRESPONDANCE TROUVEE (NUMERO): Fichier Jellyfin S{S}E{E} correspond à l'épisode Fankai ID {FankaiEpisodeId}.", info.ParentIndexNumber, info.IndexNumber, matchedFankaiEpisode.Id);
                         }
                     }
                 }
            }

            // Vérifier si une correspondance a été trouvée
            if (matchedFankaiEpisode == null || parentFankaiSeason == null)
            {
                 _logger.LogWarning("AUCUNE CORRESPONDANCE TROUVÉE pour le fichier Jellyfin '{JellyfinFile}' dans la série ID '{FankaiSeriesId}'.", jellyfinMediaFilename, fankaiSeriesId);
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

            _logger.LogDebug("Application des métadonnées : Titre='{Title}', S={SeasonNum}, E={EpisodeNum}", matchedFankaiEpisode.Title, finalSeasonNumber, finalEpisodeNumber);

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
            _logger.LogInformation("Récupération OK des métadonnées pour l'épisode Fankai ID: {FankaiEpisodeId}, Titre: {Title}", matchedFankaiEpisode.Id, matchedFankaiEpisode.Title);
            return result;
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            _logger.LogInformation("La recherche d'épisode n'est pas supportée. Retour d'une liste vide.");
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
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
            var client = _httpClientFactory.CreateClient("FankaiEpisodeImageClient");
            return client.GetAsync(new Uri(url), cancellationToken);
        }
    }
}
