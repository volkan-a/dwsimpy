"""
dwsimpy — DWSIM Chemical Process Simulation for Python (macOS ARM64)
====================================================================

Headless DWSIM automation engine exposed to Python via pythonnet/CoreCLR.

Usage:
    import dwsimpy

    sim = dwsimpy.Automation()
    fs = sim.load_flowsheet("my_simulation.dwxml")

    # Access objects by tag name
    inlet = fs.get_object("IN")
    inlet.set_property("Temperature", 500.0)
    inlet.set_property("Pressure", 101325.0)

    # Solve
    errors = sim.solve(fs)

    # Read results
    outlet = fs.get_object("OUT")
    T_out = outlet.get_property("Temperature")
    print(f"Outlet T = {T_out:.1f} K")

    sim.close(fs)
"""

import os
import sys
import platform
import shutil
from pathlib import Path

# ═══════════════════════════════════════════════════════════════════
# Paths
# ═══════════════════════════════════════════════════════════════════
_PACKAGE_DIR = Path(__file__).parent.resolve()
_LIBS_DIR = _PACKAGE_DIR / "libs"
_RUNTIME_CONFIG = _PACKAGE_DIR / ".runtimeconfig.json"

_REQUIRED_NATIVE = {
    "macos_arm64": ("libCoolProp.dylib", "libPetAz.dylib", "libSkiaSharp.dylib"),
    "linux_x86_64": ("libCoolProp.so", "libPetAz.so", "libSkiaSharp.so"),
    "windows_x86_64": ("CoolProp.dll", "PetAz.dll", "libSkiaSharp.dll"),
}

_MANAGED_RESOURCES = (
    "DWSIM.Thermodynamics.Strings.resources",
    "DWSIM.UnitOperations.Strings.resources",
    "DWSIM.FlowsheetBase.Strings.resources",
    "DWSIM.FlowsheetBase.Properties.resources",
)

# ═══════════════════════════════════════════════════════════════════
# Runtime initialization (runs once on import)
# ═══════════════════════════════════════════════════════════════════
_initialized = False
_automation = None
_dll_directory_handles = []
_resource_managers = {}


def _platform_key():
    system = platform.system().lower()
    machine = platform.machine().lower()

    if system == "darwin" and machine in {"arm64", "aarch64"}:
        return "macos_arm64"
    if system == "linux" and machine in {"x86_64", "amd64"}:
        return "linux_x86_64"
    if system == "windows" and machine in {"amd64", "x86_64"}:
        return "windows_x86_64"

    raise RuntimeError(
        "Unsupported platform for dwsimpy native runtime: "
        f"{platform.system()} {platform.machine()}. Supported targets are "
        "macOS arm64, Linux x86_64, and Windows x86_64."
    )


def _runtime_dirs(platform_name):
    candidates = [_LIBS_DIR]
    for dirname in ("native", platform_name):
        path = _LIBS_DIR / dirname
        if path.is_dir():
            candidates.append(path)
    return candidates


def _prepend_env_path(name, paths):
    current = os.environ.get(name)
    value = os.pathsep.join(str(path) for path in paths)
    os.environ[name] = value if not current else value + os.pathsep + current


def _validate_runtime_payload(platform_name, dirs):
    if not _LIBS_DIR.is_dir():
        raise RuntimeError(f"DWSIM runtime directory not found: {_LIBS_DIR}")
    if not _RUNTIME_CONFIG.is_file():
        raise RuntimeError(f"CoreCLR runtime config not found: {_RUNTIME_CONFIG}")

    missing = []
    for native_name in _REQUIRED_NATIVE[platform_name]:
        if not any((path / native_name).is_file() for path in dirs):
            missing.append(native_name)
    if missing:
        raise RuntimeError(
            "Missing dwsimpy native runtime files for "
            f"{platform_name}: {', '.join(missing)}. Rebuild or restage the "
            "platform wheel runtime payload."
        )

    missing_resources = [
        name for name in _MANAGED_RESOURCES if not (_LIBS_DIR / name).is_file()
    ]
    if missing_resources:
        raise RuntimeError(
            "Missing dwsimpy managed resource files: "
            f"{', '.join(missing_resources)}. Rebuild or restage the "
            "platform wheel runtime payload."
        )


def _configure_native_search_paths(dirs):
    global _dll_directory_handles

    path_dirs = [path for path in dirs if path.is_dir()]
    if platform.system().lower() == "windows":
        _prepend_env_path("PATH", path_dirs)
        if hasattr(os, "add_dll_directory"):
            for path in path_dirs:
                _dll_directory_handles.append(os.add_dll_directory(str(path)))
    elif platform.system().lower() == "darwin":
        _prepend_env_path("DYLD_LIBRARY_PATH", path_dirs)
    else:
        _prepend_env_path("LD_LIBRARY_PATH", path_dirs)

    for path in reversed(path_dirs):
        path_str = str(path)
        if path_str not in sys.path:
            sys.path.insert(0, path_str)


