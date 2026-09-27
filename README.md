<div align="center">

<img src="https://raw.githubusercontent.com/Nackophilz/fankai_plugin/main/Assets/fankai.png" alt="Logo Fankai" width="400">

# Plugin Fankai AIO (Jellyfin, Emby & Kodi)

_Les métadonnées ultimes pour la communauté Kaï._

[![.NET Version](https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512BD4.svg?logo=dotnet)](https://dotnet.microsoft.com/)
[![Jellyfin](https://img.shields.io/badge/Jellyfin-10.9%2B%20%7C%2012-00A4DC?logo=jellyfin)](https://jellyfin.org/)
[![Emby](https://img.shields.io/badge/Emby-4.8%2B-52B54B?logo=emby)](https://emby.media/)
[![Kodi](https://img.shields.io/badge/Kodi-20%2B-17B2E7?logo=kodi)](https://kodi.tv/)
[![Téléchargements](https://img.shields.io/github/downloads/Nackophilz/fankai_plugin/total?label=t%C3%A9l%C3%A9chargements)](https://github.com/Nackophilz/fankai_plugin/releases)
[![License](https://img.shields.io/github/license/Nackophilz/fankai_plugin)](LICENSE)

[**🌐 API Fankai**](https://metadata.fankai.fr) · [**🐛 Signaler un bug**](https://github.com/Nackophilz/fankai_plugin/issues) · [**💬 Discord**](https://discord.gg/fankai)

</div>

## 📖 À propos

Le plugin **Fankai** est un pont direct entre votre serveur multimédia et l'API communautaire [metadata.fankai.fr](https://metadata.fankai.fr). Conçu spécifiquement pour les productions de la team "Fan-Kai", il assure que vos séries soient parfaitement identifiées, affichées et organisées, sans aucune intervention manuelle de votre part.

## ✨ Fonctionnalités Clés

Ce plugin n'est pas qu'un simple scraper. Il intègre des algorithmes avancés pour garantir une correspondance parfaite :

* 🎵 **Thèmes Musicaux Automatiques :** Télécharge automatiquement les musiques thématiques de vos séries (`theme.mp3`) et utilise **FFmpeg en arrière-plan** pour s'assurer que l'encodage audio est parfaitement lisible par vos clients.
* 🖼️ **Images Haute Qualité :** Récupération des Affiches (Posters), Fanarts (Backdrops), Bannières, Logos et Vignettes d'épisodes (Thumbs).
* 🗂️ **Ordonnancement Intelligent :** Support du mode d'affichage "Absolute" (absolu) requis pour les longs animes comme One Piece.
* 👥 **Casting complet :** Remontée des acteurs et de leurs rôles avec photos de profil.

## 🚀 Installation

Notre architecture hybride permet au plugin de tourner nativement sur les deux plateformes leaders du marché : une build **.NET 8** pour Jellyfin 10.9+ et Emby, une build **.NET 10** pour Jellyfin 12. Kodi dispose de son propre add-on, en Python, alimenté par la même API.

### 🔵 Pour Jellyfin (v10.9.0 ou supérieure, y compris 12.x)

L'installation est entièrement automatisée via le système de dépôt Jellyfin.

1. Allez dans **Tableau de bord** ➔ **Plugins** ➔ **Dépôts** (Repositories).
2. Ajoutez ce dépôt Fankai :
   ```text
   https://raw.githubusercontent.com/Nackophilz/fankai_plugin/refs/heads/main/manifest.json
   ```
3. Allez dans l'onglet **Catalogue**, cherchez **Fankai** et installez-le.
4. **Redémarrez** votre serveur Jellyfin.
> _💡 Les mises à jour futures se feront automatiquement via l'interface Jellyfin. Le dépôt est le même pour toutes les versions : Jellyfin 10.x reçoit les versions 3.x, Jellyfin 12 les versions 4.x._

### 🟢 Pour Emby (v4.8.0 ou supérieure)

Le plugin nécessite une installation manuelle (Emby n'ayant pas de catalogue communautaire ouvert de la même manière).

1. Allez sur notre page [**Releases**](https://github.com/Nackophilz/fankai_plugin/releases).
2. Téléchargez le fichier `Jellyfin.Plugin.Fankai.Emby.zip`.
3. Décompressez l'archive et placez la `.dll` dans le dossier `plugins` de votre serveur Emby :
   * **Windows :** `C:\ProgramData\Emby-Server\plugins`
   * **Linux / Docker :** `/config/plugins` ou `/var/lib/emby/plugins`
4. **Redémarrez** Emby.
5. Allez dans **Dashboard** ➔ **Plugins** pour vérifier qu'il est bien actif.

### 🟠 Pour Kodi (v20 Nexus ou supérieure)

L'add-on s'installe depuis le dépôt Kodi Fankai, qui le maintient ensuite à jour.

1. **Paramètres** ➔ **Gestionnaire de fichiers** ➔ **Ajouter une source**, avec cette adresse :
   ```text
   https://nackophilz.github.io/fankai_plugin/
   ```
2. **Modules complémentaires** ➔ **Installer depuis un fichier zip** ➔ `repository.fankai-x.y.z.zip`.
3. **Installer depuis un dépôt** ➔ **Dépôt Fankai** ➔ **Fournisseurs d'informations** ➔ **Fournisseurs de séries TV** ➔ **Fankai**.

> _⚠️ Kodi ne scanne que les fichiers nommés avec `SxxExx`. Le nommage attendu, la configuration de la médiathèque et les réglages sont détaillés dans le [README de l'add-on Kodi](kodi/README.md)._

## ⚙️ Comment l'utiliser ?

Pour que le plugin opère sa magie, vous devez dire à votre serveur de l'utiliser :

1. Allez dans les paramètres de votre bibliothèque de séries (Séries TV / Animés / Kaï).
2. Dans **Récupérateurs de métadonnées** (Metadata Providers), cochez **Fankai**.
3. (Optionnel mais recommandé) Remontez "Fankai" tout en haut de la liste pour qu'il soit prioritaire.
4. Lancez une analyse complète (Scan / Refresh Metadata) de votre bibliothèque.

## 🛠️ Pour les Développeurs

Le dépôt contient deux projets indépendants, qui interrogent la même API :

* [`jellyfin-emby/`](jellyfin-emby) : le plugin .NET, une seule base de code pour Jellyfin et Emby ;
* [`kodi/`](kodi) : l'add-on Kodi en Python et son dépôt.

Ce projet utilise les **GitHub Actions** pour l'Intégration Continue (CI). À chaque push sur la branche `main` touchant `jellyfin-emby/`, le code est compilé pour les trois environnements (`Release` pour Jellyfin 10.x, `Jellyfin12` pour Jellyfin 12, `Emby` pour Emby), les archives ZIP sont créées avec leurs checksums MD5, et les manifestes JSON sont automatiquement mis à jour.

### Compiler localement :
```bash
# Pour Jellyfin 10.x
dotnet publish jellyfin-emby/Jellyfin.Plugin.Fankai/Jellyfin.Plugin.Fankai.csproj -c Release

# Pour Jellyfin 12 (SDK .NET 10 requis)
dotnet publish jellyfin-emby/Jellyfin.Plugin.Fankai/Jellyfin.Plugin.Fankai.csproj -c Jellyfin12

# Pour Emby
dotnet publish jellyfin-emby/Jellyfin.Plugin.Fankai/Jellyfin.Plugin.Fankai.csproj -c Emby
```

Côté Kodi, les tests et la publication sont décrits dans [`kodi/README.md`](kodi/README.md#développement).

---
*Fait avec ❤️ par la communauté Fankai.*
