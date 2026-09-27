"""Stub du module `xbmcaddon` : réglages et infos d'add-on pilotés par les tests."""

# Valeurs de réglages (id -> valeur Python typée), modifiées par les tests.
SETTINGS = {}

# Infos renvoyées par getAddonInfo ; `profile` est repointé vers un dossier
# temporaire par la fixture `kodi`.
INFO = {
    'id': 'metadata.tvshows.fankai',
    'name': 'Fankai',
    'version': '0.0.0-test',
    'profile': '',
    'path': '',
}

# Chaînes localisées (id -> texte) ; vide par défaut, getLocalizedString
# renvoie alors une chaîne vide comme Kodi pour un id inconnu.
STRINGS = {}


class Addon:
    def __init__(self, id=None):
        self._id = id or INFO['id']

    def getAddonInfo(self, key):
        return INFO.get(key, '')

    def getSetting(self, key):
        value = SETTINGS.get(key)
        if value is None:
            return ''
        if isinstance(value, bool):
            return 'true' if value else 'false'
        return str(value)

    def getSettingBool(self, key):
        value = SETTINGS.get(key)
        if value is None:
            # Kodi lève pour un réglage absent du schéma ; on reste indulgent
            # mais explicite pour que le code appelant gère le défaut.
            raise TypeError('setting inconnu: {}'.format(key))
        return bool(value)

    def getSettingString(self, key):
        return self.getSetting(key)

    def getSettingInt(self, key):
        value = SETTINGS.get(key)
        if value is None:
            raise TypeError('setting inconnu: {}'.format(key))
        return int(value)

    def setSetting(self, key, value):
        SETTINGS[key] = value

    def getLocalizedString(self, string_id):
        return STRINGS.get(string_id, '')


def reset():
    SETTINGS.clear()
    STRINGS.clear()
