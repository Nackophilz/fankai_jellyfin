from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from dataclasses import dataclass
from typing import Optional, Tuple

from lib.config import EDIT_TYPES, FANKAI_STUDIO
from lib.matching import normalize, strip_year

_RE_UNIQUEID = re.compile(
    r'<uniqueid\b[^>]*\btype\s*=\s*["\']fankai["\'][^>]*>\s*(\d+)\s*</uniqueid>', re.IGNORECASE)
_RE_API_URL = re.compile(r'metadata\.fankai\.fr/series/(\d+)', re.IGNORECASE)
_RE_ROOT = re.compile(r'<(tvshow|episodedetails)\b', re.IGNORECASE)
_RE_TITLE = re.compile(r'<title>\s*(.*?)\s*</title>', re.IGNORECASE | re.DOTALL)
_RE_YEAR = re.compile(r'<year>\s*(\d{4})\s*</year>', re.IGNORECASE)
_RE_PREMIERED = re.compile(r'<premiered>\s*(\d{4})', re.IGNORECASE)
_RE_STUDIO = re.compile(r'<studio>\s*(.*?)\s*</studio>', re.IGNORECASE | re.DOTALL)
_RE_GENRE = re.compile(r'<genre>\s*(.*?)\s*</genre>', re.IGNORECASE | re.DOTALL)


@dataclass
class NfoInfo:
    kind: Optional[str]
    fankai_id: Optional[str]
    title: Optional[str]
    year: Optional[int]
    is_fankai: bool


def parse_nfo(text: Optional[str]) -> NfoInfo:
    if isinstance(text, bytes):
        text = text.decode('utf-8', 'replace')
    text = (text or '').lstrip('﻿').strip()
    if not text:
        return NfoInfo(None, None, None, None, False)

    fankai_id = None
    match = _RE_UNIQUEID.search(text) or _RE_API_URL.search(text)
    if match:
        fankai_id = match.group(1)

    root = _RE_ROOT.search(text)
    kind = root.group(1).lower() if root else None
    if kind != 'tvshow':
        return NfoInfo(kind, fankai_id, None, None, False)

    title, year, studio, first_genre = _tvshow_fields(text, root.start())
    title, title_year = strip_year(title)
    if year is None:
        year = title_year
    return NfoInfo('tvshow', fankai_id, title or None, year, _looks_fankai(title, studio, first_genre))


def _tvshow_fields(text: str, start: int) -> Tuple[str, Optional[int], str, str]:
    """Champs utiles du <tvshow>, via ElementTree si possible, par regex sinon."""
    xml_text = text[start:]
    end = xml_text.lower().rfind('</tvshow>')
    if end != -1:
        xml_text = xml_text[:end + len('</tvshow>')]
    try:
        node = ET.fromstring(xml_text)
        title = (node.findtext('title') or '').strip()
        year = _to_year(node.findtext('year')) or _to_year((node.findtext('premiered') or '')[:4])
        studio = (node.findtext('studio') or '').strip()
        genre = node.find('genre')
        first_genre = (genre.text or '').strip() if genre is not None else ''
        return title, year, studio, first_genre
    except ET.ParseError:
        pass
    title = _first(_RE_TITLE, xml_text)
    year = _to_year(_first(_RE_YEAR, xml_text)) or _to_year(_first(_RE_PREMIERED, xml_text))
    return title, year, _first(_RE_STUDIO, xml_text), _first(_RE_GENRE, xml_text)


def _first(pattern: 're.Pattern[str]', text: str) -> str:
    match = pattern.search(text)
    return match.group(1).strip() if match else ''


def _to_year(value: Optional[str]) -> Optional[int]:
    try:
        year = int((value or '').strip())
    except ValueError:
        return None
    return year if 1900 <= year <= 2100 else None


def _looks_fankai(title: str, studio: str, first_genre: str) -> bool:
    if normalize(studio) == FANKAI_STUDIO:
        return True
    if normalize(first_genre) in EDIT_TYPES:
        return True
    words = normalize(title)
    return any(words == t or words.endswith(' ' + t) for t in EDIT_TYPES)
