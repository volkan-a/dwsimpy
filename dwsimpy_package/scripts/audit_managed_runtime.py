#!/usr/bin/env python3
from __future__ import annotations

import argparse
import re
from dataclasses import dataclass
from pathlib import Path


TFM_RE = re.compile(
    rb"\.(NETFramework|NETCoreApp|NETStandard),Version=v?([0-9]+(?:\.[0-9]+)*)"
)

FIRST_PARTY_PREFIXES = (
    "DWSIM.",
    "CapeOpen",
    "Interop.CAPEOPEN",
)

FORBIDDEN_HEADLESS_NAMES = {
    "System.Windows.Forms.dll",
    "netstandard.dll",
}

FORBIDDEN_HEADLESS_PREFIXES = (
    "IronPython",
    "Microsoft.Scripting",
    "Microsoft.Dynamic",
    "Eto",
)


@dataclass(frozen=True)
class AssemblyInfo:
    path: Path
    tfm: str | None
    issues: tuple[str, ...]

    @property
    def name(self) -> str:
        return self.path.name


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Audit dwsimpy managed runtime DLLs for net10/headless readiness."
    )
    parser.add_argument(
        "--libs-dir",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "dwsimpy" / "libs",
        help="Path to the staged dwsimpy/libs directory.",
    )
    parser.add_argument(
        "--fail-on-legacy",
        action="store_true",
        help="Exit non-zero if any legacy or headless-forbidden DLL is found.",
    )
    parser.add_argument(
        "--target-tfm",
        default=".NETCoreApp,Version=v10.0",
        help="Expected target framework for first-party DWSIM assemblies.",
    )
    return parser.parse_args()


def extract_tfm(path: Path) -> str | None:
    data = path.read_bytes()
    matches = TFM_RE.findall(data)
    if not matches:
        return None
    family, version = matches[-1]
    return f".{family.decode('ascii')},Version=v{version.decode('ascii')}"


def is_first_party(name: str) -> bool:
    return name.startswith(FIRST_PARTY_PREFIXES)


def audit_assembly(path: Path, target_tfm: str) -> AssemblyInfo:
    name = path.name
    tfm = extract_tfm(path)
    issues: list[str] = []

    if tfm is None:
        issues.append("missing-target-framework")
    elif tfm.startswith(".NETFramework"):
        issues.append("targets-netframework")
    elif is_first_party(name) and tfm != target_tfm:
        issues.append(f"first-party-not-{target_tfm}")

    if name in FORBIDDEN_HEADLESS_NAMES:
        issues.append("headless-forbidden-assembly")
    if name.startswith(FORBIDDEN_HEADLESS_PREFIXES):
        issues.append("headless-forbidden-prefix")

    return AssemblyInfo(path=path, tfm=tfm, issues=tuple(issues))


def print_table(rows: list[AssemblyInfo]) -> None:
    if not rows:
        print("No managed DLLs found.")
        return

    name_width = max(len(row.name) for row in rows)
    tfm_width = max(len(row.tfm or "<unknown>") for row in rows)
    print(f"{'assembly'.ljust(name_width)}  {'target-framework'.ljust(tfm_width)}  status")
    print(f"{'-' * name_width}  {'-' * tfm_width}  ------")
    for row in rows:
        status = "ok" if not row.issues else ", ".join(row.issues)
        print(f"{row.name.ljust(name_width)}  {(row.tfm or '<unknown>').ljust(tfm_width)}  {status}")


def main() -> int:
    args = parse_args()
    libs_dir = args.libs_dir
    if not libs_dir.is_dir():
        raise SystemExit(f"Managed runtime directory not found: {libs_dir}")

    rows = [
        audit_assembly(path, args.target_tfm)
        for path in sorted(libs_dir.glob("*.dll"), key=lambda p: p.name.lower())
    ]
    print_table(rows)

    failing = [row for row in rows if row.issues]
    if args.fail_on_legacy and failing:
        print()
        print("Legacy/headless-forbidden managed runtime files:")
        for row in failing:
            print(f"- {row.name}: {', '.join(row.issues)}")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
