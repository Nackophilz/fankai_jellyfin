"""Fixtures pytest : stubs Kodi réinitialisés, API factice servie depuis tests/fixtures."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

import pytest

import xbmc
import xbmcaddon
import xbmcgui
import xbmcplugin

from lib import cache as cache_module
from lib import log
from lib import scraper
from lib.api import FankaiApi
from lib.cache import Cache
from lib.http import NotFound

FIXTURES = Path(__file__).parent / 'fixtures'


def load_fixture(name: str) -> Any:
    return json.loads((FIXTURES / name).read_text(encoding='utf-8'))


class Clock:
    def __init__(self, start: float = 1_800_000_000.0):
        self.t = start

    def now(self) -> float:
        return self.t

    def advance(self, seconds: float) -> None:
        self.t += seconds


class FakeHttp:
    """Sert les fixtures JSON ; `overrides` force un code ou une exception par chemin."""

    NOT_MODIFIED = 'not_modified'

    def __init__(self):
        self.calls: List[Tuple[str, Optional[str]]] = []
        self.overrides: Dict[str, Any] = {}
        self.etags: Dict[str, str] = {}

    @staticmethod
    def fixture_name(path: str) -> str:
        path = path.strip('/')
        if path == 'series':
            return 'series_list.json'
        return path.replace('/', '_') + '.json'

    def get_json(self, path: str, etag: Optional[str] = None):
        path = path.strip('/')
        self.calls.append((path, etag))
        override = self.overrides.get(path)
        if isinstance(override, Exception):
            raise override
        if override == self.NOT_MODIFIED:
            return 304, None, etag
        if isinstance(override, (dict, list)):
            return 200, override, self.etags.get(path)
        file = FIXTURES / self.fixture_name(path)
        if not file.exists():
            raise NotFound('404 sur {}'.format(path))
        return 200, json.loads(file.read_text(encoding='utf-8')), self.etags.get(path)

    def count(self, path: Optional[str] = None) -> int:
        if path is None:
            return len(self.calls)
        return sum(1 for p, _ in self.calls if p == path.strip('/'))


@pytest.fixture(autouse=True)
def kodi(tmp_path):
    xbmc.reset()
    xbmcaddon.reset()
    xbmcplugin.reset()
    xbmcgui.set_kodi_version(21)
    xbmcaddon.INFO['profile'] = str(tmp_path / 'profile')
    cache_module.clear_memory()
    cache_module._last_access = 0.0
    log.init(True)
    yield


@pytest.fixture
def clock() -> Clock:
    return Clock()


@pytest.fixture
def fake_http() -> FakeHttp:
    return FakeHttp()


@pytest.fixture
def cache(tmp_path, clock) -> Cache:
    return Cache(str(tmp_path / 'cache.db'), now=clock.now)


@pytest.fixture
def api(fake_http, cache) -> FankaiApi:
    return FankaiApi(fake_http, cache)


@pytest.fixture
def scraper_api(monkeypatch, api) -> FankaiApi:
    """Branche l'API factice dans le dispatch du scraper."""
    monkeypatch.setattr(scraper, 'build_api', lambda settings, now=None: api)
    return api


def logged(level: Optional[int] = None) -> List[str]:
    return [m for lvl, m in xbmc.MESSAGES if level is None or lvl == level]