def _file_resource_manager(base_name):
    manager = _resource_managers.get(base_name)
    if manager is None:
        from System.Resources import ResourceManager
        manager = ResourceManager.CreateFileBasedResourceManager(
            base_name, str(_LIBS_DIR), None
        )
        _resource_managers[base_name] = manager
    return manager


def _configure_static_resource_managers():
    from DWSIM.Thermodynamics import Calculator
    from DWSIM.UnitOperations import ResMan

    Calculator._ResourceManager = _file_resource_manager(
        "DWSIM.Thermodynamics.Strings"
    )
    ResMan._ResourceManager = _file_resource_manager("DWSIM.UnitOperations.Strings")


def _invoke_if_present(obj, method_name, *args):
    method = obj.GetType().GetMethod(method_name)
    if method is not None:
        method.Invoke(obj, args)


def _configure_flowsheet_resource_managers(fs):
    _invoke_if_present(
        fs,
        "SetResourcesManager",
        _file_resource_manager("DWSIM.FlowsheetBase.Strings"),
    )
    _invoke_if_present(
        fs,
        "SetPropertyResourcesManager",
        _file_resource_manager("DWSIM.FlowsheetBase.Properties"),
    )


def _init_runtime():
    global _initialized
    if _initialized:
        return

    platform_name = _platform_key()
    runtime_dirs = _runtime_dirs(platform_name)
    _validate_runtime_payload(platform_name, runtime_dirs)
    _configure_native_search_paths(runtime_dirs)

    os.environ["PYTHONNET_RUNTIME"] = "coreclr"
    os.environ["PYTHONNET_CORECLR_RUNTIME_CONFIG"] = str(_RUNTIME_CONFIG)
    os.environ["PYTHONNET_CORECLR_DOTNET_ROOT"] = _find_dotnet_root()

    # NOTE: do NOT os.chdir(libs) — breaks user file paths

    from pythonnet import load
    load("coreclr")
    import clr

    # Stub assemblies (must be loaded first)
    clr.AddReference(str(_LIBS_DIR / "System.Windows.Forms.dll"))
    clr.AddReference(str(_LIBS_DIR / "System.Drawing.Common.dll"))

    # DWSIM assemblies
    _DWSIM_ASMS = [
        "DWSIM.Interfaces", "DWSIM.GlobalSettings", "DWSIM.SharedClasses",
        "DWSIM.SharedClassesCSharp", "DWSIM.MathOps", "DWSIM.XMLSerializer",
        "DWSIM.Thermodynamics.CoolPropInterface", "DWSIM.Thermodynamics",
        "DWSIM.Thermodynamics.AdvancedEOS.GERG2008",
        "DWSIM.Thermodynamics.AdvancedEOS.PCSAFT2",
        "DWSIM.Thermodynamics.AdvancedEOS.PRSRKTDep",
        "DWSIM.UnitOperations", "DWSIM.FlowsheetBase", "DWSIM.FlowsheetSolver",
        "DWSIM.Logging", "DWSIM.ExtensionMethods", "DWSIM.Inspector",
        "DWSIM.Drawing.SkiaSharp", "DWSIM.DrawingTools.Point",
        "DWSIM.DynamicsManager", "DWSIM.Automation",
    ]
    for asm in _DWSIM_ASMS:
        asm_path = _LIBS_DIR / f"{asm}.dll"
        clr.AddReference(str(asm_path) if asm_path.is_file() else asm)

    _configure_static_resource_managers()
    _initialized = True


def _find_dotnet_root():
    candidates = [
        os.environ.get("DOTNET_ROOT"),
        os.environ.get("DOTNET_ROOT_X64"),
        "/opt/homebrew/opt/dotnet/libexec",
        "/usr/local/share/dotnet",
        "/usr/share/dotnet",
        os.environ.get("ProgramFiles") and os.path.join(os.environ["ProgramFiles"], "dotnet"),
    ]
    dotnet_on_path = shutil.which("dotnet")
    if dotnet_on_path:
        candidates.append(str(Path(dotnet_on_path).parent))

    exe_name = "dotnet.exe" if platform.system().lower() == "windows" else "dotnet"
    for c in candidates:
        if c and os.path.isfile(os.path.join(c, exe_name)):
            return c
    raise RuntimeError(
        ".NET runtime not found. Install .NET 10 or set DOTNET_ROOT to the "
        "directory containing the dotnet executable."
    )


# ═══════════════════════════════════════════════════════════════════
# Public API
# ═══════════════════════════════════════════════════════════════════

