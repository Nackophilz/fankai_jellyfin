<div align="center">
<img src="https://raw.githubusercontent.com/Nackophilz/fankai_plugin/main/Assets/fankai.png" alt="Logo Fankai" width="400">

   # Fankai pour Kodi
 
_Les métadonnées ultimes pour la communauté Kaï, directement dans Kodi._
 
[![Kodi](https://img.shields.io/badge/Kodi-20%2B%20(Nexus)-17B2E7?logo=kodi)](https://kodi.tv/)
[![Python](https://img.shields.io/badge/Python-3.8%2B-3776AB?logo=python&logoColor=white)](https://www.python.org/)
[![License](https://img.shields.io/github/license/Nackophilz/fankai_plugin)](../LICENSE)
 
[**🌐 API Fankai**](https://metadata.fankai.fr) · [**🧩 Version Jellyfin/Emby**](../README.md) · [**🐛 Signaler un bug**](https://github.com/Nackophilz/fankai_plugin/issues) · [**💬 Discord**](https://discord.gg/fankai)
 
</div>

## 📖 À propos
 
**Fankai pour Kodi** est un fournisseur d'informations (« scraper ») de séries TV alimenté par l'API communautaire [metadata.fankai.fr](https://metadata.fankai.fr). C'est l'équivalent Kodi du [plugin Jellyfin/Emby](../README.md) : vos Kai sont identifiés, remplis et illustrés automatiquement, sans saisie manuelle.
 
## ✨ Ce que l'add-on remplit
 
* 🎬 **Fiche de série complète :** titres, résumé, année, statut, genres, studio, pays, note et bande-annonce.
* 🗂️ **Saisons nommées d'après les arcs :** *East Blue*, *Baroque Works*… avec leur propre résumé, au lieu d'un banal « Saison 1 ».
* 👥 **Casting complet :** acteurs et rôles, avec le Kaïeur mis en tête de liste.
* 📺 **Épisodes détaillés :** titre, résumé, date de diffusion et durée.
* 🖼️ **Images haute qualité :** affiche, fanart, bannière, logo, affiches et fanarts de saison, vignettes d'épisodes.
## 🚀 Installation
 
### Prérequis
 
* **Kodi 20 (Nexus) ou plus récent.** Kodi 19 n'est pas pris en charge.
* Une connexion Internet.
### Via le dépôt Fankai (recommandé)
 
Tout se fait depuis Kodi, à partir d'une seule adresse :
 
```text
https://nackophilz.github.io/fankai_plugin/
```
 
1. **Paramètres** ➔ **Gestionnaire de fichiers** ➔ **Ajouter une source** : collez l'adresse ci-dessus et nommez la source `fankai`.
2. **Paramètres** ➔ **Modules complémentaires** ➔ **Installer depuis un fichier zip** ➔ `fankai` ➔ `repository.fankai-x.y.z.zip`.
3. **Installer depuis un dépôt** ➔ **Dépôt Fankai** ➔ **Fournisseurs d'informations** ➔ **Fournisseurs de séries TV** ➔ **Fankai**.
> _💡 Une fois le dépôt installé, l'add-on se met à jour tout seul. Si Kodi refuse le zip, activez **Paramètres ➔ Système ➔ Modules complémentaires ➔ Sources inconnues**._
 
### Installation manuelle
 
Le zip du scraper seul est disponible sur la même page et sur les [**Releases**](https://github.com/Nackophilz/fankai_plugin/releases?q=kodi). Les mises à jour sont alors à faire à la main.
 
## ⚙️ Comment l'utiliser ?
 
1. Sur la source vidéo contenant vos séries Fan-Kai : **Modifier la source** ➔ **Définir le contenu**.
2. Contenu : **Séries TV**, fournisseur d'informations : **Fankai**.
3. Lancez une analyse.
Pour une source déjà scannée avec un autre fournisseur : **Rafraîchir** la série, ou **Modifier la source ➔ Définir le contenu** puis accepter de rescanner.
 
> _💡 Le niveau d'artwork de Kodi (**Paramètres ➔ Médias ➔ Médiathèque**) vaut « Tout » par défaut et récupère l'ensemble des images. En « Basique », le clearlogo et les fanarts de saison ne seront pas récupérés._
 
## 📁 Nommage des dossiers et des fichiers
 
**Passez par [FanKarr](https://github.com/Masutayunikon/FanKarr)** pour organiser votre bibliothèque : il range les séries et renomme les fichiers au format attendu. C'est la façon prévue de préparer une bibliothèque Fan-Kai, et le scraper est conçu pour cette structure.
 
> ⚠️ **C'est indispensable, pas cosmétique.** Kodi n'envoie **jamais** le nom des fichiers au scraper : il découpe lui-même le numéro de saison et d'épisode dans le nom du fichier (`S01E01`, `1x01`, `E01`…). Les fichiers Fan-Kai bruts (`[Brazh] Ao Ashi Henshū 01 - Les détections - 1080p.VOSTFR.x264.mkv`) ne contiennent aucun motif reconnu et sont **ignorés silencieusement** par Kodi — ils n'apparaîtront tout simplement pas dans la médiathèque.
 
Structure produite par FanKarr, et attendue ici :
 
```text
Séries Fan-Kai/
└── Ao Ashi Henshū/
    ├── Saison 1/
    │   └── Ao Ashi Henshū.S01E01.VOSTFR.1080p.x264-FANKAI.mkv
    └── Specials/
        └── Ao Ashi Henshū.S00E01.MULTI.1080p.x264-FANKAI.mkv
```
 
* Le dossier de la série porte le titre Fan-Kai (`One Piece Kaï`, `Hunter x Hunter Kaï (1999)`…) ; l'année entre parenthèses sert à départager les homonymes.
* Les fichiers portent le nom canonique de l'API (`formatted_name`) et contiennent `SxxExx`.
* Les films et spéciaux vont en saison 0 (`S00Exx`), dans un dossier `Specials`.
Si vous renommez à la main, ces trois règles suffisent — mais FanKarr le fait pour vous.
 
### 📦 Avec le pack NFO (`fankai_pack`)
 
Le pack fournit des `tvshow.nfo`, `season.nfo`, NFO d'épisodes et images déjà au format Kodi.
 
* Les **NFO d'épisodes** sont complets : Kodi les lit directement, le scraper n'est pas sollicité.
* Pour **`tvshow.nfo`**, le scraper reconnaît la série par son titre exact et complète la fiche ; les champs du NFO restent prioritaires. Un `<uniqueid type="fankai">ID</uniqueid>` dans le NFO fige l'identification.
* ⚠️ Un NFO du pack contient `<episodeguide>{}</episodeguide>`, qui remplace le guide du scraper : si un épisode n'a pas de NFO, Kodi ne pourra pas le compléter en ligne. Ajoutez le `uniqueid` ci-dessus ou retirez la balise `episodeguide`.
## 🎛️ Réglages
 
| Réglage | Défaut | Effet |
|---|---|---|
| Utiliser le titre original | non | Titre japonais/original à la place du titre français |
| Nommer les saisons d'après l'API | oui | Noms d'arcs au lieu de « Saison N » |
| Ajouter le type d'édition comme tag | oui | Tag `Kaï` / `Henshū` / `Yabai`… pour filtrer la médiathèque |
| Bandes-annonces | oui | Bande-annonce YouTube quand l'API en fournit (add-on YouTube requis) |
| Journal détaillé (avancé) | non | Messages de débogage dans `kodi.log` |
| URL de l'API (expert) | `https://metadata.fankai.fr` | Instance locale ou miroir |
 
Chaque réglage peut être surchargé par source vidéo (**Définir le contenu ➔ Paramètres**).
 
## 🔍 Fonctionnement et cache
 
Le scraper charge la liste des séries (`/series`) et rapproche le nom du dossier par **distance de Levenshtein** sur le titre normalisé (même algorithme que le plugin Jellyfin), puis lit série, saisons, acteurs et épisodes.
 
Les réponses sont mises en cache dans le profil de l'add-on (`cache.db`, SQLite) : **1 h** pour la liste (revalidée par ETag), **24 h** pour le reste. En cas de panne de l'API, la dernière réponse connue est resservie. Si un identifiant de série disparaît côté API, la série est ré-identifiée par son titre exact.
 
## ⚠️ Limites connues
 
* **Musique de thème (`theme.mp3`)** : le plugin Jellyfin la télécharge dans le dossier de la série ; un scraper Kodi ne connaît pas ce dossier, ce n'est donc pas possible ici. Le pack NFO fournit les `theme.mp3` (lus par l'add-on **TvTunes**).
* Les fichiers sans `SxxExx` ne sont pas scannés par Kodi (voir plus haut).
* Les identifiants **IMDb / TMDb / TVDb** ne sont pas renseignés : l'API n'en a pas (contenu fanmade).
## 🛠️ Pour les Développeurs

Commandes à lancer depuis `kodi/` :
 
```bash
python -m pytest            # tests hors Kodi (stubs xbmc* dans tests/stubs)
python tools/build.py       # zips + addons.xml dans repo/
python tools/make_assets.py # icône et fanart depuis ../../fankai_pack/assets (Pillow)
```
 
Le code doit rester compatible **Python 3.8** (version embarquée par Kodi 20/21 sur Windows et Android) ; la CI l'exécute en 3.8 et 3.12 sous Linux et Windows.
 
Pour tester dans Kodi sans passer par le dépôt : installez le zip produit par `tools/build.py`, ou copiez `metadata.tvshows.fankai/` dans le dossier `addons/` du profil Kodi puis activez l'add-on.
 
### Publication
 
1. Incrémenter `version` dans `metadata.tvshows.fankai/addon.xml` et compléter `<news>`.
2. Pousser sur `main` : la CI joue les tests, construit les zips, publie le site du dépôt sur GitHub Pages et crée la release GitHub `kodi-v<version>` avec le message du dernier commit comme notes.
## 📄 Licence
 
MIT. Voir [LICENSE](../LICENSE).
 
---
 
*Fait avec ❤️ par la communauté Fankai.*
