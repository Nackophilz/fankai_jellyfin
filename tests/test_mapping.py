import json

import pytest
import xbmcgui
from conftest import load_fixture

from lib import mapping
from lib.config import Settings


def test_clean_plot_normalise_les_fins_de_ligne():
    assert mapping.clean_plot('a\r\r\r\r\nb') == 'a\n\nb'
    assert mapping.clean_plot('a\r\nb\r\n\r\nc  ') == 'a\nb\n\nc'
    assert mapping.clean_plot(None) == ''


def test_split_genres_et_type_d_edition():
    genres = mapping.split_genres('Kaï,Action, Aventure,,Animation')
    assert genres == ['Kaï', 'Action', 'Aventure', 'Animation']
    assert mapping.edit_type_tag(genres) == 'Kaï'
    assert mapping.edit_type_tag(['Action', 'Kaï']) is None
    assert mapping.split_genres(None) == []


def test_to_int():
    assert mapping.to_int('7') == 7
    assert mapping.to_int(7) == 7
    assert mapping.to_int('') is None
    assert mapping.to_int(None) is None
    assert mapping.to_int('x') is None
    assert mapping.to_int(True) is None


def test_episode_numbers_episode_regulier():
    envelope = load_fixture('seasons_118_episodes.json')
    season = {'id': 118, 'season_number': 1}
    episode = envelope['episodes'][0]
    assert episode.get('season_number') is None
    assert mapping.episode_numbers(episode, envelope, season) == (1, 1, None, None)


def test_episode_numbers_special_saison_zero():
    envelope = load_fixture('seasons_110_episodes.json')
    season = {'id': 110, 'season_number': 0}
    numbers = mapping.episode_numbers(envelope['episodes'][0], envelope, season)
    # display_season vaut 0 : pas de position de tri à imposer.
    assert numbers == (0, 1, None, None)


def test_episode_numbers_fallbacks_et_tri():
    episode = {'episode_number': None, 'display_episode': '7', 'display_season': '2'}
    assert mapping.episode_numbers(episode, {}, {'season_number': None}) == (2, 7, None, None)
    episode = {'episode_number': 5, 'display_episode': '5', 'display_season': '2'}
    assert mapping.episode_numbers(episode, {'season_number': 1}, {}) == (1, 5, 2, 5)
    assert mapping.episode_numbers({'episode_number': None}, {'season_number': 1}, {}) is None
    assert mapping.episode_numbers({'episode_number': -1}, {'season_number': 1}, {}) is None


def _bundle(serie_id):
    series = load_fixture('series_{}.json'.format(serie_id))
    seasons = load_fixture('series_{}_seasons.json'.format(serie_id))['seasons']
    actors = load_fixture('series_{}_actors.json'.format(serie_id))['actors']
    return series, seasons, actors


def test_fill_show_tag_one_piece_kai():
    series, seasons, actors = _bundle(53)
    li = xbmcgui.ListItem(offscreen=True)
    tag = li.getVideoInfoTag()
    mapping.fill_show_tag(tag, series, seasons, actors, Settings())

    assert tag.data['setTitle'] == 'One Piece Kaï'
    assert tag.data['setOriginalTitle'] == 'ワンピース'
    assert tag.data['setYear'] == 1999
    assert tag.data['setPremiered'] == series['premiered']
    assert tag.data['setTvShowStatus'] == 'Continuing'
    assert tag.data['setGenres'][0] == 'Kaï'
    assert tag.data['setTags'] == ['Kaï']
    assert tag.data['setStudios'] == ['Fan-Kai']
    assert tag.data['setCountries'] == ['Japon']
    assert tag.data['setMediaType'] == 'tvshow'
    assert 'setSortTitle' not in tag.data
    assert '\r' not in tag.data['setPlot']

    assert tag.default_rating == 'fankai'
    assert tag.ratings['fankai'][0] == pytest.approx(series['rating_value'])
    assert tag.unique_ids == {'fankai': '53'}
    assert tag.default_unique_id == 'fankai'
    assert json.loads(tag.data['setEpisodeGuide']) == {'fankai': '53', 'title': 'One Piece Kaï', 'year': 1999}

    assert len(tag.seasons) == 13
    assert tag.seasons[0] == (0, 'Films Officiels', mapping.clean_plot(seasons[0].get('plot')))
    assert tag.seasons[1][:2] == (1, 'East Blue')

    assert len(tag.cast) == len(actors)
    assert [a.order for a in tag.cast] == list(range(len(tag.cast)))
    assert {a.name for a in tag.cast} == {a['name'] for a in actors}
    assert all(a.thumbnail for a in tag.cast)


def test_order_cast_place_le_kaieur_en_tete():
    actors = [{'name': 'A', 'role': 'Nami (voice)'}, {'name': 'B', 'role': 'Kaïeur'},
              {'name': 'C', 'role': 'Luffy (voice)'}, {'name': 'D', 'role': 'kaieur'}]
    assert [a['name'] for a in mapping.order_cast(actors)] == ['B', 'D', 'A', 'C']
    tag = xbmcgui.ListItem(offscreen=True).getVideoInfoTag()
    mapping.fill_show_tag(tag, {'id': 1, 'title': 'x'}, [], actors, Settings())
    assert [(a.name, a.order) for a in tag.cast] == [('B', 0), ('D', 1), ('A', 2), ('C', 3)]


