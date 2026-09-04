from __future__ import annotations

import os
import time
import traceback
from typing import Any, Callable, Dict, List, Optional

import xbmcgui
import xbmcplugin
import xbmcvfs

from lib import log, mapping
from lib.api import FankaiApi, ShowBundle
from lib.cache import Cache
from lib.config import ADDON_ID, Settings, addon, addon_version, load_settings
from lib.http import ApiError, HttpClient, NotFound
from lib.ids import (ShowRef, decode_episode_url, encode_episode_url, encode_show_url,
                     parse_show_ref, resolve_show_ref)
from lib.matching import exact_matches, is_unambiguous, search, series_year
from lib.nfo import parse_nfo

MAX_FIND_RESULTS = 10
_RESOLVED_ACTIONS = ('getdetails', 'getepisodedetails', 'getartwork')


def build_api(settings: Settings, now: Callable[[], float] = time.time) -> FankaiApi:
    profile = xbmcvfs.translatePath(addon().getAddonInfo('profile'))
    cache = Cache(os.path.join(profile, 'cache.db'), now=now)
    http = HttpClient(settings.base_url, user_agent='{}/{}'.format(ADDON_ID, addon_version()))
    return FankaiApi(http, cache)


def run_action(handle: int, action: Optional[str], params: Dict[str, str]) -> None:
    normalized = (action or '').lower()
    try:
        settings = load_settings(params.get('pathSettings'))
        log.init(settings.verbose_log)
        log.debug('action={} params={}'.format(action, {k: v for k, v in params.items() if k != 'nfo'}))
        api = build_api(settings)
        handlers = {
            'find': lambda: find(handle, api, params, settings),
            'nfourl': lambda: nfo_url(handle, api, params, settings),
            'getdetails': lambda: get_details(handle, api, params, settings),
            'getepisodelist': lambda: get_episode_list(handle, api, params, settings),
            'getepisodedetails': lambda: get_episode_details(handle, api, params, settings),
            'getartwork': lambda: get_artwork(handle, api, params, settings),
        }
        handler = handlers.get(normalized)
        if handler is None:
            log.info('action inconnue : {}'.format(action))
            xbmcplugin.endOfDirectory(handle)
            return
        handler()
    except Exception:  # pylint: disable=broad-except
        log.error('échec de l\'action {} :\n{}'.format(action, traceback.format_exc()))
        if normalized in _RESOLVED_ACTIONS:
            _fail(handle)
        else:
            xbmcplugin.endOfDirectory(handle)


def _fail(handle: int) -> None:
    xbmcplugin.setResolvedUrl(handle, False, xbmcgui.ListItem(offscreen=True))


def _label(series: Dict[str, Any]) -> str:
    title = str(series.get('title') or '')
    year = series_year(series)
    return '{} ({})'.format(title, year) if year else title


def _reidentify(api: FankaiApi, ref: ShowRef) -> Optional[str]:
    """Retrouve l'identifiant d'une série par titre exact (identifiant mort ou absent)."""
    if not ref.title:
        return None
    candidates = exact_matches(api.list_series(), ref.title)
    if len(candidates) > 1 and ref.year:
        filtered = [s for s in candidates if series_year(s) == ref.year]
        if filtered:
            candidates = filtered
    if len(candidates) != 1:
        log.warning('ré-identification impossible pour « {} » ({} candidat(s))'
                    .format(ref.title, len(candidates)))
        return None
    new_id = str(candidates[0].get('id'))
    log.warning('série « {} » : identifiant {} remplacé par {}'.format(ref.title, ref.fankai_id, new_id))
    return new_id


def _load_bundle(api: FankaiApi, ref: ShowRef) -> Optional[ShowBundle]:
    if ref.fankai_id:
        try:
            return api.get_bundle(ref.fankai_id)
        except NotFound:
            log.warning('série {} introuvable sur l\'API'.format(ref.fankai_id))
    new_id = _reidentify(api, ref)
    if new_id is None:
        return None
    try:
        return api.get_bundle(new_id)
    except NotFound:
        return None


def find(handle: int, api: FankaiApi, params: Dict[str, str], settings: Settings) -> None:
    title = params.get('title') or ''
    year = mapping.to_int(params.get('year'))
    log.debug('recherche « {} » ({})'.format(title, year or 'année inconnue'))
    try:
        series_list = api.list_series()
    except ApiError as exc:
        log.error('liste des séries indisponible : {}'.format(exc))
        xbmcplugin.endOfDirectory(handle)
        return
    matches = search(series_list, title, year)
    if is_unambiguous(matches):
        matches = matches[:1]
    else:
        matches = matches[:MAX_FIND_RESULTS]
    log.debug('{} résultat(s) pour « {} »'.format(len(matches), title))
    for match in matches:
        series = match.series
        li = xbmcgui.ListItem(_label(series), offscreen=True)
        mapping.fill_search_item(li, series)
        li.setProperty('relevance', str(match.relevance))
        xbmcplugin.addDirectoryItem(
            handle, url=encode_show_url(series.get('id'), series.get('title'), series_year(series)),
            listitem=li, isFolder=True)
    xbmcplugin.endOfDirectory(handle)


