# dwsimpy .NET 10 Runtime Port

## Goal

Make the runtime behind `dwsimpy` a headless .NET 10 runtime with no dependency on
Mono, .NET Framework assemblies, desktop UI assemblies, or compatibility stubs.

This is separate from the Python package surface. The Python API can continue to
exist, but it should load a managed runtime payload whose first-party DWSIM
assemblies target `.NETCoreApp,Version=v10.0`.

## Current State

The current wheel uses Python + pythonnet to host CoreCLR with a net10
`runtimeconfig.json`, but the shipped DWSIM assemblies are still legacy
`.NETFramework,Version=v4.6.2` assemblies.

Current non-headless baggage includes:

- `System.Windows.Forms.dll`
- `System.Drawing.Common.dll` as a local compatibility payload
- `Eto.dll`
- `IronPython*.dll`
- `Microsoft.Scripting*.dll`
- many `System.*.dll` facade/reference assemblies
- first-party `DWSIM.*.dll` assemblies targeting .NET Framework

Run the audit locally:

```bash
python3 dwsimpy_package/scripts/audit_managed_runtime.py
```

Make it strict when a net10 payload exists:

```bash
python3 dwsimpy_package/scripts/audit_managed_runtime.py --fail-on-legacy
```

If a local DWSIM source checkout is available under `./dwsim`, audit source
projects with:

```bash
python3 dwsimpy_package/scripts/audit_dwsim_source.py \
  --focus 'DWSIM\.(Interfaces|GlobalSettings|Math|SharedClasses|Thermodynamics|UnitOperations|FlowsheetBase|FlowsheetSolver|Automation)$'
```

This repo intentionally ignores `./dwsim`, so this audit is a local planning
tool unless an explicit source checkout is added to CI.

Current local source-audit findings:

- Every project in the runtime port queue still targets .NET Framework.
- `DWSIM.MathOps*` projects are the cleanest leaves: mostly target-framework
  migration with few or no UI blockers.
- `DWSIM.Interfaces` was the first real contract split: the net10 port keeps
  the solver/automation contracts while replacing desktop WinForms and Drawing
  signatures with `Object`.
- `DWSIM.GlobalSettings` has been ported as a headless settings/state assembly
  without Python.Runtime, Cudafy, DWSIM.Logging, or the legacy Nini DLL.
- The old `DWSIM.SharedClasses` project still carries heavy editor/form/drawing
  references. The net10 port has started as a small headless subset and should
  continue by adding only runtime-safe classes.
- `DWSIM.Thermodynamics`, `DWSIM.UnitOperations`, and `DWSIM.FlowsheetBase`
  carry heavy editor/form/drawing references and should not be ported by
  blindly retargeting the old projects.
- `DWSIM.FlowsheetSolver` is comparatively small but still references the
  legacy script interpreter surface.

The first pure .NET 10 slice now lives in `src/DwsimPy.Runtime`:

- `DwsimDocument` opens `.dwxml` and `.dwxmz` files.
- Empty `.dwxml`/`.dwxmz` documents can be created.
- `UnitOperationRegistry` exposes DWSIM palette/toolbox metadata in a pure
  .NET 10 type:
  - built-in DWSIM unit operation object types
  - display names and aliases
  - simulation type and graphic type names
  - canonical graphic object type
  - connector counts and default sizes
  - a registration point for future custom unit operations
- `XmlFlowsheetEngine` extracts a graph from `GraphicObjects`.
- Existing `External` graphic nodes are resolved back to meaningful palette
  object types such as `SolarPanel` when the simulation type identifies them.
- Raw simulation-object scalar fields and `graphic.*` fields can be read and
  edited.
- Graph-level node operations are available:
  - add node
  - move node
  - rename node
  - connect nodes
  - delete node and clear related connectors
- Documents can be saved back as `.dwxml` or `.dwxmz`.
- Solver execution intentionally throws `NotSupportedException` until the
  headless solver path is ported.

The first upstream DWSIM contract/leaf assemblies now also live in `src/` as SDK-style
`.NETCoreApp,Version=v10.0` projects:

- `src/DWSIM.Interfaces`
- `src/DWSIM.GlobalSettings`
- `src/DWSIM.SharedClassesCSharp`
- `src/DWSIM.SharedClasses`
- `src/DWSIM.MathOps.SimpsonIntegrator`
- `src/DWSIM.MathOps`
- `src/DWSIM.MathOps.Mapack`
- `src/DWSIM.MathOps.RandomOps`
- `src/DWSIM.MathOps.SwarmOps`
- `src/DWSIM.MathOps.DotNumerics`

