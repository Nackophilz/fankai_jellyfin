from lib import cache as cache_module
from lib.cache import Cache
from lib.config import IDLE_TTL


def test_put_get_et_expiration(cache, clock):
    cache.put('k', {'a': 1}, ttl=60, etag='"e1"', meta={'m': 1})
    entry = cache.get('k')
    assert entry.data == {'a': 1}
    assert entry.etag == '"e1"'
    assert entry.meta == {'m': 1}
    assert entry.is_fresh(clock.now())
    clock.advance(61)
    entry = cache.get('k')
    assert entry is not None, 'une entrée périmée reste lisible (stale-if-error)'
    assert not entry.is_fresh(clock.now())


def test_touch_prolonge(cache, clock):
    cache.put('k', 1, ttl=10)
    clock.advance(11)
    cache.touch('k', 100)
    assert cache.get('k').is_fresh(clock.now())


def test_disque_partage_entre_instances(tmp_path, clock):
    a = Cache(str(tmp_path / 'c.db'), now=clock.now)
    a.put('k', [1, 2], ttl=10)
    cache_module.clear_memory()
    b = Cache(str(tmp_path / 'c.db'), now=clock.now)
    assert b.get('k').data == [1, 2]
    b.touch('k', 500)
    cache_module.clear_memory()
    assert a.get('k').expires == clock.now() + 500


def test_delete_et_prefix(cache):
    cache.put('series:1', 1, ttl=10)
    cache.put('series:12', 2, ttl=10)
    cache.put('seasons:1', 3, ttl=10)
    cache.delete('series:1')
    assert cache.get('series:1') is None
    cache.delete_prefix('series:')
    assert cache.get('series:12') is None
    assert cache.get('seasons:1').data == 3


def test_purge_expired(cache, clock):
    cache.put('old', 1, ttl=10)
    cache.put('new', 2, ttl=10 ** 6)
    clock.advance(10 + 8 * 24 * 3600)
    cache.purge_expired()
    cache_module.clear_memory()
    assert cache.get('old') is None
    assert cache.get('new').data == 2


def test_memoire_videe_apres_inactivite(cache, clock):
    cache.put('k', 1, ttl=10 ** 6)
    assert 'k' in cache_module._memory
    clock.advance(IDLE_TTL + 1)
    cache.get('autre')
    assert 'k' not in cache_module._memory
    # Le disque, lui, a toujours la valeur.
    assert cache.get('k').data == 1


def test_repertoire_cree_et_base_illisible(tmp_path, clock):
    cache = Cache(str(tmp_path / 'sous' / 'dossier' / 'c.db'), now=clock.now)
    cache.put('k', 1, ttl=10)
    assert (tmp_path / 'sous' / 'dossier' / 'c.db').exists()
    # Chemin impossible : le cache dégrade en mémoire seule sans lever.
    broken = Cache(str(tmp_path / 'c.db' / 'impossible.db'), now=clock.now)
    broken.put('k', 2, ttl=10)
    assert broken.get('k').data == 2
