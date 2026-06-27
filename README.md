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
    validate_native_artifacts.py
    validate_wheel_contents.py
    smoke_test.py
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
