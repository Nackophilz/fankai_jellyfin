import json

import pytest
import xbmc
import xbmcaddon
import xbmcplugin
from conftest import load_fixture, logged

from lib import scraper
from lib.ids import encode_show_url
from lib.scraper import run_action
from test_nfo import EPISODE, PACK_TVSHOW, TMDB_TVSHOW

HANDLE = 7


def run(action, **params):
    xbmcplugin.reset()
    run_action(HANDLE, action, params)


# -- find -------------------------------------------------------------------

def test_find_match_exact_renvoie_un_seul_item(scraper_api):
    run('find', title='One Piece Kaï')
    items = xbmcplugin.items()
    assert len(items) == 1
    url, li, is_folder = items[0]
    assert json.loads(url) == {'fankai': '53', 'title': 'One Piece Kaï', 'year': 1999}
    assert is_folder
    assert li.label == 'One Piece Kaï (1999)'
    assert li.offscreen
    assert float(li.getProperty('relevance')) > 0.8
    assert li.getVideoInfoTag().unique_ids == {'fankai': '53'}
    assert xbmcplugin.CALLS[-1] == ('endOfDirectory', HANDLE, True)


def test_find_avec_annee_departage(scraper_api):
    run('find', title='Hunter x Hunter Kaï', year='1999')
    items = xbmcplugin.items()
    assert json.loads(items[0][0])['fankai'] == '82'
    assert items[0][1].getProperty('relevance') == '1.0'
    assert json.loads(items[1][0])['fankai'] == '71'


def test_find_sans_resultat(scraper_api):
    run('find', title='Totalement inconnu')
    assert xbmcplugin.items() == []
    assert xbmcplugin.CALLS[-1][0] == 'endOfDirectory'


def test_find_api_indisponible(scraper_api, fake_http):
    from lib.http import ApiError
    fake_http.overrides['series'] = ApiError('panne')
    run('find', title='One Piece Kaï')
    assert xbmcplugin.items() == []
    assert any('indisponible' in m for m in logged(xbmc.LOGERROR))


# -- NfoUrl -----------------------------------------------------------------

def test_nfourl_pack_titre_exact(scraper_api):
    nfo = PACK_TVSHOW.replace('Ao Ashi Henshū', 'One Piece Kaï')
    run('NfoUrl', nfo=nfo)
    items = xbmcplugin.items()
    assert len(items) == 1
    assert json.loads(items[0][0])['fankai'] == '53'
    assert items[0][1].getVideoInfoTag().unique_ids == {'fankai': '53'}


def test_nfourl_uniqueid_sans_reseau(scraper_api, fake_http):
    nfo = '<tvshow><title>Peu importe</title><uniqueid type="fankai">85</uniqueid></tvshow>'
    run('nfourl', nfo=nfo)
    assert json.loads(xbmcplugin.items()[0][0]) == {'fankai': '85', 'title': 'Peu importe'}
    assert fake_http.count() == 0


def test_nfourl_ne_detourne_pas_une_autre_bibliotheque(scraper_api):
    run('NfoUrl', nfo=TMDB_TVSHOW)
    assert xbmcplugin.items() == []
    # Indice Fankai mais titre non strictement identique : refus aussi.
    run('NfoUrl', nfo=PACK_TVSHOW.replace('Ao Ashi Henshū', 'One Piece'))
    assert xbmcplugin.items() == []


def test_nfourl_ambigu_departage_par_annee(scraper_api):
    nfo = PACK_TVSHOW.replace('Ao Ashi Henshū', 'Hunter x Hunter Kaï').replace('2022', '2011')
    run('NfoUrl', nfo=nfo)
    assert json.loads(xbmcplugin.items()[0][0])['fankai'] == '71'
    nfo = '<tvshow><title>Hunter x Hunter Kaï</title><studio>Fan-Kai</studio></tvshow>'
    run('NfoUrl', nfo=nfo)
    assert xbmcplugin.items() == []


def test_nfourl_episode_ignore(scraper_api):
    run('NfoUrl', nfo=EPISODE)
    assert xbmcplugin.items() == []


# -- getdetails ---------------------------------------------------------------

def test_getdetails(scraper_api):
    run('getdetails', url=encode_show_url(53, 'One Piece Kaï', 1999))
    succeeded, li = xbmcplugin.resolved()
    assert succeeded
    tag = li.getVideoInfoTag()
    assert tag.data['setTitle'] == 'One Piece Kaï'
    assert len(tag.seasons) == 13
    assert tag.cast
    assert {a['arttype'] for a in tag.artwork if a['season'] == -1} == {'poster', 'banner', 'clearlogo'}
    assert tag.fanart


def test_getdetails_par_unique_ids(scraper_api):
    run('getdetails', uniqueIDs='{"fankai": "1"}')
    succeeded, li = xbmcplugin.resolved()
    assert succeeded
    assert li.getVideoInfoTag().data['setTitle'] == 'Black Lagoon Henshū'


