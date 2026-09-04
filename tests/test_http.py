import gzip
import io
import json
import socket
from email.message import Message
from urllib.error import HTTPError, URLError

import pytest

from lib.http import ApiError, HttpClient, NotFound, RateLimited


class FakeResponse:
    def __init__(self, body: bytes, headers=None):
        self._body = body
        self.headers = Message()
        for key, value in (headers or {}).items():
            self.headers[key] = value

    def read(self):
        return self._body

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False


def http_error(code, headers=None):
    msg = Message()
    for key, value in (headers or {}).items():
        msg[key] = value
    return HTTPError('https://x/series', code, 'err', msg, io.BytesIO(b''))


class Opener:
    """Renvoie ou lève tour à tour les éléments de `script`."""

    def __init__(self, script):
        self.script = list(script)
        self.requests = []

    def __call__(self, request, timeout=None):
        self.requests.append(request)
        item = self.script.pop(0)
        if isinstance(item, Exception):
            raise item
        return item


def make_client(script, sleeps=None):
    opener = Opener(script)
    client = HttpClient('https://metadata.fankai.fr/', 'test/1',
                        opener=opener, sleep=(sleeps.append if sleeps is not None else lambda s: None))
    return client, opener


def test_get_json_ok_et_entetes():
    client, opener = make_client([FakeResponse(b'{"a": 1}', {'ETag': '"e"'})])
    assert client.get_json('/series', etag='"old"') == (200, {'a': 1}, '"e"')
    request = opener.requests[0]
    assert request.full_url == 'https://metadata.fankai.fr/series'
    assert request.get_header('If-none-match') == '"old"'
    assert request.get_header('User-agent') == 'test/1'
    assert request.get_header('Accept-encoding') == 'gzip'


def test_gzip():
    body = gzip.compress(json.dumps({'ok': True}).encode('utf-8'))
    client, _ = make_client([FakeResponse(body, {'Content-Encoding': 'gzip'})])
    assert client.get_json('series')[1] == {'ok': True}


def test_304():
    client, _ = make_client([http_error(304, {'ETag': '"e2"'})])
    assert client.get_json('series', etag='"e1"') == (304, None, '"e2"')
    client, _ = make_client([http_error(304)])
    assert client.get_json('series', etag='"e1"') == (304, None, '"e1"')


def test_404():
    client, _ = make_client([http_error(404)])
    with pytest.raises(NotFound):
        client.get_json('series/999')


def test_429_puis_succes_respecte_retry_after():
    sleeps = []
    client, _ = make_client([http_error(429, {'Retry-After': '2'}), FakeResponse(b'[]')], sleeps)
    assert client.get_json('series') == (200, [], None)
    assert sleeps == [2.0]


def test_429_persistant():
    sleeps = []
    client, _ = make_client([http_error(429), http_error(429)], sleeps)
    with pytest.raises(RateLimited) as exc:
        client.get_json('series')
    assert exc.value.retry_after == 5.0
    assert sleeps == [5.0]


def test_5xx_un_seul_nouvel_essai():
    client, _ = make_client([http_error(503), FakeResponse(b'1')])
    assert client.get_json('series')[1] == 1
    client, _ = make_client([http_error(503), http_error(503)])
    with pytest.raises(ApiError):
        client.get_json('series')


def test_erreur_reseau_puis_succes():
    client, _ = make_client([URLError('boom'), FakeResponse(b'2')])
    assert client.get_json('series')[1] == 2
    client, _ = make_client([socket.timeout(), socket.timeout()])
    with pytest.raises(ApiError):
        client.get_json('series')


def test_json_invalide():
    client, _ = make_client([FakeResponse(b'<html>')])
    with pytest.raises(ApiError):
        client.get_json('series')


def test_url_absolue_conservee():
    client, opener = make_client([FakeResponse(b'{}')])
    client.get_json('https://autre.example/x')
    assert opener.requests[0].full_url == 'https://autre.example/x'
