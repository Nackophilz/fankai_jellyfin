from __future__ import annotations

import hashlib
import html
import os
import shutil
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT_DIR = ROOT / 'repo'
ADDON_ID = 'metadata.tvshows.fankai'
REPO_ID = 'repository.fankai'
SITE_URL = 'https://nackophilz.github.io/fankai_kodi/'

EXCLUDED_DIRS = {'__pycache__', '.pytest_cache', 'tests'}
EXCLUDED_SUFFIXES = ('.pyc', '.pyo')
ASSET_FILES = {
    ADDON_ID: ['resources/icon.png', 'resources/fanart.jpg'],
    REPO_ID: ['icon.png'],
}

PAGE = """<!DOCTYPE html>
<html lang="fr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<style>
body {{ margin: 0; padding: 2rem 1.25rem; background: #17171b; color: #e7e7ea;
       font: 15px/1.6 system-ui, -apple-system, "Segoe UI", sans-serif; }}
main {{ max-width: 46rem; margin: 0 auto; }}
h1 {{ font-size: 1.5rem; margin: 0 0 .25rem; }}
p.sub {{ color: #9a9aa4; margin: 0 0 2rem; }}
h2 {{ font-size: 1rem; margin: 2rem 0 .75rem; color: #ffd54a; }}
code {{ background: #24242b; padding: .15rem .4rem; border-radius: 4px; }}
.url {{ display: block; background: #24242b; border: 1px solid #34343d; border-radius: 6px;
        padding: .8rem 1rem; margin: 0 0 1rem; word-break: break-all; }}
ol {{ padding-left: 1.2rem; }}
li {{ margin: .35rem 0; }}
ul.files {{ list-style: none; padding: 0; }}
ul.files li {{ border-bottom: 1px solid #2a2a32; padding: .5rem 0; }}
a {{ color: #7cc4ff; }}
</style>
</head>
<body>
<main>
{body}
</main>
</body>
</html>
"""


def read_version(addon_dir: Path) -> str:
    return ET.parse(addon_dir / 'addon.xml').getroot().attrib['version']


def build_zip(addon_id: str) -> Path:
    src = ROOT / addon_id
    version = read_version(src)
    out = OUT_DIR / addon_id
    out.mkdir(parents=True, exist_ok=True)
    zip_path = out / '{}-{}.zip'.format(addon_id, version)

    with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED) as zf:
        for root, dirs, files in os.walk(src):
            dirs[:] = sorted(d for d in dirs if d not in EXCLUDED_DIRS)
            for name in sorted(files):
                if name.endswith(EXCLUDED_SUFFIXES):
                    continue
                path = Path(root) / name
                arcname = '{}/{}'.format(addon_id, path.relative_to(src).as_posix())
                zf.write(path, arcname)

    shutil.copy2(src / 'addon.xml', out / 'addon.xml')
    for relative in ASSET_FILES.get(addon_id, []):
        asset = src / relative
        if asset.exists():
            shutil.copy2(asset, out / asset.name)
    print('  {} v{} -> {}'.format(addon_id, version, zip_path.relative_to(ROOT)))
    return zip_path


def write_index(directory: Path, title: str, body: str) -> None:
    (directory / 'index.html').write_text(
        PAGE.format(title=html.escape(title), body=body), encoding='utf-8')


