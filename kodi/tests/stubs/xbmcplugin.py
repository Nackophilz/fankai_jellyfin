"""Stub du module `xbmcplugin` : enregistre les appels faits par le scraper."""

# Liste de tuples : ('addDirectoryItem', handle, url, listitem, isFolder),
# ('setResolvedUrl', handle, succeeded, listitem), ('endOfDirectory', handle, succeeded).
CALLS = []


def addDirectoryItem(handle, url, listitem, isFolder=False, totalItems=0):
    CALLS.append(('addDirectoryItem', handle, url, listitem, isFolder))
    return True


def addDirectoryItems(handle, items, totalItems=0):
    for url, listitem, is_folder in items:
        addDirectoryItem(handle, url, listitem, is_folder, totalItems)
    return True


def endOfDirectory(handle, succeeded=True, updateListing=False, cacheToDisc=True):
    CALLS.append(('endOfDirectory', handle, succeeded))


def setResolvedUrl(handle, succeeded, listitem):
    CALLS.append(('setResolvedUrl', handle, succeeded, listitem))


def items():
    """Items ajoutés via addDirectoryItem : liste de (url, listitem, isFolder)."""
    return [(c[2], c[3], c[4]) for c in CALLS if c[0] == 'addDirectoryItem']


def resolved():
    """Dernier setResolvedUrl : (succeeded, listitem) ou None."""
    for call in reversed(CALLS):
        if call[0] == 'setResolvedUrl':
            return call[2], call[3]
    return None


def reset():
    del CALLS[:]
