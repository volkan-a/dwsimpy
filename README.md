# dwsimpy

Python wheels for running the DWSIM headless automation engine through
pythonnet/CoreCLR.

`dwsimpy` is intended for scripts, notebooks, CI jobs, and optimization
workflows that need to load and solve existing DWSIM flowsheets from Python.

## What Is Bundled

The release wheels include:

- a curated DWSIM managed runtime payload
- platform native libraries for CoolProp, PetAz, and SkiaSharp
- Python wrappers for `Automation`, `Flowsheet`, and `SimulationObject`

You do not need to install the DWSIM desktop application to use a wheel.
You do need:

- Python 3.10 or newer
- a compatible .NET runtime available through `dotnet` or `DOTNET_ROOT`
- an existing `.dwxml` or `.dwxmz` flowsheet file to load

Current wheels target .NET/CoreCLR with the package runtime config in
`dwsimpy/.runtimeconfig.json`.

## .NET 10 Runtime Port Status

The current release hosts CoreCLR with a .NET 10 runtime config, but the curated
DWSIM managed payload is still based on legacy DWSIM assemblies. The next
runtime milestone is to replace that payload with first-party DWSIM assemblies
compiled for `.NETCoreApp,Version=v10.0` and remove desktop/legacy baggage such
as WinForms, Eto, IronPython, Microsoft.Scripting, and .NET Framework facade
assemblies.

Track the port plan in `docs/NET10_PORT_PLAN.md`.

The repo now also contains the first ported DWSIM leaf assemblies built as
SDK-style `.NETCoreApp,Version=v10.0` projects:

- `DWSIM.MathOps.SimpsonIntegrator`
- `DWSIM.MathOps.Mapack`
- `DWSIM.MathOps.RandomOps`
- `DWSIM.MathOps.SwarmOps`
- `DWSIM.MathOps.DotNumerics`

Audit the current payload with:

```bash
python3 dwsimpy_package/scripts/audit_managed_runtime.py
```

Audit a local ignored DWSIM source checkout with:

```bash
python3 dwsimpy_package/scripts/audit_dwsim_source.py --source-dir ./dwsim
```

## Supported Wheels

GitHub Releases provide one wheel per platform:

- Apple Silicon macOS: `dwsimpy-*-macosx_11_0_arm64.whl`
- Linux x86_64: `dwsimpy-*-manylinux_2_17_x86_64.manylinux2014_x86_64.whl`
- Windows x86_64: `dwsimpy-*-win_amd64.whl`

## Install

Download the wheel for your platform from the latest release:

https://github.com/volkan-a/dwsimpy/releases

Then install it with pip:

```bash
python -m pip install ./dwsimpy-1.0.1-py3-none-macosx_11_0_arm64.whl
```

You can also install directly from a release asset URL, for example:

```bash
python -m pip install \
  https://github.com/volkan-a/dwsimpy/releases/download/v1.0.1/dwsimpy-1.0.1-py3-none-macosx_11_0_arm64.whl
```

If `dotnet` is not on `PATH`, set `DOTNET_ROOT` to the directory containing the
`dotnet` executable.

## Quick Start

```python
from pathlib import Path
import dwsimpy

flowsheet_path = Path("my_simulation.dwxml")

sim = dwsimpy.Automation()
print("Compounds:", len(sim.available_compounds))
print("Property packages:", len(sim.available_property_packages))

fs = sim.load_flowsheet(flowsheet_path)

errors = sim.solve(fs)
if errors:
    raise RuntimeError(errors)

outlet = fs.get_object("OUT")
print("Outlet temperature:", outlet.get_property("Temperature"))

sim.close(fs)
```

Run the included example:

```bash
python examples/solve_existing_flowsheet.py path/to/my_simulation.dwxml
```

## Public API

The supported Python API is intentionally small:

- `dwsimpy.Automation()`
- `Automation.load_flowsheet(path)`
- `Automation.solve(flowsheet)`
- `Automation.close(flowsheet)`
- `Flowsheet.get_object(tag)`
- `Flowsheet.get_objects()`
- `SimulationObject.get_property(name)`
- `SimulationObject.set_property(name, value)`
- `SimulationObject.get_property_unit(name)`

`get_property()` and `set_property()` use DWSIM property names and SI units.

## Building Wheels

The GitHub Actions workflow builds native artifacts and platform wheels for:

- macOS arm64
- Linux x86_64
- Windows x86_64

To publish a release:

```bash
git tag v1.0.1
git push origin v1.0.1
```

The tag workflow attaches the built wheels to the matching GitHub Release.

## Repository Layout

```text
dwsimpy_package/
  dwsimpy/
    __init__.py
    .runtimeconfig.json
    libs/
  scripts/
    stage_platform_runtime.py
    audit_managed_runtime.py
    audit_dwsim_source.py
    validate_native_artifacts.py
    validate_wheel_contents.py
    smoke_test.py
docs/
  NET10_PORT_PLAN.md
src/
  DWSIM.MathOps.DotNumerics/
  DWSIM.MathOps.Mapack/
  DWSIM.MathOps.RandomOps/
  DWSIM.MathOps.SimpsonIntegrator/
  DWSIM.MathOps.SwarmOps/
  DwsimPy.Runtime/
  DwsimPy.Runtime.Cli/
  DwsimPy.MathOps.Tests/
  DwsimPy.Runtime.Tests/
examples/
  solve_existing_flowsheet.py
native_libs_arm64/
  PetAz.c
.github/workflows/build-native.yml
```

## License And Attribution

This repository redistributes DWSIM runtime binaries. DWSIM is developed by
Daniel Wagner and contributors and is licensed under the GNU General Public
License, version 3. See `LICENSE` and the upstream DWSIM source repository:

https://github.com/DanWBR/dwsim

This project is an experimental Python packaging layer for DWSIM automation and
is not an official DWSIM distribution.

## Experimental .NET 10 Runtime Slice

`src/DwsimPy.Runtime` is the start of a pure .NET 10 runtime boundary. It can
open, inspect, edit, and save `.dwxml/.dwxmz` documents without pythonnet,
Mono, WinForms, Eto, or IronPython. It also exposes a `UnitOperationRegistry`
for DWSIM palette/toolbox metadata and future custom unit operations. Solver
execution is not ported there yet.

Try the CLI:

```bash
dotnet run --project src/DwsimPy.Runtime.Cli -- inspect path/to/file.dwxmz
dotnet run --project src/DwsimPy.Runtime.Cli -- unitops
dotnet run --project src/DwsimPy.Runtime.Cli -- create /tmp/new.dwxml
dotnet run --project src/DwsimPy.Runtime.Cli -- add-node /tmp/new.dwxml MaterialStream Feed 40 80 /tmp/with-feed.dwxml
```

`unitops` returns JSON descriptors for the web UI palette: object type,
display name, category, simulation type, graphic type, connector counts, and
aliases. For example, `Mixer` resolves to DWSIM's canonical `NodeIn` type.

Run the .NET 10 runtime tests:

```bash
dotnet run --project src/DwsimPy.Runtime.Tests -c Release
dotnet run --project src/DwsimPy.MathOps.Tests -c Release
```
