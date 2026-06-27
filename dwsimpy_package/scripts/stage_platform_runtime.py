#!/usr/bin/env python3
from __future__ import annotations

import argparse
import shutil
from pathlib import Path

from native_manifest import COMMON_MARKERS, KNOWN_NATIVE_FILES, PLATFORM_DIRS, REQUIRED_NATIVE


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Stage one platform's native DWSIM runtime files into dwsimpy/libs."
    )
    parser.add_argument("--platform", required=True, choices=sorted(REQUIRED_NATIVE))
    parser.add_argument("--native-dir", required=True, type=Path)
    parser.add_argument(
        "--package-dir",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help="Path to dwsimpy_package.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    libs_dir = args.package_dir / "dwsimpy" / "libs"
    native_dir = args.native_dir

    if not libs_dir.is_dir():
        raise SystemExit(f"Missing runtime directory: {libs_dir}")
    if not native_dir.is_dir():
        raise SystemExit(f"Missing native artifact directory: {native_dir}")

    missing_common = [name for name in COMMON_MARKERS if not (libs_dir / name).is_file()]
    if missing_common:
        raise SystemExit(
            "Missing common DWSIM runtime files in "
            f"{libs_dir}: {', '.join(missing_common)}"
        )

    for name in KNOWN_NATIVE_FILES:
        path = libs_dir / name
        if path.exists():
            path.unlink()

    for dirname in PLATFORM_DIRS:
        path = libs_dir / dirname
        if path.exists():
            shutil.rmtree(path)

    for name in REQUIRED_NATIVE[args.platform]:
        src = native_dir / name
        if not src.is_file():
            raise SystemExit(f"Missing native artifact for {args.platform}: {src}")
        shutil.copy2(src, libs_dir / name)

    print(f"Staged {args.platform} native runtime files into {libs_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
