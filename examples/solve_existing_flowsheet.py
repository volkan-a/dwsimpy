#!/usr/bin/env python3
from __future__ import annotations

import argparse
from pathlib import Path

import dwsimpy


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Solve an existing DWSIM flowsheet.")
    parser.add_argument("flowsheet", type=Path, help="Path to a .dwxml or .dwxmz file")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    flowsheet = args.flowsheet.expanduser().resolve()
    if not flowsheet.is_file():
        raise SystemExit(f"Flowsheet not found: {flowsheet}")

    sim = dwsimpy.Automation()
    print(f"Available compounds: {len(sim.available_compounds)}")
    print(f"Available property packages: {len(sim.available_property_packages)}")

    fs = sim.load_flowsheet(flowsheet)
    try:
        print("\nObjects:")
        for tag, obj in fs.get_objects().items():
            print(f"  {tag:30s} {obj.type_name}")

        errors = sim.solve(fs)
        if errors:
            print("\nSolve errors:")
            for error in errors:
                print(f"  {error}")
            return 1

        print("\nMaterial stream results:")
        for tag, obj in fs.get_objects().items():
            if obj.type_name == "MaterialStream":
                temp = obj.get_property("Temperature")
                pressure = obj.get_property("Pressure")
                flow = obj.get_property("Molar Flow")
                print(f"  {tag:20s} T={temp} K  P={pressure} Pa  F={flow} mol/s")
    finally:
        sim.close(fs)

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
