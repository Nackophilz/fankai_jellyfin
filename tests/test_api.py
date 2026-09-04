import pytest
from conftest import FakeHttp, load_fixture

from lib.config import TTL_SERIES_LIST, TTL_SHOW
from lib.http import ApiError, NotFound


def test_list_series_mise_en_cache_puis_revalidee(api, fake_http, clock):
    fake_http.etags['series'] = '"v1"'
    assert len(api.list_series()) == 12
    assert len(api.list_series()) == 12
    assert fake_http.count('series') == 1

    clock.advance(TTL_SERIES_LIST + 1)
    fake_http.overrides['series'] = FakeHttp.NOT_MODIFIED
    assert len(api.list_series()) == 12
    assert fake_http.calls[-1] == ('series', '"v1"')
    # Le 304 a prolongé l'entrée : plus d'appel réseau ensuite.
    api.list_series()
    assert fake_http.count('series') == 2


def test_get_bundle_trois_appels_puis_cache(api, fake_http):
    bundle = api.get_bundle(53)
    assert bundle.id == '53'
    assert len(bundle.seasons) == 13
    assert bundle.actors and all(a['name'] for a in bundle.actors)
    assert fake_http.count() == 3
    api.get_bundle('53')
    assert fake_http.count() == 3
    assert bundle.season_by_id(110)['season_number'] == 0
    assert bundle.season_by_id(1) is None


def test_actors_absents_ne_bloquent_pas(api, fake_http):
    fake_http.overrides['series/71/seasons'] = {'serie_title': 'x', 'seasons_count': 0, 'seasons': []}
    bundle = api.get_bundle(71)
    assert bundle.actors == []
    assert bundle.series['title'].startswith('Hunter x Hunter')


def test_not_found_purge_le_cache(api, fake_http, cache):
    api.get_series(53)
    fake_http.overrides['series/53'] = NotFound('mort')
    cache.delete('series:53')
    with pytest.raises(NotFound):
        api.get_series(53)
    assert cache.get('series:53') is None


def test_stale_if_error(api, fake_http, clock):
    api.get_series(53)
    clock.advance(TTL_SHOW + 1)
    fake_http.overrides['series/53'] = ApiError('panne')
    assert api.get_series(53)['id'] == 53
    fake_http.overrides.clear()
    fake_http.overrides['series/85'] = ApiError('panne')
    with pytest.raises(ApiError):
        api.get_series(85)


def test_invalidation_quand_la_liste_est_plus_recente(api, fake_http, clock):
    api.get_bundle(53)
    api.list_series()
    assert fake_http.count() == 4
    # Nouvelle liste avec un last_update plus récent pour la série 53.
    listing = load_fixture('series_list.json')
    for s in listing:
        if s['id'] == 53:
            s['last_update'] = s['last_update'] + 1000
    clock.advance(TTL_SERIES_LIST + 1)
    fake_http.overrides['series'] = listing
    api.list_series()
    api.get_bundle(53)
    assert fake_http.count('series/53') == 2
    assert fake_http.count('series/53/seasons') == 2


def test_get_episodes_recharge_si_saison_modifiee(api, fake_http):
    season = api.get_seasons(53)[0]
    assert season['id'] == 110
    envelope = api.get_episodes(season)
    assert envelope['season_number'] == 0
    assert len(envelope['episodes']) == 5
    api.get_episodes(season)
    assert fake_http.count('seasons/110/episodes') == 1
    api.get_episodes(dict(season, last_update=(season.get('last_update') or 0) + 1))
    assert fake_http.count('seasons/110/episodes') == 2


def test_find_episode(api, fake_http):
    found = api.find_episode(53, 110, 714)
    assert found is not None
    episode, envelope, season = found
    assert episode['title'] == 'Strong World'
    assert envelope['season_number'] == 0
    assert season['id'] == 110
    assert api.find_episode(53, 110, 1) is None
    assert api.find_episode(53, 9999, 1) is None
