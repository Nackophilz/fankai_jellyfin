from __future__ import annotations

from dataclasses import dataclass
from typing import Any, Dict, List, Optional, Tuple

from lib import log
from lib.cache import Cache, Entry
from lib.config import TTL_SERIES_LIST, TTL_SHOW
from lib.http import ApiError, HttpClient, NotFound

KEY_SERIES_LIST = 'series_list'


def key_series(serie_id: Any) -> str:
    return 'series:{}'.format(serie_id)


def key_seasons(serie_id: Any) -> str:
    return 'seasons:{}'.format(serie_id)


def key_actors(serie_id: Any) -> str:
    return 'actors:{}'.format(serie_id)


def key_episodes(season_id: Any) -> str:
    return 'episodes:{}'.format(season_id)


@dataclass
class ShowBundle:
    series: Dict[str, Any]
    seasons: List[Dict[str, Any]]
    actors: List[Dict[str, Any]]

    @property
    def id(self) -> str:
        return str(self.series.get('id'))

    def season_by_id(self, season_id: Any) -> Optional[Dict[str, Any]]:
        for season in self.seasons:
            if str(season.get('id')) == str(season_id):
                return season
        return None


class FankaiApi:
    def __init__(self, http: HttpClient, cache: Cache):
        self._http = http
        self._cache = cache

    def _fetch(self, key: str, path: str, ttl: float, meta: Optional[Dict[str, Any]] = None,
               force: bool = False) -> Any:
        entry = self._cache.get(key)
        if entry is not None and not force and entry.is_fresh(self._cache.now()):
            return entry.data
        etag = entry.etag if entry is not None else None
        try:
            status, data, new_etag = self._http.get_json(path, etag=etag)
        except NotFound:
            self._cache.delete(key)
            raise
        except ApiError as exc:
            if entry is not None:
                log.warning('API injoignable ({}) : réutilisation du cache pour {}'.format(exc, key))
                return entry.data
            raise
        if status == 304 and entry is not None:
            log.debug('{} inchangé (304)'.format(key))
            self._cache.touch(key, ttl)
            return entry.data
        self._cache.put(key, data, ttl, etag=new_etag, meta=meta)
        return data

    def _cached(self, key: str) -> Optional[Entry]:
        return self._cache.get(key)

    def list_series(self) -> List[Dict[str, Any]]:
        data = self._fetch(KEY_SERIES_LIST, 'series', TTL_SERIES_LIST)
        if isinstance(data, dict):
            # Forme paginée si un jour `paginate=true` devenait le défaut.
            data = data.get('series') or []
        return [s for s in data if isinstance(s, dict) and s.get('id') is not None]

    def get_series(self, serie_id: Any, force: bool = False) -> Dict[str, Any]:
        return self._fetch(key_series(serie_id), 'series/{}'.format(serie_id), TTL_SHOW, force=force)

    def get_seasons(self, serie_id: Any, force: bool = False) -> List[Dict[str, Any]]:
        data = self._fetch(key_seasons(serie_id), 'series/{}/seasons'.format(serie_id), TTL_SHOW,
                           force=force)
        seasons = data.get('seasons') if isinstance(data, dict) else data
        return [s for s in (seasons or []) if isinstance(s, dict) and s.get('id') is not None]

    def get_actors(self, serie_id: Any, force: bool = False) -> List[Dict[str, Any]]:
        try:
            data = self._fetch(key_actors(serie_id), 'series/{}/actors'.format(serie_id), TTL_SHOW,
                               force=force)
        except NotFound:
            return []
        actors = data.get('actors') if isinstance(data, dict) else data
        return [a for a in (actors or []) if isinstance(a, dict) and a.get('name')]

    def get_episodes(self, season: Dict[str, Any]) -> Dict[str, Any]:
        """Enveloppe `/seasons/{id}/episodes` (avec `season_number` et `episodes`)."""
        season_id = season.get('id')
        key = key_episodes(season_id)
        meta = {'serie_id': season.get('serie_id'), 'last_update': season.get('last_update')}
        entry = self._cached(key)
        force = False
        if entry is not None and entry.meta and season.get('last_update') is not None \
                and entry.meta.get('last_update') != season.get('last_update'):
            log.debug('saison {} modifiée côté API : rechargement des épisodes'.format(season_id))
            force = True
        data = self._fetch(key, 'seasons/{}/episodes'.format(season_id), TTL_SHOW, meta=meta,
                           force=force)
        if not isinstance(data, dict):
            data = {'episodes': data or []}
        data.setdefault('episodes', [])
        return data

    def get_bundle(self, serie_id: Any) -> ShowBundle:
        """Série + saisons + acteurs, avec invalidation si la liste est plus récente."""
        self._invalidate_if_outdated(serie_id)
        series = self.get_series(serie_id)
        seasons = self.get_seasons(serie_id)
        actors = self.get_actors(serie_id)
        return ShowBundle(series, seasons, actors)

    def _invalidate_if_outdated(self, serie_id: Any) -> None:
        cached_series = self._cached(key_series(serie_id))
        listing = self._cached(KEY_SERIES_LIST)
        if cached_series is None or listing is None or not isinstance(listing.data, list):
            return
        fresh = next((s for s in listing.data if str(s.get('id')) == str(serie_id)), None)
        if fresh is None:
            return
        old_update = (cached_series.data or {}).get('last_update')
        new_update = fresh.get('last_update')
        if old_update is not None and new_update is not None and new_update > old_update:
            log.debug('série {} modifiée côté API : cache invalidé'.format(serie_id))
            for key in (key_series(serie_id), key_seasons(serie_id), key_actors(serie_id)):
                self._cache.delete(key)

    def find_episode(self, serie_id: Any, season_id: Any, episode_id: Any
                     ) -> Optional[Tuple[Dict[str, Any], Dict[str, Any], Dict[str, Any]]]:
        """(épisode, enveloppe d'épisodes, saison) ou None si introuvable."""
        seasons = self.get_seasons(serie_id)
        season = next((s for s in seasons if str(s.get('id')) == str(season_id)), None)
        if season is None:
            season = {'id': season_id, 'serie_id': serie_id}
        try:
            envelope = self.get_episodes(season)
        except NotFound:
            return None
        for episode in envelope.get('episodes', []):
            if str(episode.get('id')) == str(episode_id):
                return episode, envelope, season
        return None
