# Plugin Fankai pour Jellyfin (min. 10.9) et Emby (min. 4.8)

## **À propos du projet**

Le plugin **Fankai** enrichit votre expérience media en intégrant les métadonnées et images de la communauté Fankai directement dans votre médiathèque, que vous utilisiez **Jellyfin** ou **Emby**. Il vise une intégration transparente et qualitative pour les passionnés de la team "Fan-Kai" et les utilisateurs de ces plateformes, en particulier pour les séries animées. Les informations sont récupérées via l’API publique [metadata.fankai.fr](https://metadata.fankai.fr).

---

## **Fonctionnalités**

- **Récupération de métadonnées complètes** : titres, résumés, épisodes, dates de diffusion, etc.
- **Intégration d’images** : affiches, bannières, fonds d’écran, vignettes d’épisodes.
- **Informations supplémentaires** : genres, acteurs/personnages, studios, thèmes musicaux.
- **Mises à jour automatiques** : selon la configuration du catalogue de plugins (Jellyfin) ou via mise à jour manuelle (Emby).

---

## **Installation**

### **Pour Jellyfin (min. 10.9)**

#### **Méthode 1 : Via le catalogue de plugins Jellyfin (recommandé)**

1. Depuis Jellyfin, allez dans **Tableau de bord → Plugins**.
2. Cliquez sur l’onglet **Catalogue**.
3. Recherchez **Fankai** dans la liste.
4. Si non disponible, ajoutez le dépôt personnalisé :  
   `https://raw.githubusercontent.com/Nackophilz/fankai_jellyfin/refs/heads/main/manifest.json`
5. Cliquez sur le plugin Fankai puis sur **Installer**.
6. Redémarrez Jellyfin si demandé.

> Les mises à jour seront ensuite gérées automatiquement via ce catalogue[1][4][7].

---

#### **Méthode 2 : Installation manuelle**

##### **Téléchargement**

- Rendez-vous sur la page **Releases** du projet.
- Téléchargez la dernière version du fichier `Jellyfin.Plugin.Fankai.zip`.

##### **Extraction et placement**

- Décompressez l’archive.
- Copiez le dossier extrait dans le dossier `plugins` de votre installation Jellyfin.

 - **Windows** :  
   `C:\Program Files\Jellyfin\Server\plugins`  
   ou `%LOCALAPPDATA%\jellyfin\plugins`
 - **Linux (Docker)** :  
   Volume monté, ex : `/config/plugins` ou `/data/jellyfin/plugins`
 - **Linux (natif)** :  
   `/var/lib/jellyfin/plugins` ou `/usr/lib/jellyfin/plugins`

##### **Redémarrage**

- Arrêtez puis redémarrez Jellyfin pour charger le plugin.

---

### **Pour Emby (min. 4.8)**

> ⚠️ Le plugin Fankai n’est **pas disponible dans le catalogue officiel Emby**. L’installation se fait **uniquement manuellement**.

#### **Étapes d’installation**

1. **Téléchargez le plugin**  
   - Allez sur la page [Releases du projet Fankai Jellyfin](https://github.com/Nackophilz/fankai_jellyfin/releases)  
   - Téléchargez le fichier `Jellyfin.Plugin.Fankai.zip`

2. **Renommez le fichier**  
   - Renommez le fichier en `Emby.Plugin.Fankai.zip` (Emby attend ce nom pour le reconnaître comme plugin compatible)

3. **Extraction et placement**  
   - Décompressez l’archive.
   - Copiez le dossier extrait dans le dossier `plugins` de votre installation Emby.

   - **Windows** :  
     `C:\ProgramData\Emby-Server\plugins`  
     ou `%APPDATA%\Emby-Server\plugins`
   - **Linux (Docker)** :  
     Volume monté, ex : `/config/plugins` ou `/data/embysrv/plugins`
   - **Linux (natif)** :  
     `/var/lib/emby/plugins`

4. **Redémarrage**  
   - Redémarrez Emby pour charger le plugin.

5. **Activation**  
   - Allez dans **Dashboard → Plugins** → **Installed Plugins**.
   - Trouvez **Fankai** et activez-le.

> ✅ **Note** : Emby ne mettra pas à jour ce plugin automatiquement. Vous devrez répéter cette procédure à chaque nouvelle version.

---

## **Utilisation**

### **Pour Jellyfin**

1. **Configuration de la bibliothèque**
   - Administration → Bibliothèques.
   - Sélectionnez la bibliothèque (ex : "Séries TV" ou "Animés" ou encore mieux **"Fankai"**).
   - Cliquez sur les trois points (...) → Gérer la bibliothèque.
   - Dans "Récupérateurs de métadonnées", cochez **Fankai** et placez-le en priorité si souhaité.

2. **Scan de la bibliothèque**
   - Lancez un scan des métadonnées pour que Jellyfin utilise le plugin Fankai.
   - Pour les nouvelles séries, l’import est automatique ; pour les existantes, une actualisation peut être nécessaire.

---

### **Pour Emby**

1. **Configuration de la bibliothèque**
   - Allez dans **Dashboard → Libraries**.
   - Sélectionnez votre bibliothèque (ex : "TV Shows" ou "Anime").
   - Cliquez sur **Edit** → **Metadata**.
   - Dans **Metadata Providers**, cochez **Fankai** et placez-le en haut de la liste si vous souhaitez le prioriser.

2. **Scan de la bibliothèque**
   - Lancez un **Refresh Metadata** pour forcer Emby à utiliser le plugin Fankai.
   - Pour les séries existantes, vous pouvez aussi cliquer sur **Refresh Metadata** pour chaque série individuellement.

---

## **Ressources Utiles**

- [Site Officiel Fankai.fr](https://fankai.fr)
- [Wiki Fandom Fankai](https://discord.gg/team-fankai-414117314418704414)[8]
- [API Metadata Fankai](https://metadata.fankai.fr)
- [Discord Team Fankai](https://discord.gg/fankai)

---

## **Contribuer**

Les contributions sont les bienvenues !  
- Forkez le projet :  
  `https://gitlab.com/ElPouki/fankai_jellyfin/-/forks/new`
- Créez une branche :  
  `git checkout -b feature/AmazingFeature`
- Commitez vos modifications :  
  `git commit -m 'Add some AmazingFeature'`
- Poussez sur votre branche :  
  `git push origin feature/AmazingFeature`
- Ouvrez une **Merge Request**

---

## **Problèmes et Suggestions**

Pour tout bug ou suggestion, ouvrez une [**Issue**](https://github.com/Nackophilz/fankai_jellyfin/issues/new) sur Github.

---

## **Licence**

Ce projet est distribué sous la licence **MIT**.

```
MIT License

Copyright (c) 2024 ElPouki

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.
```

Cette licence permet aux utilisateurs de :
- Utiliser le code librement
- Modifier le code source
- Contribuer au projet
- Distribuer le code

Tout en exigeant de :
- Préserver les mentions de copyright
- Inclure la licence MIT dans toute copie

---

**Astuce** : Pour plus d’informations sur la gestion des plugins Jellyfin, consultez la [documentation officielle](https://jellyfin.org/docs/general/server/plugins/)[1][4].

[1] https://jellyfin.org/docs/  
[2] https://github.com/elmehdou/FanKai-Jellyfin  
[3] https://gitlab.com/ElPouki/fankai_pack  
[4] https://jellyfin.org/docs/general/server/plugins/  
[5] https://github.com/elmehdou/FanKai-Jellyfin/blob/master/jellyfin.cpp  
[6] https://docs.ikaros.run/en/docs/0.8/plugins/plugin-jellyfin/  
[7] https://www.youtube.com/watch?v=F8k_nvatKZE  
[8] https://fan-kai.fandom.com/fr/wiki/Emby

--- 

✅ Vous pouvez désormais utiliser le plugin Fankai sur **Jellyfin** et **Emby** !
