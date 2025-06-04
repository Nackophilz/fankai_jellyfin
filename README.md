# Plugin Fankai pour Jellyfin

## **À propos du projet**

Le plugin **Fankai** pour Jellyfin enrichit votre expérience en intégrant les métadonnées et images de la communauté Fankai directement dans votre médiathèque. Il vise une intégration transparente et qualitative pour les passionnés de la team "Fan-Kai" et les utilisateurs de Jellyfin, en particulier pour les séries animées. Les informations sont récupérées via l’API publique [metadata.fankai.fr](https://metadata.fankai.fr).

---

## **Fonctionnalités**

- **Récupération de métadonnées complètes** : titres, résumés, épisodes, dates de diffusion, etc.
- **Intégration d’images** : affiches, bannières, fonds d’écran, vignettes d’épisodes.
- **Informations supplémentaires** : genres, acteurs/personnages, studios, thèmes musicaux (en cours d'implémentation).
- **Mises à jour automatiques** : selon la configuration du catalogue de plugins Jellyfin.
- **Spécialisé pour les contenus asiatiques** : optimisé pour les animés et productions suivies par la communauté Fankai.

---

## **Installation**

### **Méthode 1 : Via le catalogue de plugins Jellyfin (recommandé)**

1. Depuis Jellyfin, allez dans **Tableau de bord → Plugins**.
2. Cliquez sur l’onglet **Catalogue**.
3. Recherchez **Fankai** dans la liste.
4. *(Si non disponible, ajoutez le dépôt personnalisé : [LIEN_DU_DEPOT_A_AJOUTER_QUAND_DISPONIBLE_SUIS_PAS_TRES_RAPIDE])*
5. Cliquez sur le plugin Fankai puis sur **Installer**.
6. Redémarrez Jellyfin si demandé.

> Les mises à jour seront ensuite gérées automatiquement via ce catalogue[1][4][7].

---

### **Méthode 2 : Installation manuelle**

#### **Téléchargement**

- Rendez-vous sur la page **Releases** du projet.
- Téléchargez la dernière version du fichier `Jellyfin.Plugin.Fankai.zip`.

#### **Extraction et placement**

- Décompressez l’archive.
- Copiez le dossier extrait dans le dossier `plugins` de votre installation Jellyfin.

  - **Windows** :  
    `C:\Program Files\Jellyfin\Server\plugins`  
    ou `%LOCALAPPDATA%\jellyfin\plugins`
  - **Linux (Docker)** :  
    Volume monté, ex : `/config/plugins` ou `/data/jellyfin/plugins`
  - **Linux (natif)** :  
    `/var/lib/jellyfin/plugins` ou `/usr/lib/jellyfin/plugins`

#### **Redémarrage**

- Arrêtez puis redémarrez Jellyfin pour charger le plugin.

#### **Configuration (si nécessaire)**

- Accédez au **Tableau de bord → Plugins**.
- Cliquez sur Fankai pour accéder aux options de configuration (ordre des fournisseurs de métadonnées, etc.)[1][4].

---

## **Utilisation**

1. **Configuration de la bibliothèque**
   - Administration → Bibliothèques.
   - Sélectionnez la bibliothèque (ex : "Séries TV" ou "Animés" ou encore mieux **"Fankai"**).
   - Cliquez sur les trois points (...) → Gérer la bibliothèque.
   - Dans "Récupérateurs de métadonnées", cochez **Fankai** et placez-le en priorité si souhaité.

2. **Scan de la bibliothèque**
   - Lancez un scan des métadonnées pour que Jellyfin utilise le plugin Fankai.
   - Pour les nouvelles séries, l’import est automatique ; pour les existantes, une actualisation peut être nécessaire.

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
