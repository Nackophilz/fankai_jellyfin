"""Stub du module `xbmcvfs` : opérations de fichiers locales."""

import os


def translatePath(path):
    return path


def exists(path):
    return os.path.exists(path)


def mkdir(path):
    try:
        os.mkdir(path)
        return True
    except OSError:
        return False


def mkdirs(path):
    try:
        os.makedirs(path, exist_ok=True)
        return True
    except OSError:
        return False
