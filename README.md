<div align="center">

<img src="https://raw.githubusercontent.com/Nackophilz/fankai_jellyfin/main/Assets/fankai.png" alt="Logo Fankai" width="400">

# Plugin Fankai AIO (Jellyfin & Emby)

_Les métadonnées ultimes pour la communauté Kaï._

[![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4.svg?logo=dotnet)](https://dotnet.microsoft.com/)
[![Jellyfin](https://img.shields.io/badge/Jellyfin-10.9%2B-00A4DC?logo=jellyfin)](https://jellyfin.org/)
[![Emby](https://img.shields.io/badge/Emby-4.8%2B-52B54B?logo=emby)](https://emby.media/)
[![License](https://img.shields.io/github/license/Nackophilz/fankai_jellyfin)](LICENSE)

[**🌐 API Fankai**](https://metadata.fankai.fr) · [**🐛 Signaler un bug**](https://github.com/Nackophilz/fankai_jellyfin/issues) · [**💬 Discord**](https://discord.gg/fankai)

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

Notre architecture hybride en **.NET 8** permet au plugin de tourner nativement sur les deux plateformes leaders du marché.

### 🔵 Pour Jellyfin (v10.9.0 ou supérieure)

L'installation est entièrement automatisée via le système de dépôt Jellyfin.

1. Allez dans **Tableau de bord** ➔ **Plugins** ➔ **Dépôts** (Repositories).
2. Ajoutez ce dépôt Fankai :
   ```text
   https://raw.githubusercontent.com/Nackophilz/fankai_jellyfin/refs/heads/main/manifest.json
   ```
3. Allez dans l'onglet **Catalogue**, cherchez **Fankai** et installez-le.
4. **Redémarrez** votre serveur Jellyfin.
> _💡 Les mises à jour futures se feront automatiquement via l'interface Jellyfin._

### 🟢 Pour Emby (v4.8.0 ou supérieure)

Le plugin nécessite une installation manuelle (Emby n'ayant pas de catalogue communautaire ouvert de la même manière).

1. Allez sur notre page [**Releases**](https://github.com/Nackophilz/fankai_jellyfin/releases).
2. Téléchargez le fichier `Jellyfin.Plugin.Fankai.Emby.zip`.
3. Décompressez l'archive et placez la `.dll` dans le dossier `plugins` de votre serveur Emby :
   * **Windows :** `C:\ProgramData\Emby-Server\plugins`
   * **Linux / Docker :** `/config/plugins` ou `/var/lib/emby/plugins`
4. **Redémarrez** Emby.
5. Allez dans **Dashboard** ➔ **Plugins** pour vérifier qu'il est bien actif.

## ⚙️ Comment l'utiliser ?

Pour que le plugin opère sa magie, vous devez dire à votre serveur de l'utiliser :

1. Allez dans les paramètres de votre bibliothèque de séries (Séries TV / Animés / Kaï).
2. Dans **Récupérateurs de métadonnées** (Metadata Providers), cochez **Fankai**.
3. (Optionnel mais recommandé) Remontez "Fankai" tout en haut de la liste pour qu'il soit prioritaire.
4. Lancez une analyse complète (Scan / Refresh Metadata) de votre bibliothèque.

## 🛠️ Pour les Développeurs

Ce projet utilise les **GitHub Actions** pour l'Intégration Continue (CI). À chaque push sur la branche `main`, le code est compilé pour les deux environnements (`Release` pour Jellyfin, `Emby` pour Emby), les archives ZIP sont créées avec leurs checksums MD5, et les manifestes JSON sont automatiquement mis à jour.

### Compiler localement :
```bash
# Pour Jellyfin
dotnet publish Jellyfin.Plugin.Fankai/Jellyfin.Plugin.Fankai.csproj -c Release

# Pour Emby
dotnet publish Jellyfin.Plugin.Fankai/Jellyfin.Plugin.Fankai.csproj -c Emby
```

---
*Fait avec ❤️ par la communauté Fankai.*
