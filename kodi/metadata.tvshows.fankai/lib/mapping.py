from __future__ import annotations

import re
from typing import Any, Dict, List, Optional, Tuple

import xbmc

from lib import log
from lib.config import EDIT_TYPES, UNIQUE_ID_TYPE, Settings
from lib.ids import encode_show_url
from lib.matching import normalize

_RE_MULTI_NEWLINES = re.compile(r'\n{3,}')


def clean_plot(text: Optional[str]) -> str:
    if not text:
        return ''
    text = text.replace('\r\n', '\n').replace('\r', '\n')
    text = '\n'.join(line.rstrip() for line in text.split('\n'))
    return _RE_MULTI_NEWLINES.sub('\n\n', text).strip()


def split_genres(value: Any) -> List[str]:
    if isinstance(value, list):
        items = [str(v) for v in value]
    else:
        items = str(value or '').split(',')
    return [g.strip() for g in items if g and g.strip()]


def edit_type_tag(genres: List[str]) -> Optional[str]:
    """Type d'édition Fan-Kai (Kaï, Henshū…) s'il ouvre la liste des genres."""
    if genres and normalize(genres[0]) in EDIT_TYPES:
        return genres[0]
    return None


def to_int(value: Any) -> Optional[int]:
    if value is None or isinstance(value, bool):
        return None
    if isinstance(value, int):
        return value
    try:
        text = str(value).strip()
        return int(text) if text else None
    except ValueError:
        return None


def year_of(date_text: Any, fallback: Any = None) -> Optional[int]:
    year = to_int(fallback)
    if year:
        return year
    text = str(date_text or '')
    return to_int(text[:4]) if len(text) >= 4 and text[:4].isdigit() else None


def episode_numbers(episode: Dict[str, Any], envelope: Dict[str, Any], season: Dict[str, Any]
                    ) -> Optional[Tuple[int, int, Optional[int], Optional[int]]]:
    """(saison, épisode, saison de tri, épisode de tri) ou None si non numérotable.
    """
    season_no = to_int(episode.get('season_number'))
    if season_no is None:
        season_no = to_int(envelope.get('season_number'))
    if season_no is None:
        season_no = to_int(season.get('season_number'))
    display_season = to_int(episode.get('display_season'))
    display_episode = to_int(episode.get('display_episode'))
    if season_no is None:
        season_no = display_season
    episode_no = to_int(episode.get('episode_number'))
    if episode_no is None:
        episode_no = display_episode
    if season_no is None or episode_no is None or season_no < 0 or episode_no < 0:
        return None
    sort_season = sort_episode = None
    if display_season is not None and display_episode is not None and display_season > 0 \
            and (season_no == 0 or (display_season, display_episode) != (season_no, episode_no)):
        sort_season, sort_episode = display_season, display_episode
    return season_no, episode_no, sort_season, sort_episode


KAIEUR_ROLE = 'kaieur'


def order_cast(actors: List[Dict[str, Any]]) -> List[Dict[str, Any]]:
    """Le Kaïeur (auteur du montage) en tête, le reste dans l'ordre de l'API."""
    return sorted(actors, key=lambda a: 0 if normalize(a.get('role')) == KAIEUR_ROLE else 1)


def make_actor(actor: Dict[str, Any], order: int) -> xbmc.Actor:
    return xbmc.Actor(str(actor.get('name') or ''), str(actor.get('role') or ''), order,
                      str(actor.get('thumb_url') or ''))


def unique_ids(series: Dict[str, Any], fankai_id: Any) -> Dict[str, str]:
    ids = {UNIQUE_ID_TYPE: str(fankai_id)}
    for name, value in (series.get('ids') or {}).items():
        if value not in (None, '', 'NULL', 'null'):
            ids[str(name)] = str(value)
    return ids


def set_rating(tag: Any, item: Dict[str, Any]) -> None:
    value = item.get('rating_value')
    if value is None and isinstance(item.get('rating'), dict):
        value = item['rating'].get('value')
    try:
        rating = float(value) if value not in (None, '') else None
    except (TypeError, ValueError):
        rating = None
    if rating is None or rating <= 0:
        return
    votes = to_int(item.get('rating_votes'))
    if votes is None and isinstance(item.get('rating'), dict):
        votes = to_int(item['rating'].get('votes'))
    tag.setRating(rating, votes or 0, UNIQUE_ID_TYPE, True)


def fill_search_item(li: Any, series: Dict[str, Any]) -> None:
    tag = li.getVideoInfoTag()
    tag.setTitle(str(series.get('title') or ''))
    if series.get('original_title'):
        tag.setOriginalTitle(str(series['original_title']))
    if series.get('plot'):
        tag.setPlot(clean_plot(series['plot']))
    if series.get('premiered'):
        tag.setPremiered(str(series['premiered']))
    year = year_of(series.get('premiered'), series.get('year'))
    if year:
        tag.setYear(year)
    tag.setMediaType('tvshow')
    tag.setUniqueIDs(unique_ids(series, series.get('id')), UNIQUE_ID_TYPE)
    poster = (series.get('images') or {}).get('poster') or series.get('poster_image')
    if poster:
        tag.addAvailableArtwork(poster, 'poster', preview=poster)


