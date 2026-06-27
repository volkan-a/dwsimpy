# dwsimpy

Python bindings for the DWSIM headless automation engine through
pythonnet/CoreCLR.

This repository packages the curated DWSIM managed runtime together with
platform native libraries for:

- macOS arm64
- Linux x86_64
- Windows x86_64

## Install From GitHub

Download the wheel for your platform from the GitHub Actions artifacts or from
the latest GitHub Release:

- `dwsimpy-*-macosx_11_0_arm64.whl` for Apple Silicon macOS
- `dwsimpy-*-manylinux_x86_64.whl` for Linux x86_64
- `dwsimpy-*-win_amd64.whl` for Windows x86_64

Then install it:

```bash
python -m pip install ./dwsimpy-1.0.0-py3-none-macosx_11_0_arm64.whl
```

The machine must have a compatible .NET runtime installed. If `dotnet` is not
on `PATH`, set `DOTNET_ROOT` to the directory containing the `dotnet`
executable.

## Quick Start

```python
import dwsimpy

sim = dwsimpy.Automation()
print(len(sim.available_compounds))
print(len(sim.available_property_packages))

fs = sim.load_flowsheet("my_simulation.dwxml")
errors = sim.solve(fs)
if errors:
    raise RuntimeError(errors)

outlet = fs.get_object("OUT")
print(outlet.get_property("Temperature"))
sim.close(fs)
```

## Build Wheels On GitHub

The `Build Native Libraries and Wheels` workflow builds native libraries and
platform wheels for macOS arm64, Linux x86_64, and Windows x86_64.

Artifacts are uploaded for every workflow run:

- `wheels-macos-arm64`
- `wheels-linux-x86_64`
- `wheels-windows-x86_64`

To publish downloadable release assets:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The tag build attaches the three wheel files to the matching GitHub Release.

## Local Smoke Test

From this checkout on macOS arm64:

```bash
cd dwsimpy_package
PYTHONPATH=. python3 scripts/smoke_test.py --automation
```

Expected result: the import succeeds and DWSIM reports available compounds and
property packages.

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
native_libs_arm64/
  PetAz.c
.github/workflows/build-native.yml
```

## Notes

- The public Python API is `Automation`, `Flowsheet`, and `SimulationObject`.
- The managed DWSIM runtime payload is committed as package data; CI builds the
  native platform libraries and stages the correct platform files into each
  wheel.
