"""Stub du module `xbmcgui` : ListItem et InfoTagVideo enregistreurs.

`KODI_VERSION` simule les différences d'API entre versions de Kodi :
- < 22 : `addSeason` n'accepte pas le paramètre `plot` (TypeError) ;
- < 21 : `setAvailableFanart` n'existe pas sur InfoTagVideo (AttributeError),
  seulement sur ListItem.
"""

KODI_VERSION = 21

_SIMPLE_SETTERS = (
    'setTitle', 'setOriginalTitle', 'setSortTitle', 'setPlot', 'setPlotOutline',
    'setTagLine', 'setPremiered', 'setFirstAired', 'setYear', 'setTvShowStatus',
    'setGenres', 'setStudios', 'setCountries', 'setTags', 'setMpaa', 'setDuration',
    'setTrailer', 'setEpisodeGuide', 'setSeason', 'setEpisode', 'setSortSeason',
    'setSortEpisode', 'setMediaType', 'setTvShowTitle', 'setUserRating',
    'setDateAdded', 'setWriters', 'setDirectors', 'setPlayCount',
)


class InfoTagVideo:
    def __init__(self):
        # Dernière valeur par setter simple, ex. data['setTitle'] == 'Ao Ashi Henshū'
        self.data = {}
        self.ratings = {}
        self.default_rating = None
        self.unique_ids = {}
        self.default_unique_id = None
        self.cast = []
        self.seasons = []
        self.artwork = []
        self.fanart = []

    def __getattr__(self, name):
        if name in _SIMPLE_SETTERS:
            def setter(value):
                self.data[name] = value
            return setter
        if name == 'setAvailableFanart' and KODI_VERSION < 21:
            raise AttributeError(name)
        raise AttributeError(name)

    def setRating(self, rating, votes=0, type='', isdefault=False):
        self.ratings[type] = (rating, votes)
        if isdefault:
            self.default_rating = type

    def setRatings(self, ratings, defaultrating=''):
        self.ratings.update(ratings)
        self.default_rating = defaultrating or self.default_rating

    def setUniqueIDs(self, values, defaultuniqueid=''):
        self.unique_ids = dict(values)
        self.default_unique_id = defaultuniqueid

    def setCast(self, actors):
        self.cast = list(actors)

    def addSeason(self, number, name='', *args):
        if args and KODI_VERSION < 22:
            raise TypeError('addSeason() takes at most 3 arguments')
        plot = args[0] if args else ''
        self.seasons.append((number, name, plot))

    def addAvailableArtwork(self, url, arttype, preview='', referrer='', cache='',
                            post=False, isgz=False, season=-1):
        self.artwork.append({
            'url': url, 'arttype': arttype, 'preview': preview, 'season': season,
        })

    def setAvailableFanart(self, images):
        if KODI_VERSION < 21:
            raise AttributeError('setAvailableFanart')
        self.fanart = list(images)


class ListItem:
    def __init__(self, label='', label2='', path='', offscreen=False):
        self.label = label
        self.label2 = label2
        self.path = path
        self.offscreen = offscreen
        self.properties = {}
        self.art = {}
        self.fanart = []
        self._tag = InfoTagVideo()

    def getVideoInfoTag(self):
        return self._tag

    def setLabel(self, label):
        self.label = label

    def getLabel(self):
        return self.label

    def setProperty(self, key, value):
        self.properties[key] = value

    def getProperty(self, key):
        return self.properties.get(key, '')

    def setArt(self, values):
        self.art.update(values)

    def setAvailableFanart(self, images):
        self.fanart = list(images)


def set_kodi_version(version):
    global KODI_VERSION
    KODI_VERSION = version