All ten keep their DWSIM assembly names and compile without Mono, .NET Framework,
WinForms, Eto, IronPython, or desktop drawing dependencies. The MathOps
assemblies are guarded by `src/DwsimPy.MathOps.Tests`; the interface, global
settings, shared C#, and shared VB contracts are guarded by
`src/DwsimPy.Runtime.Tests`.

`src/DWSIM.Interfaces` is a headless contract split. Methods that used to expose
desktop values such as `System.Windows.Forms.Form`, `System.Windows.Forms.UserControl`,
`System.Drawing.Bitmap`, or `System.Drawing.Image` now expose `Object` in this
runtime boundary. The runtime test runner checks those signatures by reflection.

`src/DWSIM.GlobalSettings` keeps the settings and solver state surface needed by
automation/solver code. Python.NET initialization is explicitly unsupported in
this pure net10 assembly; Python hosting stays outside the managed runtime
boundary.

`src/DWSIM.SharedClassesCSharp` currently includes the headless AI convergence
DTOs, solid particle size/distribution classes, and injectable file picker
service contracts. The old WinForms connection editor, resource bitmap wrapper,
Windows file picker dialog, and Simulate365 file management coupling remain
outside the pure runtime boundary.

`src/DWSIM.SharedClasses` currently includes the headless VB unit-system,
unit-conversion, dimension, flowsheet options/results, transition restore, new
data event args, and weather data classes. Optimization and petroleum assay
collections are kept as headless object collections until those concrete data
types are ported. The old editor forms, resource bitmap wrappers, update
checks, weather providers, IronPython snippets, and desktop helpers remain
outside the pure runtime boundary.

The main VB `DWSIM.MathOps` port intentionally does not carry legacy optional
solver adapters into the net10 source project:

- `IPOPTSolver.vb`, because it depends on the legacy `Cureos.Numerics` binary.
- `LibOptimizationWrappers/**`, because it depends on `LibOptimization`'s old
  `net35` assembly surface.

The remaining `LP_Solve.vb` file compiles as a P/Invoke declaration surface, but
the native `lpsolve55` runtime is not yet staged for dwsimpy wheels.

The CLI harness in `src/DwsimPy.Runtime.Cli` is for local validation:

```bash
dotnet run --project src/DwsimPy.Runtime.Cli -- unitops
dotnet run --project src/DwsimPy.Runtime.Cli -- inspect path/to/file.dwxmz
dotnet run --project src/DwsimPy.Runtime.Cli -- properties path/to/file.dwxml Air
dotnet run --project src/DwsimPy.Runtime.Cli -- set-property path/to/file.dwxml Air graphic.X 77 /tmp/out.dwxml
dotnet run --project src/DwsimPy.Runtime.Cli -- create /tmp/new.dwxml
dotnet run --project src/DwsimPy.Runtime.Cli -- add-node /tmp/new.dwxml MaterialStream Feed 40 80 /tmp/with-feed.dwxml
dotnet run --project src/DwsimPy.Runtime.Cli -- connect /tmp/flowsheet.dwxml Feed MIX-001 material /tmp/connected.dwxml
```

`unitops` returns JSON descriptors intended for a future React Flow toolbox.
Aliases are resolved by the runtime when adding nodes; for example, `Mixer`
creates canonical DWSIM `NodeIn` XML.

The no-dependency test runner in `src/DwsimPy.Runtime.Tests` guards the current
slice:

```bash
dotnet run --project src/DwsimPy.Runtime.Tests -c Release
```

It checks registry coverage for the DWSIM palette, alias resolution,
`.dwxml/.dwxmz` graph edit roundtrips, `External` graphic object resolution,
the headless `DWSIM.Interfaces` contract signatures, and `DWSIM.GlobalSettings`
platform/settings behavior. It also smoke-tests the headless
`DWSIM.SharedClassesCSharp` AI, solids, and file picker service surface plus the
`DWSIM.SharedClasses` unit-system, unit-conversion, dimension, flowsheet
options/results, transition restore, and weather data surface.

The MathOps test runner checks the first ported numerical assemblies:

```bash
dotnet run --project src/DwsimPy.MathOps.Tests -c Release
```

It currently covers Simpson integration, Mapack linear solves, Cholesky, LU,
eigenvalues, SVD, Brent root finding, MathNet-based interpolation,
DotNumerics/LAPACK linear solves, deterministic random generation, and a
SwarmOps benchmark optimization smoke path.

## Definition of Done

- First-party runtime DLLs shipped in wheels target `.NETCoreApp,Version=v10.0`.
- `dwsimpy_package/dwsimpy/libs` contains no `.NETFramework` assemblies.
- The packaged runtime contains no Mono bridge and no `mono` subprocess path.
- The packaged runtime contains no WinForms/Eto/IronPython/Microsoft.Scripting
  assemblies.
