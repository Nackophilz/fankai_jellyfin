import json

from lib.ids import (decode_episode_url, encode_episode_url, encode_show_url, parse_show_ref,
                     resolve_show_ref)


def test_show_url_aller_retour():
    url = encode_show_url(53, 'One Piece Kaï', 1999)
    assert json.loads(url) == {'fankai': '53', 'title': 'One Piece Kaï', 'year': 1999}
    ref = parse_show_ref(url)
    assert (ref.fankai_id, ref.title, ref.year) == ('53', 'One Piece Kaï', 1999)
    assert ref


def test_parse_show_ref_formes_tolerees():
    assert parse_show_ref('53').fankai_id == '53'
    assert parse_show_ref('https://metadata.fankai.fr/series/53').fankai_id == '53'
    assert parse_show_ref('{"fankai": 53}').fankai_id == '53'
    assert parse_show_ref('{"fankai": "abc"}').fankai_id is None
    assert parse_show_ref('{"title": "One Piece Kaï"}').title == 'One Piece Kaï'


def test_parse_show_ref_vide():
    for value in ('', None, '{}', '[]', 'n importe quoi', '{"tmdb": "1"}'):
        assert not parse_show_ref(value)


def test_resolve_show_ref_privilegie_unique_ids():
    params = {'uniqueIDs': '{"fankai": "1", "tmdb": "99"}',
              'url': encode_show_url(53, 'One Piece Kaï', 1999)}
    ref = resolve_show_ref(params)
    assert ref.fankai_id == '1'
    assert ref.title == 'One Piece Kaï'
    assert resolve_show_ref({'uniqueIDs': '{"tmdb": "99"}', 'url': '53'}).fankai_id == '53'
    assert resolve_show_ref({'uniqueIDs': 'pas du json', 'url': '53'}).fankai_id == '53'


def test_episode_url():
    assert encode_episode_url(53, 110, 714) == '53/110/714'
    assert decode_episode_url('53/110/714') == (53, 110, 714)
    assert decode_episode_url('53/110') is None
    assert decode_episode_url('a/b/c') is None
    assert decode_episode_url(None) is None
