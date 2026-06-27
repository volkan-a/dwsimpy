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
`.dwxml/.dwxmz` graph edit roundtrips, and `External` graphic object resolution.

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
   - Run it in CI as report-only while the current payload is legacy.
   - Flip it to `--fail-on-legacy` once the net10 payload is staged.

2. **Create SDK-style net10 projects**
   - Start from dependency leaves:
     - `DWSIM.Interfaces`
     - `DWSIM.GlobalSettings`
     - `DWSIM.MathOps*`
     - `DWSIM.SharedClassesCSharp`
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