- Platform-native files are limited to the required native solver/property
  dependencies for the active wheel platform.
- Smoke tests still pass:
  - `import dwsimpy`
  - `dwsimpy.Automation().available_compounds`
  - load and solve `Carbon Combustion.dwxml`

## Port Milestones

1. **Inventory and guard**
   - Keep `audit_managed_runtime.py` in the repo.
   - Keep `audit_dwsim_source.py` in the repo for local source dependency
     inventory while `./dwsim` remains ignored.
   - Run it in CI as report-only while the current payload is legacy.
   - Flip it to `--fail-on-legacy` once the net10 payload is staged.

2. **Create SDK-style net10 projects**
   - Started from dependency leaves with low UI coupling:
     - `DWSIM.Interfaces`
     - `DWSIM.GlobalSettings`
     - `DWSIM.SharedClassesCSharp`
     - `DWSIM.SharedClasses`
     - `DWSIM.MathOps`
     - `DWSIM.MathOps.SimpsonIntegrator`
     - `DWSIM.MathOps.Mapack`
     - `DWSIM.MathOps.RandomOps`
     - `DWSIM.MathOps.SwarmOps`
     - `DWSIM.MathOps.DotNumerics`
   - Revisit the optional old solver adapters only after choosing native/managed
     replacements for IPOPT and LibOptimization.
   - Continue expanding the remaining headless contract/data classes:
     - `DWSIM.SharedClasses` charts, optimization, sensitivity analysis,
       exception processing, petroleum assay data, GHG emission summary, and
       runtime utility methods
   - Avoid carrying old `.vbproj/.csproj` desktop references forward.
   - Split UI-facing interfaces out of the headless contract. Even
     `DWSIM.Interfaces` currently exposes desktop types such as
     `System.Windows.Forms.Form`, `System.Windows.Forms.UserControl`,
     `System.Drawing.Bitmap`, and `System.Drawing.Image`.

3. **Remove UI dependencies from headless code**
   - Move UI editor forms out of headless projects.
   - Replace any needed UI types with small headless abstractions.
   - Do not ship `System.Windows.Forms.dll` stubs.

4. **Port thermodynamics and serialization**
   - Port `DWSIM.Thermodynamics` without editor forms.
   - Keep resource files embedded or generated as managed resources.
   - Decide whether CoolProp is required for the first net10 cut.

5. **Port flowsheet solve path**
   - Port `DWSIM.FlowsheetBase`, `DWSIM.FlowsheetSolver`, and the built-in
     `DWSIM.UnitOperations` set needed for full flowsheet compatibility.
   - Do not exclude any real DWSIM unit operation from the headless runtime
     boundary. Keep all built-in unit operations represented in
     `UnitOperationRegistry`, then mark calculation support as ready only after
     the corresponding solver/default-initialization path is ported.
   - Keep the registry extensible so custom unit operations can be registered
     by object type, display name, runtime type, graphic type, connector model,
     and default geometry.
   - Keep drawing/SkiaSharp out unless a solve path strictly requires it.

6. **Port automation facade**
   - Recreate the small API that `dwsimpy` needs:
     - load `.dwxml/.dwxmz`
     - solve
     - list objects
     - get/set object properties
     - close/dispose

6a. **Bridge XML runtime to solver runtime**
   - Keep XML graph/document manipulation independent from solver execution.
   - Let the web UI operate on the XML/runtime graph DTOs.
   - Use the solver facade only for calculation and validated DWSIM object
     mutations that cannot be represented safely as direct XML edits.
   - Treat graph-only nodes created by `XmlFlowsheetEngine` as draft model
     edits until the solver facade validates and initializes their
     DWSIM-specific defaults.

7. **Replace wheel payload**
   - Stage only the net10 managed output and platform-native files.
   - Enable `audit_managed_runtime.py --fail-on-legacy` in CI.

## Proposed Runtime Boundary

The future runtime should expose a small .NET API that is independent of Python
and independent of the web UI:

```csharp
public interface IFlowsheetEngine
{
    IReadOnlyList<UnitOperationDescriptor> UnitOperations { get; }
    LoadedFlowsheet Load(byte[] document, string fileName);
    SolveResult Solve(string flowsheetId);
    FlowsheetGraph GetGraph(string flowsheetId);
    IReadOnlyDictionary<string, object?> GetProperties(string flowsheetId, string objectId);
    void SetProperty(string flowsheetId, string objectId, string propertyName, object? value);
    FlowsheetNode AddNode(string flowsheetId, AddNodeRequest request);
    FlowsheetEdge Connect(string flowsheetId, string sourceId, string targetId, string streamType);
}
```

Python, ZeroMQ workers, and future web APIs should all call this boundary rather
than reaching directly into many DWSIM assemblies.
