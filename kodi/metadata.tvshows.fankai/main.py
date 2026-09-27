from __future__ import annotations

import sys
from typing import Dict, List, Tuple
from urllib.parse import parse_qsl

from lib.scraper import run_action


def parse_argv(argv: List[str]) -> Tuple[int, Dict[str, str]]:
    handle = int(argv[1]) if len(argv) > 1 else -1
    query = argv[2] if len(argv) > 2 else ''
    params = dict(parse_qsl(query.lstrip('?'), keep_blank_values=True))
    return handle, params


def main(argv: List[str]) -> None:
    handle, params = parse_argv(argv)
    run_action(handle, params.get('action'), params)


if __name__ == '__main__':
    main(sys.argv)
