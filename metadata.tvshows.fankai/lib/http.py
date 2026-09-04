from __future__ import annotations

import gzip
import json
import socket
import ssl
import time
from typing import Any, Callable, Optional, Tuple
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen

from lib import log

DEFAULT_TIMEOUT = 15
DEFAULT_RETRY_AFTER = 5.0
MAX_RETRY_AFTER = 30.0


class ApiError(Exception):
    """Erreur réseau ou réponse inexploitable."""


class NotFound(ApiError):
    """HTTP 404 : l'identifiant n'existe pas (ou plus)."""


class RateLimited(ApiError):
    """HTTP 429 persistant après un nouvel essai."""

    def __init__(self, retry_after: float):
        super().__init__('limite de requêtes atteinte (réessayer dans {:.0f} s)'.format(retry_after))
        self.retry_after = retry_after


class HttpClient:
    """GET JSON avec gestion des codes 304/404/429/5xx et un unique nouvel essai."""

    def __init__(self, base_url: str, user_agent: str, timeout: int = DEFAULT_TIMEOUT,
                 opener: Callable[..., Any] = urlopen, sleep: Callable[[float], None] = time.sleep):
        self.base_url = base_url.rstrip('/')
        self.timeout = timeout
        self._opener = opener
        self._sleep = sleep
        self._headers = {
            'User-Agent': user_agent,
            'Accept': 'application/json',
            'Accept-Encoding': 'gzip',
        }

    def url(self, path: str) -> str:
        if path.startswith('http://') or path.startswith('https://'):
            return path
        return '{}/{}'.format(self.base_url, path.lstrip('/'))

    def get_json(self, path: str, etag: Optional[str] = None) -> Tuple[int, Any, Optional[str]]:
        """Renvoie (200, données, etag) ou (304, None, etag).

        Lève NotFound sur 404, RateLimited sur 429 persistant, ApiError sinon.
        """
        url = self.url(path)
        headers = dict(self._headers)
        if etag:
            headers['If-None-Match'] = etag
        request = Request(url, headers=headers)

        retried = False
        while True:
            try:
                with self._opener(request, timeout=self.timeout) as response:
                    body = _read_body(response)
                    response_etag = response.headers.get('ETag') or None
                try:
                    return 200, json.loads(body.decode('utf-8')), response_etag
                except ValueError as exc:
                    raise ApiError('réponse JSON invalide pour {}: {}'.format(url, exc))
            except HTTPError as exc:
                if exc.code == 304:
                    return 304, None, (exc.headers.get('ETag') if exc.headers else None) or etag
                if exc.code == 404:
                    raise NotFound('404 sur {}'.format(url))
                if exc.code == 429:
                    delay = _retry_after(exc)
                    if retried:
                        raise RateLimited(delay)
                    log.warning('429 sur {} : nouvel essai dans {:.0f} s'.format(url, delay))
                    self._sleep(delay)
                    retried = True
                    continue
                if 500 <= exc.code < 600 and not retried:
                    log.warning('HTTP {} sur {} : nouvel essai'.format(exc.code, url))
                    self._sleep(1.0)
                    retried = True
                    continue
                raise ApiError('HTTP {} sur {}'.format(exc.code, url))
            except ssl.SSLError as exc:
                raise ApiError(_tls_message(url, exc))
            except (URLError, socket.timeout, OSError) as exc:
                reason = getattr(exc, 'reason', exc)
                if isinstance(reason, ssl.SSLError):
                    raise ApiError(_tls_message(url, reason))
                if not retried:
                    log.warning('échec réseau sur {} ({}) : nouvel essai'.format(url, reason))
                    self._sleep(1.0)
                    retried = True
                    continue
                raise ApiError('échec réseau sur {} : {}'.format(url, reason))


def _tls_message(url: str, exc: Exception) -> str:
    return ('erreur TLS vers {} ({}) : vérifier la date système ou les certificats de Kodi'
            .format(url, exc))


def _read_body(response: Any) -> bytes:
    body = response.read()
    encoding = (response.headers.get('Content-Encoding') or '').lower()
    if encoding == 'gzip' or body[:2] == b'\x1f\x8b':
        body = gzip.decompress(body)
    return body


def _retry_after(exc: HTTPError) -> float:
    value = exc.headers.get('Retry-After') if exc.headers else None
    try:
        delay = float(value) if value else DEFAULT_RETRY_AFTER
    except ValueError:
        delay = DEFAULT_RETRY_AFTER
    return max(1.0, min(delay, MAX_RETRY_AFTER))
