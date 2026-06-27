#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Iterable


SOURCE_EXTENSIONS = {".cs", ".vb"}

RUNTIME_PORT_QUEUE = (
    "DWSIM.Interfaces",
    "DWSIM.GlobalSettings",
    "DWSIM.MathOps",
    "DWSIM.MathOps.DotNumerics",
    "DWSIM.MathOps.Mapack",
    "DWSIM.MathOps.RandomOps",
    "DWSIM.MathOps.SimpsonIntegrator",
    "DWSIM.MathOps.SwarmOps",
    "DWSIM.SharedClassesCSharp",
    "DWSIM.SharedClasses",
    "DWSIM.XMLSerializer",
    "DWSIM.Thermodynamics",
    "DWSIM.Thermodynamics.CoolPropInterface",
    "DWSIM.Thermodynamics.ThermoC",
    "DWSIM.UnitOperations",
    "DWSIM.FlowsheetBase",
    "DWSIM.FlowsheetSolver",
    "DWSIM.Automation",
)

SOURCE_PATTERNS = {
    "winforms": (
        re.compile(r"\bSystem\.Windows\.Forms\b", re.IGNORECASE),
        re.compile(r"\bInherits\s+(Form|UserControl)\b", re.IGNORECASE),
        re.compile(r"\bAs\s+(Form|UserControl|DataGridView|Control)\b", re.IGNORECASE),
    ),
    "eto": (
        re.compile(r"\bEto(\.|$)", re.IGNORECASE),
        re.compile(r"\bDWSIM\.ExtensionMethods\.Eto\b", re.IGNORECASE),
    ),
    "ironpython": (
        re.compile(r"\bIronPython\b", re.IGNORECASE),
        re.compile(r"\bMicrosoft\.Scripting\b", re.IGNORECASE),
        re.compile(r"\bMicrosoft\.Dynamic\b", re.IGNORECASE),
    ),
    "drawing": (
        re.compile(r"\bSystem\.Drawing\b", re.IGNORECASE),
        re.compile(r"\bBitmap\b", re.IGNORECASE),
        re.compile(r"\bImage\b", re.IGNORECASE),
    ),
    "skia": (
        re.compile(r"\bSkiaSharp\b", re.IGNORECASE),
        re.compile(r"\bSK(Canvas|Bitmap|Image|Paint|Path|Point|Rect|Size)\b", re.IGNORECASE),
    ),
    "desktop-ui": (
        re.compile(r"\bWPF\b", re.IGNORECASE),
        re.compile(r"\bGTK\b", re.IGNORECASE),
        re.compile(r"\bOxyPlot\b", re.IGNORECASE),
        re.compile(r"\bZedGraph\b", re.IGNORECASE),
    ),
}

REFERENCE_PATTERNS = {
    "winforms": re.compile(r"System\.Windows\.Forms", re.IGNORECASE),
    "eto": re.compile(r"(^|[.])Eto($|[.])|DWSIM\.ExtensionMethods\.Eto", re.IGNORECASE),
    "ironpython": re.compile(r"IronPython|Microsoft\.Scripting|Microsoft\.Dynamic", re.IGNORECASE),
    "drawing": re.compile(r"System\.Drawing", re.IGNORECASE),
    "skia": re.compile(r"SkiaSharp|DWSIM\.Drawing", re.IGNORECASE),
    "desktop-ui": re.compile(r"WPF|GTK|OxyPlot|ZedGraph", re.IGNORECASE),
}


@dataclass(frozen=True)
class SourceHit:
    category: str
    file: str
    line: int
    text: str


@dataclass(frozen=True)
class ProjectAudit:
    name: str
    path: str
    language: str
    sdk_style: bool
    target_frameworks: tuple[str, ...]
    references: tuple[str, ...]
    package_references: tuple[str, ...]
    project_references: tuple[str, ...]
    source_file_count: int
    issue_counts: dict[str, int]
    sample_hits: tuple[SourceHit, ...]

    @property
    def issue_total(self) -> int:
        return sum(self.issue_counts.values())


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Audit DWSIM source projects for pure .NET 10/headless port readiness."
    )
    parser.add_argument(
        "--source-dir",
        type=Path,
        default=Path(__file__).resolve().parents[2] / "dwsim",
        help="Path to a local DWSIM source checkout. Defaults to <repo>/dwsim.",
    )
    parser.add_argument(
        "--focus",
        action="append",
        default=[],
        help="Only print matching project names in the main table. Can be repeated.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Emit machine-readable JSON instead of tables.",
    )
    parser.add_argument(
        "--max-hits",
        type=int,
        default=4,
        help="Maximum sample source hits to retain per project.",
    )
    parser.add_argument(
        "--fail-on-issues",
        action="store_true",
        help="Exit non-zero if focused projects have headless blockers.",
    )
    return parser.parse_args()


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def child_texts(root: ET.Element, tag_name: str) -> list[str]:
    result = []
    for element in root.iter():
        if local_name(element.tag) == tag_name and element.text:
            text = element.text.strip()
            if text:
                result.append(text)
    return result


