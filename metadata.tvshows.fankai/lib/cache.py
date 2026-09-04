from __future__ import annotations

import json
import os
import sqlite3
import time
from dataclasses import dataclass
from typing import Any, Callable, Dict, Optional

from lib import log
from lib.config import IDLE_TTL

PURGE_GRACE = 7 * 24 * 60 * 60


@dataclass
class Entry:
    data: Any
    etag: Optional[str]
    expires: float
    meta: Optional[Dict[str, Any]]

    def is_fresh(self, now: float) -> bool:
        return now < self.expires


_memory: Dict[str, Entry] = {}
_last_access = 0.0


def clear_memory() -> None:
    _memory.clear()


class Cache:
    def __init__(self, db_path: str, now: Callable[[], float] = time.time):
        self._db_path = db_path
        self._now = now
        self._ready = False
        self._touch_memory()

    def now(self) -> float:
        return self._now()

    def _touch_memory(self) -> None:
        global _last_access
        now = self._now()
        if _last_access and now - _last_access > IDLE_TTL:
            log.debug('cache mémoire vidé après inactivité')
            _memory.clear()
        _last_access = now

    def _connect(self) -> Optional[sqlite3.Connection]:
        try:
            directory = os.path.dirname(self._db_path)
            if directory and not os.path.isdir(directory):
                os.makedirs(directory, exist_ok=True)
            conn = sqlite3.connect(self._db_path, timeout=5)
            if not self._ready:
                try:
                    conn.execute('PRAGMA journal_mode=WAL')
                except sqlite3.Error:
                    pass
                conn.execute(
                    'CREATE TABLE IF NOT EXISTS cache ('
                    'key TEXT PRIMARY KEY, payload TEXT NOT NULL, etag TEXT, '
                    'expires REAL NOT NULL, meta TEXT)')
                conn.commit()
                self._ready = True
            return conn
        except (sqlite3.Error, OSError) as exc:
            log.error('cache sqlite indisponible ({}) : {}'.format(self._db_path, exc))
            return None

    def get(self, key: str) -> Optional[Entry]:
        self._touch_memory()
        entry = _memory.get(key)
        if entry is not None:
            return entry
        conn = self._connect()
        if conn is None:
            return None
        try:
            row = conn.execute(
                'SELECT payload, etag, expires, meta FROM cache WHERE key = ?', (key,)).fetchone()
        except sqlite3.Error as exc:
            log.error('lecture du cache impossible ({}) : {}'.format(key, exc))
            return None
        finally:
            conn.close()
        if row is None:
            return None
        try:
            entry = Entry(json.loads(row[0]), row[1], float(row[2]),
                          json.loads(row[3]) if row[3] else None)
        except (ValueError, TypeError):
            self.delete(key)
            return None
        _memory[key] = entry
        return entry

    def put(self, key: str, data: Any, ttl: float, etag: Optional[str] = None,
            meta: Optional[Dict[str, Any]] = None) -> Entry:
        entry = Entry(data, etag, self._now() + ttl, meta)
        _memory[key] = entry
        conn = self._connect()
        if conn is None:
            return entry
        try:
            conn.execute(
                'INSERT OR REPLACE INTO cache (key, payload, etag, expires, meta) VALUES (?, ?, ?, ?, ?)',
                (key, json.dumps(data, ensure_ascii=False, separators=(',', ':')), etag,
                 entry.expires, json.dumps(meta) if meta else None))
            conn.commit()
        except sqlite3.Error as exc:
            log.error('écriture du cache impossible ({}) : {}'.format(key, exc))
        finally:
            conn.close()
        return entry

    def touch(self, key: str, ttl: float) -> None:
        """Prolonge une entrée après une réponse 304 (contenu inchangé)."""
        expires = self._now() + ttl
        entry = _memory.get(key)
        if entry is not None:
            entry.expires = expires
        conn = self._connect()
        if conn is None:
            return
        try:
            conn.execute('UPDATE cache SET expires = ? WHERE key = ?', (expires, key))
            conn.commit()
        except sqlite3.Error as exc:
            log.error('mise à jour du cache impossible ({}) : {}'.format(key, exc))
        finally:
            conn.close()

    def delete(self, key: str) -> None:
        _memory.pop(key, None)
        conn = self._connect()
        if conn is None:
            return
        try:
            conn.execute('DELETE FROM cache WHERE key = ?', (key,))
            conn.commit()
        except sqlite3.Error as exc:
            log.error('suppression du cache impossible ({}) : {}'.format(key, exc))
        finally:
            conn.close()

    def delete_prefix(self, prefix: str) -> None:
        for key in [k for k in _memory if k.startswith(prefix)]:
            _memory.pop(key, None)
        conn = self._connect()
        if conn is None:
            return
        try:
            conn.execute('DELETE FROM cache WHERE substr(key, 1, ?) = ?', (len(prefix), prefix))
            conn.commit()
        except sqlite3.Error as exc:
            log.error('suppression du cache impossible ({}*) : {}'.format(prefix, exc))
        finally:
            conn.close()

    def purge_expired(self, grace: float = PURGE_GRACE) -> None:
        """Supprime les entrées périmées depuis plus de `grace` secondes."""
        limit = self._now() - grace
        for key in [k for k, e in _memory.items() if e.expires < limit]:
            _memory.pop(key, None)
        conn = self._connect()
        if conn is None:
            return
        try:
            conn.execute('DELETE FROM cache WHERE expires < ?', (limit,))
            conn.commit()
        except sqlite3.Error as exc:
            log.error('purge du cache impossible : {}'.format(exc))
        finally:
            conn.close()
