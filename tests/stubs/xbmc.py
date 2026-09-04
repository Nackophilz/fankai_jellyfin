"""Stub minimal du module `xbmc` de Kodi pour les tests hors Kodi."""

LOGDEBUG = 0
LOGINFO = 1
LOGWARNING = 2
LOGERROR = 3
LOGFATAL = 4

# Messages journalisés pendant un test : (niveau, message).
MESSAGES = []


def log(msg, level=LOGDEBUG):
    MESSAGES.append((level, msg))


class Actor:
    def __init__(self, name='', role='', order=-1, thumbnail=''):
        self.name = name
        self.role = role
        self.order = order
        self.thumbnail = thumbnail

    def getName(self):
        return self.name

    def getRole(self):
        return self.role

    def getOrder(self):
        return self.order

    def getThumbnail(self):
        return self.thumbnail

    def __repr__(self):
        return 'Actor({!r}, {!r}, {!r}, {!r})'.format(
            self.name, self.role, self.order, self.thumbnail)


def reset():
    del MESSAGES[:]
