#!/usr/bin/env python3
from __future__ import annotations

import argparse

import dwsimpy


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Smoke-test an installed dwsimpy wheel.")
    parser.add_argument(
        "--automation",
        action="store_true",
        help="Instantiate Automation; requires a compatible local .NET runtime.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    print("import dwsimpy: ok")

    if args.automation:
        sim = dwsimpy.Automation()
        print(f"available compounds: {len(sim.available_compounds)}")
        print(f"available property packages: {len(sim.available_property_packages)}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
