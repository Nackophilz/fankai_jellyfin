from __future__ import annotations

import json
import re
from dataclasses import dataclass
from typing import Any, Dict, Optional, Tuple

from lib.config import UNIQUE_ID_TYPE

_RE_API_URL = re.compile(r'metadata\.fankai\.fr/series/(\d+)', re.IGNORECASE)


@dataclass
class ShowRef:
    fankai_id: Optional[str]
    title: Optional[str] = None
    year: Optional[int] = None

    def __bool__(self) -> bool:
        return bool(self.fankai_id or self.title)


def encode_show_url(serie_id: Any, title: Optional[str] = None, year: Optional[int] = None) -> str:
    payload: Dict[str, Any] = {UNIQUE_ID_TYPE: str(serie_id)}
    if title:
        payload['title'] = title
    if year:
        payload['year'] = int(year)
    return json.dumps(payload, ensure_ascii=False)


def encode_episode_url(serie_id: Any, season_id: Any, episode_id: Any) -> str:
    return '{}/{}/{}'.format(serie_id, season_id, episode_id)


def decode_episode_url(url: Optional[str]) -> Optional[Tuple[int, int, int]]:
    parts = (url or '').strip().split('/')
    if len(parts) != 3:
        return None
    try:
        serie_id, season_id, episode_id = (int(p) for p in parts)
    except ValueError:
        return None
    return serie_id, season_id, episode_id


def parse_show_ref(value: Optional[str]) -> ShowRef:
    """Interprète une url/episodeguide : JSON, entier nu ou URL de l'API."""
    text = (value or '').strip()
    if not text:
        return ShowRef(None)
    if text.isdigit():
        return ShowRef(text)
    match = _RE_API_URL.search(text)
    if match:
        return ShowRef(match.group(1))
    try:
        data = json.loads(text)
    except ValueError:
        return ShowRef(None)
    if not isinstance(data, dict):
        return ShowRef(None)
    fankai_id = data.get(UNIQUE_ID_TYPE)
    fankai_id = str(fankai_id).strip() if fankai_id not in (None, '') else None
    if fankai_id is not None and not fankai_id.isdigit():
        fankai_id = None
    title = data.get('title')
    year = data.get('year')
    try:
        year = int(year) if year not in (None, '') else None
    except (TypeError, ValueError):
        year = None
    return ShowRef(fankai_id, str(title).strip() if title else None, year)


def resolve_show_ref(params: Dict[str, str]) -> ShowRef:
    """Référence de série à partir des paramètres d'un appel Kodi.

    `uniqueIDs` (JSON des identifiants connus de Kodi) prime sur `url`.
    """
    unique_ids = params.get('uniqueIDs')
    if unique_ids:
        try:
            data = json.loads(unique_ids)
        except ValueError:
            data = None
        if isinstance(data, dict):
            fankai_id = data.get(UNIQUE_ID_TYPE)
            if fankai_id not in (None, '') and str(fankai_id).strip().isdigit():
                ref = parse_show_ref(params.get('url'))
                return ShowRef(str(fankai_id).strip(), ref.title, ref.year)
    return parse_show_ref(params.get('url'))
