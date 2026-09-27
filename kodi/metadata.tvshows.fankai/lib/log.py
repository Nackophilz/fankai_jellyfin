from __future__ import annotations

import xbmc

from lib.config import ADDON_ID

_PREFIX = '[{}] '.format(ADDON_ID)
_verbose = False


def init(verbose: bool) -> None:
    global _verbose
    _verbose = bool(verbose)


def _emit(message: str, level: int) -> None:
    if isinstance(message, bytes):
        message = message.decode('utf-8', 'replace')
    xbmc.log(_PREFIX + message, level)


def debug(message: str) -> None:
    if _verbose:
        _emit(message, xbmc.LOGDEBUG)


def info(message: str) -> None:
    _emit(message, xbmc.LOGINFO)


def warning(message: str) -> None:
    _emit(message, xbmc.LOGWARNING)


def error(message: str) -> None:
    _emit(message, xbmc.LOGERROR)