def listing(directory: Path) -> str:
    entries = sorted(p for p in directory.iterdir() if p.name != 'index.html')
    items = []
    for p in entries:
        size = '' if p.is_dir() else ' <span style="color:#9a9aa4">({} ko)</span>'.format(
            p.stat().st_size // 1024)
        name = p.name + ('/' if p.is_dir() else '')
        items.append('<li><a href="{0}">{0}</a>{1}</li>'.format(html.escape(name), size))
    return '<ul class="files">\n{}\n</ul>'.format('\n'.join(items))


def write_site(addon_version: str, repo_version: str) -> None:
    repo_zip = '{}-{}.zip'.format(REPO_ID, repo_version)
    body = """<h1>Dépôt Kodi Fankai</h1>
<p class="sub">Métadonnées des éditions Fan-Kai (Kaï, Henshū, Yabai…) pour Kodi 20 et plus.</p>

<h2>Installation</h2>
<ol>
<li>Kodi : <em>Paramètres → Gestionnaire de fichiers → Ajouter une source</em>, et coller cette adresse :</li>
</ol>
<code class="url">{url}</code>
<ol start="2">
<li>Nommer la source <code>fankai</code>.</li>
<li><em>Paramètres → Modules complémentaires → Installer depuis un fichier zip</em> → <code>fankai</code> → <code>{repo_zip}</code>.</li>
<li><em>Installer depuis un dépôt → Dépôt Fankai → Fournisseurs d'informations → Fournisseurs de séries TV → Fankai</em>.</li>
<li>Sur votre source vidéo : <em>Définir le contenu</em> → <strong>Séries TV</strong> → fournisseur <strong>Fankai</strong>.</li>
</ol>
<p>Le dépôt installé, les mises à jour de l'add-on se font ensuite toutes seules.
Si Kodi refuse le zip, activer <em>Paramètres → Système → Modules complémentaires → Sources inconnues</em>.</p>

<h2>Téléchargements</h2>
<ul class="files">
<li><a href="{repo_zip}">{repo_zip}</a> — le dépôt (à installer en premier)</li>
<li><a href="{addon_id}/{addon_id}-{addon_version}.zip">{addon_id}-{addon_version}.zip</a> — le scraper seul (mises à jour manuelles)</li>
</ul>

<h2>Fichiers</h2>
{files}

<p style="margin-top:2rem"><a href="https://github.com/Nackophilz/fankai_kodi">Code source</a></p>""".format(
        url=SITE_URL, repo_zip=repo_zip, addon_id=ADDON_ID,
        addon_version=addon_version, files=listing(OUT_DIR))
    write_index(OUT_DIR, 'Dépôt Kodi Fankai', body)

    for addon_id in (ADDON_ID, REPO_ID):
        directory = OUT_DIR / addon_id
        write_index(directory, addon_id,
                    '<h1>{0}</h1>\n{1}'.format(html.escape(addon_id), listing(directory)))


def write_addons_xml(addon_ids) -> None:
    root = ET.Element('addons')
    for addon_id in addon_ids:
        root.append(ET.parse(OUT_DIR / addon_id / 'addon.xml').getroot())
    index = OUT_DIR / 'addons.xml'
    ET.ElementTree(root).write(index, encoding='utf-8', xml_declaration=True)
    (OUT_DIR / 'addons.xml.md5').write_text(
        hashlib.md5(index.read_bytes()).hexdigest(), encoding='ascii')
    print('  addons.xml + addons.xml.md5 ({} add-ons)'.format(len(addon_ids)))


def main() -> int:
    for addon_id in (ADDON_ID, REPO_ID):
        if not (ROOT / addon_id / 'addon.xml').exists():
            print('addon.xml introuvable pour {}'.format(addon_id), file=sys.stderr)
            return 1
    if OUT_DIR.exists():
        shutil.rmtree(OUT_DIR)
    OUT_DIR.mkdir(parents=True)
    print('Construction du dépôt Kodi dans {}/'.format(OUT_DIR.relative_to(ROOT)))

    build_zip(ADDON_ID)
    repo_zip = build_zip(REPO_ID)
    write_addons_xml([ADDON_ID, REPO_ID])

    shutil.copy2(repo_zip, OUT_DIR / repo_zip.name)
    (OUT_DIR / '.nojekyll').write_text('', encoding='ascii')
    write_site(read_version(ROOT / ADDON_ID), read_version(ROOT / REPO_ID))
    print('  index.html + .nojekyll')
    return 0


if __name__ == '__main__':
    sys.exit(main())