def include_attrs(root: ET.Element, tag_name: str) -> list[str]:
    result = []
    for element in root.iter():
        if local_name(element.tag) == tag_name:
            include = element.attrib.get("Include") or element.attrib.get("Update")
            if include:
                result.append(include)
    return result


def split_frameworks(root: ET.Element) -> tuple[str, ...]:
    frameworks: list[str] = []
    frameworks.extend(child_texts(root, "TargetFramework"))
    for value in child_texts(root, "TargetFrameworks"):
        frameworks.extend(part.strip() for part in value.split(";") if part.strip())
    frameworks.extend(child_texts(root, "TargetFrameworkVersion"))
    return tuple(dict.fromkeys(frameworks)) or ("<unknown>",)


def project_language(path: Path) -> str:
    if path.suffix.lower() == ".vbproj":
        return "vb"
    if path.suffix.lower() == ".csproj":
        return "cs"
    return path.suffix.lower().lstrip(".")


def normalize_ref(value: str) -> str:
    return value.split(",", 1)[0].strip()


def source_files(project_path: Path, root: ET.Element) -> list[Path]:
    project_dir = project_path.parent
    includes = include_attrs(root, "Compile")
    files: list[Path] = []

    for include in includes:
        if "*" in include:
            continue
        candidate = (project_dir / include).resolve()
        if candidate.suffix.lower() in SOURCE_EXTENSIONS and candidate.is_file():
            files.append(candidate)

    if not files:
        files = [
            path.resolve()
            for path in project_dir.rglob("*")
            if path.suffix.lower() in SOURCE_EXTENSIONS
            and "bin" not in path.parts
            and "obj" not in path.parts
        ]

    return sorted(dict.fromkeys(files))


def scan_source(files: Iterable[Path], root_dir: Path, max_hits: int) -> tuple[Counter[str], tuple[SourceHit, ...]]:
    counts: Counter[str] = Counter()
    samples: list[SourceHit] = []

    for path in files:
        try:
            lines = path.read_text(encoding="utf-8-sig", errors="ignore").splitlines()
        except OSError:
            continue

        for line_number, line in enumerate(lines, start=1):
            for category, patterns in SOURCE_PATTERNS.items():
                if any(pattern.search(line) for pattern in patterns):
                    counts[f"source:{category}"] += 1
                    if len(samples) < max_hits:
                        samples.append(
                            SourceHit(
                                category=f"source:{category}",
                                file=str(path.relative_to(root_dir)),
                                line=line_number,
                                text=line.strip()[:160],
                            )
                        )
                    break

    return counts, tuple(samples)


def scan_references(references: Iterable[str]) -> Counter[str]:
    counts: Counter[str] = Counter()
    for reference in references:
        for category, pattern in REFERENCE_PATTERNS.items():
            if pattern.search(reference):
                counts[f"reference:{category}"] += 1
    return counts


def scan_target_frameworks(frameworks: Iterable[str]) -> Counter[str]:
    counts: Counter[str] = Counter()
    for framework in frameworks:
        normalized = framework.lower()
        if normalized.startswith("v4") or normalized.startswith("net4"):
            counts["target:netframework"] += 1
        elif normalized == "<unknown>":
            counts["target:unknown"] += 1
        elif normalized.startswith("netstandard"):
            counts["target:netstandard"] += 1
        elif normalized.startswith("netcoreapp"):
            counts["target:netcoreapp"] += 1
    return counts


