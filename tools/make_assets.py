from __future__ import annotations

import argparse
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ICON_SIZE = 512
FANART_SIZE = (1920, 1080)
ICON_BACKGROUND = (24, 24, 28, 255)
LOGO_RATIO = 0.78

OUTPUTS = {
    'icon': [ROOT / 'metadata.tvshows.fankai' / 'resources' / 'icon.png',
             ROOT / 'repository.fankai' / 'icon.png'],
    'fanart': [ROOT / 'metadata.tvshows.fankai' / 'resources' / 'fanart.jpg'],
}


def make_icon(logo_path: Path) -> Image.Image:
    logo = Image.open(logo_path).convert('RGBA')
    bbox = logo.getbbox()
    if bbox:
        logo = logo.crop(bbox)
    max_side = int(ICON_SIZE * LOGO_RATIO)
    scale = min(max_side / logo.width, max_side / logo.height)
    logo = logo.resize((round(logo.width * scale), round(logo.height * scale)), Image.LANCZOS)
    icon = Image.new('RGBA', (ICON_SIZE, ICON_SIZE), ICON_BACKGROUND)
    icon.alpha_composite(logo, ((ICON_SIZE - logo.width) // 2, (ICON_SIZE - logo.height) // 2))
    return icon


def make_fanart(background_path: Path) -> Image.Image:
    image = Image.open(background_path).convert('RGB')
    target_w, target_h = FANART_SIZE
    scale = max(target_w / image.width, target_h / image.height)
    image = image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)
    left = (image.width - target_w) // 2
    top = (image.height - target_h) // 2
    return image.crop((left, top, left + target_w, top + target_h))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--logo', type=Path, default=ROOT.parent / 'fankai_pack' / 'assets' / 'Logo_Fankai.png')
    parser.add_argument('--background', type=Path, default=ROOT.parent / 'fankai_pack' / 'assets' / 'fankai.png')
    args = parser.parse_args()
    for path in (args.logo, args.background):
        if not path.exists():
            print('source introuvable : {}'.format(path), file=sys.stderr)
            return 1

    icon = make_icon(args.logo)
    for target in OUTPUTS['icon']:
        target.parent.mkdir(parents=True, exist_ok=True)
        icon.save(target, 'PNG', optimize=True)
        print('  {} ({}x{})'.format(target.relative_to(ROOT), *icon.size))

    fanart = make_fanart(args.background)
    for target in OUTPUTS['fanart']:
        target.parent.mkdir(parents=True, exist_ok=True)
        fanart.save(target, 'JPEG', quality=88, optimize=True, progressive=True)
        print('  {} ({}x{})'.format(target.relative_to(ROOT), *fanart.size))
    return 0


if __name__ == '__main__':
    sys.exit(main())
