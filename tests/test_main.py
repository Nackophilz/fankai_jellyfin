import json

import main


def test_parse_argv():
    handle, params = main.parse_argv(
        ['plugin://metadata.tvshows.fankai/', '5',
         '?action=find&title=One+Piece+Ka%C3%AF&year=1999&pathSettings=%7B%22a%22%3A+true%7D'])
    assert handle == 5
    assert params['action'] == 'find'
    assert params['title'] == 'One Piece Kaï'
    assert params['year'] == '1999'
    assert json.loads(params['pathSettings']) == {'a': True}


def test_parse_argv_sans_query():
    assert main.parse_argv(['x']) == (-1, {})
    assert main.parse_argv(['x', '3', '']) == (3, {})


def test_main_dispatch(monkeypatch):
    calls = []
    monkeypatch.setattr(main, 'run_action', lambda handle, action, params: calls.append((handle, action, params)))
    main.main(['x', '2', '?action=getartwork&id=53'])
    assert calls == [(2, 'getartwork', {'action': 'getartwork', 'id': '53'})]
