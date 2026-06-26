#!/usr/bin/env python3
"""
dwsimpy test — herhangi bir .dwxml dosyasıyla çalışır
"""
import sys
sys.path.insert(0, ".")
import dwsimpy

SIM = "../dwsim/PlatformFiles/Common/samples/Carbon Combustion.dwxml"

print("=== dwsimpy Test ===\n")

# 1. Automation oluştur
sim = dwsimpy.Automation()
print(f"Bileşik sayısı: {len(sim.available_compounds)}")
print(f"PP sayısı:      {len(sim.available_property_packages)}")

# 2. Flowsheet yükle
fs = sim.load_flowsheet(SIM)
print(f"\nNesneler:")
fs.list_objects()
print(f"\nBileşikler: {fs.compounds}")

# 3. Feed stream bul
air = fs.get_object("Air")
print(f"\nAir stream:")
print(f"  Tag:  {air.tag}")
print(f"  Type: {air.type_name}")
air.list_properties()

# 4. Property değiştir
print("\n--- Temperature = 500K ---")
air.set_property("Temperature", 500.0)
air.set_property("Pressure", 200000.0)
print(f"  T = {air.get_property('Temperature')} K")
print(f"  P = {air.get_property('Pressure')} Pa")

# 5. Çöz
print("\nSolving...")
errors = sim.solve(fs)
print(f"Errors: {len(errors)}")
for e in errors:
    print(f"  {e}")

# 6. Sonuçları oku
print("\n--- Sonuçlar ---")
for tag, obj in fs.get_objects().items():
    if obj.type_name == "MaterialStream":
        T = obj.get_property("Temperature")
        P = obj.get_property("Pressure")
        F = obj.get_property("Molar Flow")
        print(f"  {tag:15s}  T={T:.1f}K  P={P:.0f}Pa  F={F:.2f}mol/s")
    elif "Reactor" in obj.type_name:
        print(f"  {tag:15s}  ({obj.type_name})")
        obj.list_properties()

sim.close(fs)
print("\nDone!")