def audit_project(project_path: Path, source_dir: Path, max_hits: int) -> ProjectAudit:
    root = ET.parse(project_path).getroot()
    references = tuple(sorted(dict.fromkeys(normalize_ref(value) for value in include_attrs(root, "Reference"))))
    package_references = tuple(sorted(dict.fromkeys(normalize_ref(value) for value in include_attrs(root, "PackageReference"))))
    project_references = tuple(
        sorted(
            dict.fromkeys(
                str((project_path.parent / value).resolve().relative_to(source_dir.resolve()))
                if (project_path.parent / value).resolve().is_relative_to(source_dir.resolve())
                else value
                for value in include_attrs(root, "ProjectReference")
            )
        )
    )
    frameworks = split_frameworks(root)
    sources = source_files(project_path, root)

    issue_counts: Counter[str] = Counter()
    issue_counts.update(scan_target_frameworks(frameworks))
    issue_counts.update(scan_references(references))
    issue_counts.update(scan_references(package_references))
    source_counts, sample_hits = scan_source(sources, source_dir.resolve(), max_hits)
    issue_counts.update(source_counts)

    relative_path = project_path.resolve().relative_to(source_dir.resolve())
    return ProjectAudit(
        name=project_path.stem,
        path=str(relative_path),
        language=project_language(project_path),
        sdk_style="Sdk" in ET.tostring(root, encoding="unicode")[:300],
        target_frameworks=frameworks,
        references=references,
        package_references=package_references,
        project_references=project_references,
        source_file_count=len(sources),
        issue_counts=dict(sorted(issue_counts.items())),
        sample_hits=sample_hits,
    )


def discover_projects(source_dir: Path) -> list[Path]:
    return sorted(
        [
            path
            for path in source_dir.rglob("*")
            if path.suffix.lower() in {".csproj", ".vbproj"}
            and "bin" not in path.parts
            and "obj" not in path.parts
        ],
        key=lambda p: str(p).lower(),
    )


def focus_filter(projects: list[ProjectAudit], focus: list[str]) -> list[ProjectAudit]:
    if not focus:
        return projects
    patterns = [re.compile(pattern, re.IGNORECASE) for pattern in focus]
    return [project for project in projects if any(pattern.search(project.name) for pattern in patterns)]


def print_table(projects: list[ProjectAudit]) -> None:
    if not projects:
        print("No matching projects.")
        return

    name_width = max(len(project.name) for project in projects)
    target_width = max(len(",".join(project.target_frameworks)) for project in projects)
    print(f"{'project'.ljust(name_width)}  {'target'.ljust(target_width)}  src  issues")
    print(f"{'-' * name_width}  {'-' * target_width}  ---  ------")
    for project in projects:
        issue_summary = ", ".join(f"{key}={value}" for key, value in project.issue_counts.items()) or "ok"
        print(
            f"{project.name.ljust(name_width)}  "
            f"{','.join(project.target_frameworks).ljust(target_width)}  "
            f"{str(project.source_file_count).rjust(3)}  "
            f"{issue_summary}"
        )


def print_port_queue(projects: list[ProjectAudit]) -> None:
    by_name = {project.name: project for project in projects}
    print()
    print("Runtime port queue:")
    for name in RUNTIME_PORT_QUEUE:
        project = by_name.get(name)
        if project is None:
            print(f"- {name}: missing")
            continue

        blockers = [
            key
            for key in project.issue_counts
            if key.startswith("source:")
            or key.startswith("reference:")
            or key in {"target:netframework", "target:unknown"}
        ]
        status = "ready-to-split" if blockers else "clean"
        if project.issue_counts.get("target:netframework"):
            status = "legacy-project"
        print(f"- {name}: {status}; {', '.join(blockers[:8]) or 'no blockers'}")


def print_hits(projects: list[ProjectAudit]) -> None:
    printed = False
    for project in projects:
        if not project.sample_hits:
            continue
        if not printed:
            print()
            print("Sample blockers:")
            printed = True
        print(f"{project.name}:")
        for hit in project.sample_hits:
            print(f"  - {hit.category} {hit.file}:{hit.line}: {hit.text}")


def emit_json(projects: list[ProjectAudit]) -> None:
    payload = [asdict(project) for project in projects]
    print(json.dumps(payload, indent=2, sort_keys=True))


def main() -> int:
    args = parse_args()
    source_dir = args.source_dir
    if not source_dir.is_dir():
        print(f"DWSIM source directory not found: {source_dir}", file=sys.stderr)
        print("Clone or copy DWSIM source into ./dwsim, or pass --source-dir.", file=sys.stderr)
        return 2 if args.fail_on_issues else 0

    project_paths = discover_projects(source_dir)
    audits = [audit_project(path, source_dir, args.max_hits) for path in project_paths]
    selected = focus_filter(audits, args.focus)

    if args.json:
        emit_json(selected)
    else:
        print(f"Audited {len(audits)} projects under {source_dir}.")
        print_table(selected)
        print_port_queue(audits)
        print_hits(selected)

    if args.fail_on_issues and any(project.issue_total for project in selected):
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
