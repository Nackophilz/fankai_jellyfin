from lib.nfo import parse_nfo

PACK_TVSHOW = '''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<!--created on 2026-03-06 15:54:25 by tinyMediaManager 5.2.4 for KODI-->
<tvshow>
  <title>Ao Ashi Henshū</title>
  <originaltitle>アオアシ</originaltitle>
  <year>2022</year>
  <plot>Ashito Aoi vit dans la préfecture d'Ehime…</plot>
  <namedseason number="1">Saison 1</namedseason>
  <episodeguide>{}</episodeguide>
  <id/>
  <premiered>2022-04-09</premiered>
  <status>Continuing</status>
  <genre>Henshū</genre>
  <genre>Animation</genre>
  <studio>Fan-Kai</studio>
</tvshow>
'''

TMDB_TVSHOW = '''<tvshow>
  <title>One Piece</title>
  <year>1999</year>
  <genre>Animation</genre>
  <studio>Toei Animation</studio>
  <uniqueid type="tmdb" default="true">37854</uniqueid>
</tvshow>
'''

EPISODE = '''<episodedetails>
  <title>Les détections</title>
  <season>1</season>
  <episode>1</episode>
</episodedetails>
'''


def test_tvshow_du_pack_sans_uniqueid():
    info = parse_nfo(PACK_TVSHOW)
    assert info.kind == 'tvshow'
    assert info.fankai_id is None
    assert info.title == 'Ao Ashi Henshū'
    assert info.year == 2022
    assert info.is_fankai


def test_uniqueid_fankai_est_prioritaire():
    nfo = PACK_TVSHOW.replace('<id/>', '<uniqueid type="fankai" default="true">77</uniqueid>')
    assert parse_nfo(nfo).fankai_id == '77'


def test_nfo_reduit_a_une_url():
    info = parse_nfo('https://metadata.fankai.fr/series/53\n')
    assert info.kind is None
    assert info.fankai_id == '53'


def test_nfo_tmdb_n_est_pas_fankai():
    info = parse_nfo(TMDB_TVSHOW)
    assert info.kind == 'tvshow'
    assert info.fankai_id is None
    assert info.title == 'One Piece'
    assert not info.is_fankai


def test_titre_avec_suffixe_kai_suffit_comme_indice():
    nfo = '<tvshow><title>Monster Kaï</title></tvshow>'
    info = parse_nfo(nfo)
    assert info.is_fankai
    assert info.title == 'Monster Kaï'


def test_episodedetails_ignore():
    info = parse_nfo(EPISODE)
    assert info.kind == 'episodedetails'
    assert info.fankai_id is None
    assert info.title is None


def test_xml_malforme_avec_bom_retombe_sur_les_regex():
    nfo = '﻿<tvshow><title>Hunter x Hunter Kaï (1999)</title><genre>Kaï</genre><plot>a & b</plot>'
    info = parse_nfo(nfo)
    assert info.kind == 'tvshow'
    assert info.title == 'Hunter x Hunter Kaï'
    assert info.year == 1999
    assert info.is_fankai


def test_annee_depuis_premiered():
    nfo = '<tvshow><title>Black Lagoon Henshū</title><premiered>2006-04-08</premiered></tvshow>'
    assert parse_nfo(nfo).year == 2006


def test_vide():
    info = parse_nfo('')
    assert info.kind is None and info.fankai_id is None
    assert parse_nfo(None).kind is None
    assert parse_nfo(b'<tvshow><title>x</title></tvshow>').title == 'x'
