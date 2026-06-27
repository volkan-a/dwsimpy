#!/usr/bin/env python3
from __future__ import annotations

import argparse
import zipfile
from pathlib import Path

from native_manifest import COMMON_MARKERS, KNOWN_NATIVE_FILES, REQUIRED_NATIVE


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Validate dwsimpy wheel runtime payload.")
    parser.add_argument("--platform", required=True, choices=sorted(REQUIRED_NATIVE))
    parser.add_argument("wheel", type=Path)
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    with zipfile.ZipFile(args.wheel) as zf:
        names = set(zf.namelist())

    required = {
        f"dwsimpy/libs/{name}"
        for name in (*COMMON_MARKERS, *REQUIRED_NATIVE[args.platform])
    }
    missing = sorted(name for name in required if name not in names)
    if missing:
        raise SystemExit("Wheel is missing required files:\n" + "\n".join(missing))

    allowed_native = set(REQUIRED_NATIVE[args.platform])
    forbidden = sorted(
        name
        for name in names
        if name.startswith("dwsimpy/libs/")
        and Path(name).name in KNOWN_NATIVE_FILES
        and Path(name).name not in allowed_native
    )
    if forbidden:
        raise SystemExit(
            "Wheel contains native files for another platform:\n" + "\n".join(forbidden)
        )

    print(f"{args.wheel.name}: runtime payload is valid for {args.platform}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