def nfo_url(handle: int, api: FankaiApi, params: Dict[str, str], settings: Settings) -> None:
    info = parse_nfo(params.get('nfo'))
    if info.kind != 'tvshow':
        xbmcplugin.endOfDirectory(handle)
        return
    series: Optional[Dict[str, Any]] = None
    fankai_id = info.fankai_id
    if fankai_id is None and info.is_fankai and info.title:
        try:
            candidates = exact_matches(api.list_series(), info.title)
        except ApiError as exc:
            log.error('liste des séries indisponible : {}'.format(exc))
            candidates = []
        if len(candidates) > 1 and info.year:
            candidates = [s for s in candidates if series_year(s) == info.year] or candidates
        if len(candidates) == 1:
            series = candidates[0]
            fankai_id = str(series.get('id'))
    if fankai_id is None:
        log.debug('NFO sans identifiant Fankai exploitable ({})'.format(info.title))
        xbmcplugin.endOfDirectory(handle)
        return
    title = (series or {}).get('title') or info.title
    year = series_year(series) if series else info.year
    li = xbmcgui.ListItem(str(title or ''), offscreen=True)
    li.getVideoInfoTag().setUniqueIDs({'fankai': fankai_id}, 'fankai')
    xbmcplugin.addDirectoryItem(handle, url=encode_show_url(fankai_id, title, year), listitem=li,
                                isFolder=True)
    xbmcplugin.endOfDirectory(handle)


def get_details(handle: int, api: FankaiApi, params: Dict[str, str], settings: Settings) -> None:
    ref = resolve_show_ref(params)
    if not ref:
        log.error('getdetails sans identifiant exploitable : {}'.format(params.get('url')))
        _fail(handle)
        return
    bundle = _load_bundle(api, ref)
    if bundle is None:
        _fail(handle)
        return
    li = xbmcgui.ListItem(str(bundle.series.get('title') or ''), offscreen=True)
    mapping.fill_show_tag(li.getVideoInfoTag(), bundle.series, bundle.seasons, bundle.actors, settings)
    mapping.add_show_art(li, bundle.series, bundle.seasons)
    xbmcplugin.setResolvedUrl(handle, True, li)


def get_episode_list(handle: int, api: FankaiApi, params: Dict[str, str], settings: Settings) -> None:
    ref = parse_show_ref(params.get('url'))
    if not ref:
        log.error('guide des épisodes sans identifiant Fankai ({!r}) : un tvshow.nfo avec '
                  '<episodeguide>{{}}</episodeguide> écrase celui du scraper, ajoutez-y '
                  '<uniqueid type="fankai">ID</uniqueid> ou retirez la balise'.format(params.get('url')))
        xbmcplugin.endOfDirectory(handle)
        return
    bundle = _load_bundle(api, ref)
    if bundle is None:
        xbmcplugin.endOfDirectory(handle)
        return
    seasons = sorted(bundle.seasons, key=lambda s: mapping.to_int(s.get('season_number')) or 0)
    count = 0
    for season in seasons:
        try:
            envelope = api.get_episodes(season)
        except NotFound:
            log.warning('saison {} introuvable sur l\'API'.format(season.get('id')))
            continue
        for episode in envelope.get('episodes', []):
            numbers = mapping.episode_numbers(episode, envelope, season)
            if numbers is None:
                log.warning('épisode {} ignoré : aucun numéro exploitable'.format(episode.get('id')))
                continue
            li = xbmcgui.ListItem(str(episode.get('title') or ''), offscreen=True)
            mapping.fill_episode_list_item(li, episode, numbers)
            xbmcplugin.addDirectoryItem(
                handle, url=encode_episode_url(bundle.id, season.get('id'), episode.get('id')),
                listitem=li, isFolder=False)
            count += 1
    log.debug('{} épisode(s) listé(s) pour la série {}'.format(count, bundle.id))
    xbmcplugin.endOfDirectory(handle)


def get_episode_details(handle: int, api: FankaiApi, params: Dict[str, str], settings: Settings) -> None:
    decoded = decode_episode_url(params.get('url'))
    if decoded is None:
        log.error('identifiant d\'épisode invalide : {!r}'.format(params.get('url')))
        _fail(handle)
        return
    serie_id, season_id, episode_id = decoded
    found = api.find_episode(serie_id, season_id, episode_id)
    if found is None:
        log.warning('épisode {} introuvable (série {}, saison {})'.format(episode_id, serie_id, season_id))
        _fail(handle)
        return
    episode, envelope, season = found
    numbers = mapping.episode_numbers(episode, envelope, season)
    if numbers is None:
        _fail(handle)
        return
    li = xbmcgui.ListItem(str(episode.get('title') or ''), offscreen=True)
    mapping.fill_episode_tag(li.getVideoInfoTag(), episode, numbers)
    xbmcplugin.setResolvedUrl(handle, True, li)


def get_artwork(handle: int, api: FankaiApi, params: Dict[str, str], settings: Settings) -> None:
    ref = parse_show_ref(params.get('id'))
    if not ref:
        ref = resolve_show_ref(params)
    if not ref:
        log.error('getartwork sans identifiant')
        _fail(handle)
        return
    bundle = _load_bundle(api, ref)
    if bundle is None:
        _fail(handle)
        return
    li = xbmcgui.ListItem(str(bundle.series.get('title') or ''), offscreen=True)
    mapping.add_show_art(li, bundle.series, bundle.seasons)
    xbmcplugin.setResolvedUrl(handle, True, li)
