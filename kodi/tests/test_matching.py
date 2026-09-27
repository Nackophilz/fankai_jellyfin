from conftest import load_fixture

from lib.matching import (exact_matches, is_unambiguous, levenshtein, normalize, search,
                          strip_year, title_score)


def test_normalize_retire_diacritiques_et_ponctuation():
    assert normalize('Ao Ashi Henshū') == 'ao ashi henshu'
    # Comme le plugin Jellyfin : la ponctuation est supprimée, pas remplacée par
    # un espace ; title_for_plex (« Kaguya sama … ») couvre l'autre variante.
    assert normalize('Kaguya-sama : Love is War Henshū') == 'kaguyasama love is war henshu'
    assert normalize('Détective Conan Kaï') == 'detective conan kai'
    assert normalize("D.Gray-Man Kaï") == 'dgrayman kai'
    assert normalize('  ') == ''
    assert normalize(None) == ''


def test_strip_year():
    assert strip_year('Hunter x Hunter Kaï (1999)') == ('Hunter x Hunter Kaï', 1999)
    assert strip_year('One Piece Kaï') == ('One Piece Kaï', None)
    assert strip_year('') == ('', None)


def test_levenshtein():
    assert levenshtein('', 'abc') == 3
    assert levenshtein('kitten', 'sitting') == 3
    assert levenshtein('abc', 'abc') == 0


def test_title_score_est_relatif_a_la_longueur():
    assert title_score('One Piece Kaï', 'One Piece Kaï') == 100
    # Titre tronqué : rejeté comme dans le plugin Jellyfin (score < 75).
    assert title_score('Mob Psycho', 'Mob Psycho 100 Henshū') < 75


def test_search_departage_hunter_x_hunter_par_annee():
    series = load_fixture('series_list.json')
    sans_annee = search(series, 'Hunter x Hunter Kaï')
    assert {m.series['id'] for m in sans_annee[:2]} == {71, 82}
    assert sans_annee[0].score == sans_annee[1].score == 100
    assert not is_unambiguous(sans_annee)

    avec_annee = search(series, 'Hunter x Hunter Kaï', 1999)
    assert avec_annee[0].series['id'] == 82
    assert avec_annee[0].score == 115
    assert avec_annee[0].relevance == 1.0

    # Kodi passe parfois l'année dans le titre : même résultat.
    assert search(series, 'Hunter x Hunter Kaï (2011)')[0].series['id'] == 71


def test_search_one_piece_kai_sans_ambiguite_avec_yabai():
    series = load_fixture('series_list.json')
    matches = search(series, 'One Piece Kaï')
    assert matches[0].series['id'] == 53
    assert is_unambiguous(matches)
    assert all(m.series['id'] != 67 or m.score < 100 for m in matches)


def test_search_rejette_les_titres_eloignes():
    series = load_fixture('series_list.json')
    assert search(series, 'Naruto') == []
    assert search(series, '') == []


def test_exact_matches():
    series = load_fixture('series_list.json')
    assert [s['id'] for s in exact_matches(series, 'Monster Kaï')] == [35]
    # « MONSTER » est le titre original de Monster Kaï ET de Monster Henshū : ignoré.
    assert exact_matches(series, 'Monster') == []
    assert search(series, 'Monster') == []
    # title_for_plex (sans ponctuation) compte aussi.
    assert [s['id'] for s in exact_matches(series, 'Hunter x Hunter Kaï 1999')] == [82]
