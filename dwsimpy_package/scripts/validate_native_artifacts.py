#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

from native_manifest import REQUIRED_NATIVE


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate native DWSIM artifacts.")
    parser.add_argument("--platform", required=True, choices=sorted(REQUIRED_NATIVE))
    parser.add_argument("--native-dir", required=True, type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    missing = [
        str(args.native_dir / name)
        for name in REQUIRED_NATIVE[args.platform]
        if not (args.native_dir / name).is_file()
    ]
    if missing:
        raise SystemExit("Missing native artifacts:\n" + "\n".join(missing))

    for name in REQUIRED_NATIVE[args.platform]:
        print(args.native_dir / name)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