def test_getdetails_identifiant_mort_reidentifie_par_titre(scraper_api, fake_http):
    run('getdetails', url=encode_show_url(999, 'One Piece Kaï', 1999))
    succeeded, li = xbmcplugin.resolved()
    assert succeeded
    assert li.getVideoInfoTag().unique_ids == {'fankai': '53'}
    assert any('remplacé par 53' in m for m in logged(xbmc.LOGWARNING))


def test_getdetails_identifiant_mort_sans_titre(scraper_api):
    run('getdetails', url='999')
    succeeded, _ = xbmcplugin.resolved()
    assert not succeeded


def test_getdetails_path_settings(scraper_api):
    run('getdetails', url='53', pathSettings=json.dumps({'use_original_title': True}))
    _, li = xbmcplugin.resolved()
    assert li.getVideoInfoTag().data['setTitle'] == 'ワンピース'


def test_getdetails_reglage_addon(scraper_api):
    xbmcaddon.SETTINGS['use_original_title'] = True
    run('getdetails', url='53')
    _, li = xbmcplugin.resolved()
    assert li.getVideoInfoTag().data['setTitle'] == 'ワンピース'


# -- getepisodelist -------------------------------------------------------------

def test_getepisodelist(scraper_api):
    run('getepisodelist', url=json.dumps({'fankai': '53'}))
    items = xbmcplugin.items()
    # Seules les saisons 0 et 1 ont une fixture ; les autres répondent 404 et sont ignorées.
    expected = len(load_fixture('seasons_110_episodes.json')['episodes']) \
        + len(load_fixture('seasons_118_episodes.json')['episodes'])
    assert len(items) == expected
    assert any('introuvable' in m for m in logged(xbmc.LOGWARNING))
    url, li, is_folder = items[0]
    tag = li.getVideoInfoTag()
    assert not is_folder
    assert url == '53/110/714'
    assert (tag.data['setSeason'], tag.data['setEpisode']) == (0, 1)
    assert tag.data['setTitle'] == 'Strong World'
    assert tag.data['setFirstAired'] == '2009-12-12'
    assert tag.data['setMediaType'] == 'episode'
    assert (items[5][1].getVideoInfoTag().data['setSeason'], items[5][1].getVideoInfoTag().data['setEpisode']) == (1, 1)


def test_getepisodelist_guide_vide_du_pack(scraper_api):
    run('getepisodelist', url='{}')
    assert xbmcplugin.items() == []
    assert any('uniqueid' in m for m in logged(xbmc.LOGERROR))


def test_getepisodelist_identifiant_nu(scraper_api):
    run('getepisodelist', url='1')
    assert len(xbmcplugin.items()) == 4


# -- getepisodedetails ----------------------------------------------------------

def test_getepisodedetails(scraper_api):
    run('getepisodedetails', url='53/110/714')
    succeeded, li = xbmcplugin.resolved()
    assert succeeded
    tag = li.getVideoInfoTag()
    assert tag.data['setTitle'] == 'Strong World'
    assert tag.data['setDuration'] == 6600
    assert tag.unique_ids == {'fankai': '714'}
    assert tag.artwork[0]['arttype'] == 'thumb'


def test_getepisodedetails_introuvable(scraper_api):
    run('getepisodedetails', url='53/110/1')
    assert xbmcplugin.resolved()[0] is False
    run('getepisodedetails', url='n importe quoi')
    assert xbmcplugin.resolved()[0] is False


# -- getartwork ------------------------------------------------------------------

def test_getartwork(scraper_api, fake_http):
    run('getdetails', url='53')
    calls = fake_http.count()
    run('getartwork', id='53')
    succeeded, li = xbmcplugin.resolved()
    assert succeeded
    assert li.getVideoInfoTag().artwork
    assert fake_http.count() == calls, 'aucun appel réseau : tout vient du cache'


def test_getartwork_sans_id(scraper_api):
    run('getartwork')
    assert xbmcplugin.resolved()[0] is False


# -- robustesse -------------------------------------------------------------------

def test_exception_inattendue_ne_remonte_pas(scraper_api, monkeypatch):
    def boom(*args, **kwargs):
        raise RuntimeError('cassé')
    monkeypatch.setattr(scraper_api, 'list_series', boom)
    run('find', title='One Piece Kaï')
    assert xbmcplugin.CALLS[-1][0] == 'endOfDirectory'
    monkeypatch.setattr(scraper_api, 'get_bundle', boom)
    run('getdetails', url='53')
    assert xbmcplugin.resolved()[0] is False
    assert any('cassé' in m for m in logged(xbmc.LOGERROR))


def test_action_inconnue(scraper_api):
    run('danser')
    assert xbmcplugin.CALLS == [('endOfDirectory', HANDLE, True)]


def test_build_api_utilise_le_profil(tmp_path):
    from lib.config import Settings
    api = scraper.build_api(Settings())
    api._cache.put('k', 1, ttl=10)
    assert (tmp_path / 'profile' / 'cache.db').exists()