def test_fill_show_tag_reglages():
    series, seasons, actors = _bundle(53)
    tag = xbmcgui.ListItem(offscreen=True).getVideoInfoTag()
    settings = Settings(use_original_title=True, api_season_names=False, edit_type_as_tag=False,
                        enable_trailer=False)
    mapping.fill_show_tag(tag, series, seasons, actors, settings)
    assert tag.data['setTitle'] == 'ワンピース'
    assert tag.seasons == []
    assert 'setTags' not in tag.data
    assert 'setTrailer' not in tag.data


def test_fill_show_tag_trailer_et_mpaa():
    series, seasons, actors = _bundle(53)
    series = dict(series, trailer_url='plugin://plugin.video.youtube/play/?video_id=abc', mpaa='FR:12',
                  sort_title='One Piece 1', tagline='Slogan')
    tag = xbmcgui.ListItem(offscreen=True).getVideoInfoTag()
    mapping.fill_show_tag(tag, series, seasons, actors, Settings())
    assert tag.data['setTrailer'] == 'plugin://plugin.video.youtube/play/?video_id=abc'
    assert tag.data['setMpaa'] == 'FR:12'
    assert tag.data['setSortTitle'] == 'One Piece 1'
    assert tag.data['setTagLine'] == 'Slogan'


def test_serie_sans_note_ni_images_optionnelles():
    series = next(s for s in load_fixture('series_list.json') if s['id'] == 11)
    assert 'rating_value' not in series
    li = xbmcgui.ListItem(offscreen=True)
    tag = li.getVideoInfoTag()
    mapping.fill_show_tag(tag, series, [], [], Settings())
    assert tag.ratings == {}
    assert tag.cast == []
    mapping.add_show_art(li, series, [])
    assert {a['arttype'] for a in tag.artwork} == {'poster'}
    assert len(tag.fanart) == 1


def test_add_show_art_serie_et_saisons():
    series, seasons, _ = _bundle(53)
    li = xbmcgui.ListItem(offscreen=True)
    tag = li.getVideoInfoTag()
    mapping.add_show_art(li, series, seasons)
    show_art = {a['arttype']: a for a in tag.artwork if a['season'] == -1}
    assert set(show_art) == {'poster', 'banner', 'clearlogo'}
    assert show_art['poster']['url'] == series['images']['poster']
    assert show_art['clearlogo']['url'] == series['images']['logo']
    assert show_art['poster']['preview'] == show_art['poster']['url']
    assert 'thumb' not in show_art
    assert tag.fanart == [{'image': series['images']['fanart'], 'preview': series['images']['fanart']}]
    season_posters = [a for a in tag.artwork if a['arttype'] == 'poster' and a['season'] != -1]
    assert {a['season'] for a in season_posters} == {s['season_number'] for s in seasons if s['images'].get('poster')}
    season_fanart = [a for a in tag.artwork if a['arttype'] == 'fanart' and a['season'] != -1]
    assert len(season_fanart) == sum(1 for s in seasons if s['images'].get('fanart'))


def test_compatibilite_kodi_20():
    xbmcgui.set_kodi_version(20)
    series, seasons, actors = _bundle(53)
    li = xbmcgui.ListItem(offscreen=True)
    tag = li.getVideoInfoTag()
    mapping.fill_show_tag(tag, series, seasons, actors, Settings())
    assert tag.seasons[1] == (1, 'East Blue', '')
    mapping.add_show_art(li, series, seasons)
    assert tag.fanart == []
    assert li.fanart[0]['image'] == series['images']['fanart']


def test_fill_search_item():
    series = load_fixture('series_53.json')
    li = xbmcgui.ListItem(offscreen=True)
    mapping.fill_search_item(li, series)
    tag = li.getVideoInfoTag()
    assert tag.data['setTitle'] == 'One Piece Kaï'
    assert tag.unique_ids == {'fankai': '53'}
    assert tag.artwork[0]['arttype'] == 'poster'


def test_fill_episode_tag():
    envelope = load_fixture('seasons_110_episodes.json')
    episode = envelope['episodes'][0]
    tag = xbmcgui.ListItem(offscreen=True).getVideoInfoTag()
    mapping.fill_episode_tag(tag, episode, (0, 1, None, None))
    assert tag.data['setTitle'] == 'Strong World'
    assert tag.data['setSeason'] == 0
    assert tag.data['setEpisode'] == 1
    assert tag.data['setDuration'] == 6600
    assert tag.data['setFirstAired'] == '2009-12-12'
    assert tag.data['setPremiered'] == '2009-12-12'
    assert tag.data['setYear'] == 2009
    assert 'setMpaa' not in tag.data
    assert tag.data['setStudios'] == [episode['studio']]
    assert tag.unique_ids == {'fankai': '714'}
    assert tag.artwork == [{'url': episode['thumb_image'], 'arttype': 'thumb',
                            'preview': episode['thumb_image'], 'season': -1}]
    assert 'setSortSeason' not in tag.data


def test_fill_episode_tag_tri_et_titre_par_defaut():
    tag = xbmcgui.ListItem(offscreen=True).getVideoInfoTag()
    mapping.fill_episode_tag(tag, {'id': 9, 'plot': 'x\r\n'}, (1, 4, 2, 4))
    assert tag.data['setTitle'] == 'Épisode 4'
    assert tag.data['setSortSeason'] == 2
    assert tag.data['setSortEpisode'] == 4
    assert tag.data['setPlot'] == 'x'
    assert 'setDuration' not in tag.data
    assert tag.artwork == []
