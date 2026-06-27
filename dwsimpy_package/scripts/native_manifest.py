from __future__ import annotations

COMMON_MARKERS = (
    "DWSIM.Automation.dll",
    "DWSIM.Thermodynamics.dll",
    "DWSIM.Thermodynamics.CoolPropInterface.dll",
    "System.Windows.Forms.dll",
    "System.Drawing.Common.dll",
)

REQUIRED_NATIVE = {
    "macos_arm64": ("libCoolProp.dylib", "libPetAz.dylib", "libSkiaSharp.dylib"),
    "linux_x86_64": ("libCoolProp.so", "libPetAz.so", "libSkiaSharp.so"),
    "windows_x86_64": ("CoolProp.dll", "PetAz.dll", "libSkiaSharp.dll"),
}

KNOWN_NATIVE_FILES = tuple(
    sorted(
        {
            name
            for names in REQUIRED_NATIVE.values()
            for name in names
        }
        | {
            "CoolProp.dylib",
            "PetAz.dylib",
            "CoolPropCsharp.dll",
        }
    )
)

PLATFORM_DIRS = (
    "macos_arm64",
    "linux_x86_64",
    "windows_x86_64",
    "native",
)
