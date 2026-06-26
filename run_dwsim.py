#!/usr/bin/env python3
"""
DWSIM pythonnet örneği — Automation3 API
========================================
1. .dwxml yükle
2. Material stream property'lerini SetPropertyValue2 ile değiştir
3. RequestCalculation + CalculateFlowsheet2 ile çöz
4. GetPropertyValue2 ile unit operation sonuçlarını oku
"""

import sys
import os
from os.path import join, dirname, abspath, normpath

current_script_path = os.path.realpath(__file__)
PROJECT_PATH = dirname(current_script_path)
FLOWSHEET_PATH = normpath(join(
    PROJECT_PATH, "dwsim/PlatformFiles/Common/samples/Carbon Combustion.dwxml"))
DWSIM_PATH = join(PROJECT_PATH, "build")

# ═══════════════════════════════════════════════════════════════
# RUNTIME BAŞLATMA
# ═══════════════════════════════════════════════════════════════
os.environ["DYLD_LIBRARY_PATH"] = DWSIM_PATH

import json
rtcfg = {"runtimeOptions": {"tfm": "net10.0",
                            "framework": {"name": "Microsoft.NETCore.App",
                                          "version": "10.0.0"}}}
cfg_path = "/tmp/dwsim_runtimeconfig.json"
with open(cfg_path, "w") as f:
    json.dump(rtcfg, f)
os.environ["PYTHONNET_CORECLR_RUNTIME_CONFIG"] = cfg_path
os.environ["PYTHONNET_CORECLR_DOTNET_ROOT"] = "/opt/homebrew/opt/dotnet/libexec"

sys.path.insert(0, DWSIM_PATH)
os.chdir(DWSIM_PATH)

from pythonnet import load
load("coreclr")
import clr

# Stub assembly'ler
clr.AddReference(join(DWSIM_PATH, "System.Windows.Forms.dll"))
clr.AddReference(join(DWSIM_PATH, "System.Drawing.Common.dll"))

# DWSIM assembly'ler
for asm in [
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
]:
    clr.AddReference(asm)

import DWSIM

# ═══════════════════════════════════════════════════════════════
# AUTOMATION
# ═══════════════════════════════════════════════════════════════
automation = DWSIM.Automation.Automation3()


def test_run(T, P, F, idx):
    print(f"\n--- Run {idx} ---")

    # 1. Flowsheet yükle
    flowsheet = automation.LoadFlowsheet(FLOWSHEET_PATH)

    # 2. Feed stream bul (Tag = "Air")
    stream_in = flowsheet.GetFlowsheetSimulationObject("Air")

    # 3. Property'leri ayarla (SI birimleri)
    stream_in.SetPropertyValue2("Temperature", "", "", T)
    stream_in.SetPropertyValue2("Pressure", "", "", P)
    stream_in.SetPropertyValue2("Molar Flow", "", "", F)

    # 4. Çöz
    flowsheet.RequestCalculation()
    errors1 = automation.CalculateFlowsheet2(flowsheet)
    errors2 = automation.CalculateFlowsheet2(flowsheet)
    n_err1 = errors1.Count if errors1 else 0
    n_err2 = errors2.Count if errors2 else 0
    print(f"  Errors1: {n_err1}")
    print(f"  Errors2: {n_err2}")

    # 5. Sonuçları oku — Feed stream (çözüm sonrası değerler)
    T_out = float(stream_in.GetPropertyValue2("Temperature", "", ""))
    P_out = float(stream_in.GetPropertyValue2("Pressure", "", ""))
    F_out = float(stream_in.GetPropertyValue2("Molar Flow", "", ""))
    print(f"  Air: T={T_out:.2f} K, P={P_out:.0f} Pa, F={F_out:.2f} mol/s")

    # 6. Ürün stream ("Feed" Tag'li) sonuçları
    stream_out = flowsheet.GetFlowsheetSimulationObject("Feed")
    P_feed = float(stream_out.GetPropertyValue2("Pressure", "", ""))
    F_feed = float(stream_out.GetPropertyValue2("Molar Flow", "", ""))
    T_feed = float(stream_out.GetPropertyValue2("Temperature", "", ""))
    print(f"  Product: T={T_feed:.2f} K, P={P_feed:.0f} Pa, F={F_feed:.2f} mol/s")

    # 7. Reaktör dönüşümü oku
    reactor = flowsheet.GetFlowsheetSimulationObject("RC-000")
    props = reactor.GetProperties2()
    for p in props:
        if p is not None and "Conversion" in str(p):
            val = reactor.GetPropertyValue2(p, "", "")
            if val is not None:
                try:
                    v = float(val)
                    if abs(v) > 0.001:
                        print(f"  {p}: {v:.2f}%")
                except:
                    pass

    automation.CloseFlowsheet(flowsheet)


# ═══════════════════════════════════════════════════════════════
# ÇALIŞTIR
# ═══════════════════════════════════════════════════════════════
test_run(572.97, 11315621, 25975, 1)
test_run(488.66, 4400058, 22367, 2)
test_run(541.14, 10577499, 557, 3)