class Automation:
    """
    DWSIM Automation engine.

    Creates Automation3 internally and provides Pythonic wrappers
    for flowsheet loading, solving, and property access.
    """

    def __init__(self):
        _init_runtime()
        import DWSIM
        self._automation = DWSIM.Automation.Automation3()
        self._DWSIM = DWSIM

    @property
    def available_compounds(self):
        """List of available chemical compounds."""
        return list(self._automation.AvailableCompounds.Keys)

    @property
    def available_property_packages(self):
        """List of available property packages."""
        return list(self._automation.AvailablePropertyPackages.Keys)

    def load_flowsheet(self, path):
        """
        Load a .dwxml or .dwxmz flowsheet file.

        Returns a Flowsheet wrapper.
        """
        fs = self._automation.LoadFlowsheet(os.path.abspath(path))
        _configure_flowsheet_resource_managers(fs)
        return Flowsheet(fs, self._DWSIM)

    def solve(self, flowsheet):
        """
        Solve the flowsheet. Returns list of error messages (empty if OK).
        """
        errors = self._automation.CalculateFlowsheet2(flowsheet._fs)
        result = []
        if errors:
            for i in range(errors.Count):
                e = errors[i]
                result.append(str(e.Message) if e else "Unknown error")
        return result

    def close(self, flowsheet):
        """Release flowsheet resources."""
        self._automation.CloseFlowsheet(flowsheet._fs)


class Flowsheet:
    """
    Wrapper around DWSIM IFlowsheet.

    Access simulation objects by tag name, get/set properties,
    and inspect the flowsheet structure.
    """

    def __init__(self, fs, DWSIM):
        self._fs = fs
        self._DWSIM = DWSIM

    def get_object(self, tag):
        """
        Get a simulation object by its tag name (e.g. "IN", "OUT", "Air").
        Returns a SimulationObject wrapper, or None if not found.
        """
        obj = self._fs.GetFlowsheetSimulationObject(tag)
        if obj is None:
            return None
        return SimulationObject(obj)

    def get_objects(self):
        """
        Return dict of all simulation objects keyed by tag name.
        """
        result = {}
        for key in self._fs.SimulationObjects.Keys:
            obj = self._fs.SimulationObjects[key]
            tag = ""
            try:
                tag = obj.GraphicObject.Tag or obj.Name
            except:
                tag = obj.Name
            result[tag] = SimulationObject(obj)
        return result

    def list_objects(self):
        """Print all objects with their tag, name, and type."""
        for tag, obj in self.get_objects().items():
            print(f"  {tag:30s}  {obj.type_name}")

    @property
    def compounds(self):
        """List of chemical compounds in this flowsheet."""
        return list(self._fs.SelectedCompounds.Keys)

    def request_calculation(self):
        """Request a calculation (async trigger)."""
        self._fs.RequestCalculation()


class SimulationObject:
    """
    Wrapper around DWSIM ISimulationObject.

    Get/set properties using human-readable names.
    """

    def __init__(self, obj):
        self._obj = obj

    @property
    def name(self):
        """Internal object name/ID."""
        return self._obj.Name

    @property
    def tag(self):
        """User-visible tag."""
        try:
            return self._obj.GraphicObject.Tag
        except:
            return self._obj.Name

    @property
    def type_name(self):
        """Object type (MaterialStream, Reactor_Conversion, etc.)."""
        return self._obj.GetType().Name

    def get_property(self, prop_name):
        """
        Get a property value by name (e.g. "Temperature", "Pressure").
        Uses SI units. Returns float or None.
        """
        val = self._obj.GetPropertyValue2(prop_name, "", "")
        if val is None:
            return None
        try:
            return float(val)
        except (TypeError, ValueError):
            return str(val)

    def set_property(self, prop_name, value):
        """
        Set a property value by name (e.g. "Temperature", 500.0).
        Value must be in SI units (K, Pa, kg/s, mol/s).
        """
        self._obj.SetPropertyValue2(prop_name, "", "", float(value))

    def get_property_unit(self, prop_name):
        """Get the unit string for a property."""
        return str(self._obj.GetPropertyUnit(prop_name) or "")

    def list_properties(self):
        """
        Print all available properties with current values.
        """
        props = self._obj.GetDefaultProperties()
        for p in props:
            try:
                val = self._obj.GetPropertyValue(p)
                unit = self._obj.GetPropertyUnit(p) or ""
                if val is not None:
                    print(f"  {p:40s}: {val} {unit}")
            except:
                pass

    def list_all_properties(self):
        """
        Return list of all available property names (human-readable).
        """
        return [str(p) for p in self._obj.GetProperties2()]


# ═══════════════════════════════════════════════════════════════════
# Convenience: auto-create singleton on first use
# ═══════════════════════════════════════════════════════════════════

def create():
    """Create and return an Automation instance."""
    return Automation()