def fill_show_tag(tag: Any, series: Dict[str, Any], seasons: List[Dict[str, Any]],
                  actors: List[Dict[str, Any]], settings: Settings) -> None:
    title = str(series.get('title') or '')
    original = str(series.get('original_title') or '')
    if settings.use_original_title and original:
        title = original
    tag.setTitle(title)
    if original:
        tag.setOriginalTitle(original)
    if series.get('sort_title'):
        tag.setSortTitle(str(series['sort_title']))

    plot = clean_plot(series.get('plot'))
    if plot:
        tag.setPlot(plot)
        tag.setPlotOutline(plot)
    if series.get('tagline'):
        tag.setTagLine(str(series['tagline']))
    if series.get('premiered'):
        tag.setPremiered(str(series['premiered']))
    year = year_of(series.get('premiered'), series.get('year'))
    if year:
        tag.setYear(year)
    if series.get('status'):
        tag.setTvShowStatus(str(series['status']))
    tag.setMediaType('tvshow')

    genres = split_genres(series.get('genres'))
    if genres:
        tag.setGenres(genres)
    if settings.edit_type_as_tag:
        edit_type = edit_type_tag(genres)
        if edit_type:
            tag.setTags([edit_type])
    if series.get('studio'):
        tag.setStudios([str(series['studio'])])
    if series.get('country'):
        tag.setCountries([str(series['country'])])
    if series.get('mpaa'):
        tag.setMpaa(str(series['mpaa']))
    if settings.enable_trailer and series.get('trailer_url'):
        tag.setTrailer(str(series['trailer_url']))
    set_rating(tag, series)

    fankai_id = series.get('id')
    tag.setUniqueIDs(unique_ids(series, fankai_id), UNIQUE_ID_TYPE)
    tag.setEpisodeGuide(encode_show_url(fankai_id, series.get('title'), year))

    cast = [make_actor(a, i) for i, a in enumerate(order_cast(actors)) if a.get('name')]
    if cast:
        tag.setCast(cast)

    if settings.api_season_names:
        for season in seasons:
            number = to_int(season.get('season_number'))
            if number is None:
                continue
            name = str(season.get('title') or '')
            plot = clean_plot(season.get('plot'))
            try:
                tag.addSeason(number, name, plot)
            except TypeError:
                # Kodi < 22 : pas de paramètre `plot`.
                tag.addSeason(number, name)


def add_show_art(li: Any, series: Dict[str, Any], seasons: List[Dict[str, Any]]) -> None:
    tag = li.getVideoInfoTag()
    images = series.get('images') or {}
    for api_type, kodi_type in (('poster', 'poster'), ('banner', 'banner'), ('logo', 'clearlogo')):
        url = images.get(api_type) or series.get('{}_image'.format(api_type))
        if url:
            tag.addAvailableArtwork(url, kodi_type, preview=url)

    fanart = images.get('fanart') or series.get('fanart_image')
    if fanart:
        entries = [{'image': fanart, 'preview': fanart}]
        try:
            tag.setAvailableFanart(entries)
        except AttributeError:
            li.setAvailableFanart(entries)

    for season in seasons:
        number = to_int(season.get('season_number'))
        if number is None:
            continue
        season_images = season.get('images') or {}
        for api_type, kodi_type in (('poster', 'poster'), ('fanart', 'fanart')):
            url = season_images.get(api_type) or season.get('{}_image'.format(api_type))
            if url:
                tag.addAvailableArtwork(url, kodi_type, preview=url, season=number)


def fill_episode_list_item(li: Any, episode: Dict[str, Any], numbers: Tuple[int, int, Optional[int], Optional[int]]
                           ) -> None:
    season_no, episode_no, sort_season, sort_episode = numbers
    tag = li.getVideoInfoTag()
    tag.setTitle(str(episode.get('title') or 'Épisode {}'.format(episode_no)))
    tag.setSeason(season_no)
    tag.setEpisode(episode_no)
    if sort_season is not None and sort_episode is not None:
        tag.setSortSeason(sort_season)
        tag.setSortEpisode(sort_episode)
    if episode.get('aired'):
        tag.setFirstAired(str(episode['aired']))
    tag.setMediaType('episode')


def fill_episode_tag(tag: Any, episode: Dict[str, Any], numbers: Tuple[int, int, Optional[int], Optional[int]]
                     ) -> None:
    season_no, episode_no, sort_season, sort_episode = numbers
    tag.setTitle(str(episode.get('title') or 'Épisode {}'.format(episode_no)))
    tag.setSeason(season_no)
    tag.setEpisode(episode_no)
    if sort_season is not None and sort_episode is not None:
        tag.setSortSeason(sort_season)
        tag.setSortEpisode(sort_episode)
    plot = clean_plot(episode.get('plot'))
    if plot:
        tag.setPlot(plot)
        tag.setPlotOutline(plot)
    aired = episode.get('aired')
    if aired:
        tag.setFirstAired(str(aired))
        tag.setPremiered(str(aired))
        year = year_of(aired)
        if year:
            tag.setYear(year)
    duration = to_int(episode.get('duration'))
    if duration and duration > 0:
        tag.setDuration(duration)
    if episode.get('mpaa'):
        tag.setMpaa(str(episode['mpaa']))
    if episode.get('studio'):
        tag.setStudios([str(episode['studio'])])
    set_rating(tag, episode)
    tag.setMediaType('episode')
    tag.setUniqueIDs({UNIQUE_ID_TYPE: str(episode.get('id'))}, UNIQUE_ID_TYPE)
    thumb = episode.get('thumb_image')
    if thumb:
        tag.addAvailableArtwork(thumb, 'thumb', preview=thumb)
    else:
        log.debug('épisode {} sans vignette'.format(episode.get('id')))
