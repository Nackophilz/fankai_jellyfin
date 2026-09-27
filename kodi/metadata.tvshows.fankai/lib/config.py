from __future__ import annotations

import json
from dataclasses import dataclass
from typing import Any, Dict, Optional

import xbmcaddon

ADDON_ID = 'metadata.tvshows.fankai'
DEFAULT_BASE_URL = 'https://metadata.fankai.fr'
UNIQUE_ID_TYPE = 'fankai'

EDIT_TYPES = ('kai', 'henshu', 'yabai', 'recut', 'fan cut', 'fancut')
FANKAI_STUDIO = 'fan kai'

IDLE_TTL = 15 * 60
TTL_SERIES_LIST = 60 * 60
TTL_SHOW = 24 * 60 * 60


@dataclass
class Settings:
    base_url: str = DEFAULT_BASE_URL
    use_original_title: bool = False
    api_season_names: bool = True
    edit_type_as_tag: bool = True
    enable_trailer: bool = True
    verbose_log: bool = False


def addon() -> xbmcaddon.Addon:
    return xbmcaddon.Addon()


def addon_version() -> str:
    try:
        return addon().getAddonInfo('version') or '0'
    except Exception:  # pylint: disable=broad-except
        return '0'


def _parse_path_settings(raw: Optional[str]) -> Dict[str, Any]:
    if not raw:
        return {}
    try:
        data = json.loads(raw)
    except (TypeError, ValueError):
        return {}
    return data if isinstance(data, dict) else {}


def _setting_bool(add: xbmcaddon.Addon, overrides: Dict[str, Any], key: str, default: bool) -> bool:
    if key in overrides and overrides[key] is not None:
        value = overrides[key]
        if isinstance(value, str):
            return value.strip().lower() in ('true', '1', 'yes', 'on')
        return bool(value)
    try:
        return bool(add.getSettingBool(key))
    except (TypeError, RuntimeError):
        return default


def _setting_str(add: xbmcaddon.Addon, overrides: Dict[str, Any], key: str, default: str) -> str:
    if key in overrides and overrides[key] is not None:
        value = str(overrides[key]).strip()
        return value or default
    try:
        value = (add.getSettingString(key) or '').strip()
    except (TypeError, RuntimeError):
        value = ''
    return value or default


def load_settings(path_settings: Optional[str] = None) -> Settings:
    overrides = _parse_path_settings(path_settings)
    add = addon()
    base_url = _setting_str(add, overrides, 'base_url', DEFAULT_BASE_URL).rstrip('/')
    return Settings(
        base_url=base_url,
        use_original_title=_setting_bool(add, overrides, 'use_original_title', False),
        api_season_names=_setting_bool(add, overrides, 'api_season_names', True),
        edit_type_as_tag=_setting_bool(add, overrides, 'edit_type_as_tag', True),
        enable_trailer=_setting_bool(add, overrides, 'enable_trailer', True),
        verbose_log=_setting_bool(add, overrides, 'verbose_log', False),
    )
