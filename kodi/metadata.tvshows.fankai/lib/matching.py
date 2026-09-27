from __future__ import annotations

import re
import unicodedata
from dataclasses import dataclass
from typing import Any, Dict, Iterable, List, Optional, Tuple

DEFAULT_MIN_SCORE = 75
YEAR_BONUS = 15
MAX_SCORE = 100 + YEAR_BONUS

_RE_SPACES = re.compile(r'\s+')
_RE_YEAR_SUFFIX = re.compile(r'\s*\((\d{4})\)\s*$')


def normalize(title: Optional[str]) -> str:
    if not title or not title.strip():
        return ''
    decomposed = unicodedata.normalize('NFD', title)
    stripped = ''.join(c for c in decomposed if unicodedata.category(c) != 'Mn')
    lowered = stripped.lower()
    kept = ''.join(c for c in lowered if c.isalnum() or c.isspace())
    return _RE_SPACES.sub(' ', kept).strip()


def strip_year(title: Optional[str]) -> Tuple[str, Optional[int]]:
    """Sépare un suffixe « (1999) » du titre : ('Hunter x Hunter Kaï', 1999)."""
    if not title:
        return '', None
    match = _RE_YEAR_SUFFIX.search(title)
    if not match:
        return title.strip(), None
    return title[:match.start()].strip(), int(match.group(1))


def levenshtein(a: str, b: str) -> int:
    if not a:
        return len(b)
    if not b:
        return len(a)
    previous = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        current = [i]
        for j, cb in enumerate(b, 1):
            cost = 0 if ca == cb else 1
            current.append(min(previous[j] + 1, current[j - 1] + 1, previous[j - 1] + cost))
        previous = current
    return previous[-1]


def score_normalized(query: str, candidate: str) -> int:
    if not query or not candidate:
        return 0
    max_len = max(len(query), len(candidate))
    return 100 - levenshtein(query, candidate) * 100 // max_len


def title_score(query: Optional[str], candidate: Optional[str]) -> int:
    return score_normalized(normalize(query), normalize(candidate))


def candidate_titles(series: Dict[str, Any]) -> List[str]:
    """Titres comparables d'une série, normalisés et sans doublon.
    """
    seen = set()
    titles = []
    raw = [series.get('title'), series.get('show_title'), series.get('title_for_plex')]
    title_without_year, _ = strip_year(series.get('title'))
    raw.append(title_without_year)
    for value in raw:
        norm = normalize(value)
        if norm and norm not in seen:
            seen.add(norm)
            titles.append(norm)
    return titles


def series_year(series: Dict[str, Any]) -> Optional[int]:
    year = series.get('year')
    if isinstance(year, int):
        return year
    try:
        return int(str(year)) if year else None
    except ValueError:
        return None


@dataclass
class Match:
    series: Dict[str, Any]
    score: int

    @property
    def relevance(self) -> float:
        return round(min(self.score, MAX_SCORE) / MAX_SCORE, 4)


def search(series_list: Iterable[Dict[str, Any]], title: str, year: Optional[int] = None,
           min_score: int = DEFAULT_MIN_SCORE) -> List[Match]:
    """Séries dont le score dépasse `min_score`, triées de la meilleure à la moins bonne."""
    query_title, query_year = strip_year(title)
    if year is None:
        year = query_year
    query = normalize(query_title)
    if not query:
        return []
    matches = []
    for series in series_list:
        best = max((score_normalized(query, cand) for cand in candidate_titles(series)), default=0)
        if best <= min_score:
            continue
        if year is not None and series_year(series) == year:
            best += YEAR_BONUS
        matches.append(Match(series, best))
    matches.sort(key=lambda m: (-m.score, normalize(m.series.get('title'))))
    return matches


def exact_matches(series_list: Iterable[Dict[str, Any]], title: str) -> List[Dict[str, Any]]:
    """Séries dont un des titres est strictement égal au titre normalisé."""
    query = normalize(title)
    if not query:
        return []
    return [s for s in series_list if query in candidate_titles(s)]


def is_unambiguous(matches: List[Match]) -> bool:
    """Vrai si le premier résultat est un match exact sans concurrent exact."""
    if not matches:
        return False
    if matches[0].score < 100:
        return False
    return len(matches) == 1 or matches[1].score < 100
