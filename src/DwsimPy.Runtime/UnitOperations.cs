namespace DwsimPy.Runtime;

public sealed record UnitOperationDescriptor
{
    public required string ObjectType { get; init; }

    public required string DisplayName { get; init; }

    public required string Category { get; init; }

    public required string Prefix { get; init; }

    public required string SimulationType { get; init; }

    public required string ObjectClass { get; init; }

    public required string GraphicType { get; init; }

    public required string GraphicObjectType { get; init; }

    public int InputConnectorCount { get; init; }

    public int OutputConnectorCount { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public bool SupportsEnergyConnector { get; init; }

    public bool IsBuiltIn { get; init; } = true;

    public bool IsSolverReady { get; init; }

    public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

    public bool CreatesSimulationObject => !string.IsNullOrWhiteSpace(SimulationType);
}

public sealed class UnitOperationRegistry
{
    private readonly Dictionary<string, UnitOperationDescriptor> _byKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, UnitOperationDescriptor> _bySimulationType = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<UnitOperationDescriptor> _descriptors = new();

    public static UnitOperationRegistry Default { get; } = CreateDefault();

    public IReadOnlyList<UnitOperationDescriptor> All => _descriptors;

    public bool TryGet(string objectTypeOrAlias, out UnitOperationDescriptor descriptor) =>
        _byKey.TryGetValue(objectTypeOrAlias, out descriptor!);

    public bool TryResolveBySimulationType(string simulationType, out UnitOperationDescriptor descriptor) =>
        _bySimulationType.TryGetValue(simulationType, out descriptor!);

    public UnitOperationDescriptor Resolve(string objectTypeOrAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectTypeOrAlias);
        if (TryGet(objectTypeOrAlias, out var descriptor))
        {
            return descriptor;
        }

        return CreateGenericDescriptor(objectTypeOrAlias);
    }

    public void Register(UnitOperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.ObjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.Prefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.GraphicType);
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor.GraphicObjectType);

        if (_byKey.ContainsKey(descriptor.ObjectType))
        {
            throw new InvalidOperationException($"Unit operation already registered: {descriptor.ObjectType}");
        }

        _descriptors.Add(descriptor);
        _byKey[descriptor.ObjectType] = descriptor;
        _byKey[descriptor.DisplayName] = descriptor;

        foreach (var alias in descriptor.Aliases.Where(a => !string.IsNullOrWhiteSpace(a)))
        {
            _byKey[alias] = descriptor;
        }

        if (!string.IsNullOrWhiteSpace(descriptor.SimulationType)
            && !_bySimulationType.ContainsKey(descriptor.SimulationType))
        {
            _bySimulationType[descriptor.SimulationType] = descriptor;
        }
    }

    public static UnitOperationRegistry CreateDefault()
    {
        var registry = new UnitOperationRegistry();
        foreach (var descriptor in BuiltInDescriptors())
        {
            registry.Register(descriptor);
        }

        return registry;
    }

    private static UnitOperationDescriptor CreateGenericDescriptor(string objectType)
    {
        var prefix = new string(objectType.Where(char.IsLetterOrDigit).Take(8).ToArray()).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "OBJ";
        }

        return new UnitOperationDescriptor
        {
            ObjectType = objectType,
            DisplayName = objectType,
            Category = "Custom",
            Prefix = prefix,
            SimulationType = $"DWSIM.UnitOperations.UnitOperations.{objectType}",
            ObjectClass = "UserModels",
            GraphicType = "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.DummyGraphic",
            GraphicObjectType = objectType,
            InputConnectorCount = 1,
            OutputConnectorCount = 1,
            Width = 40,
            Height = 40,
            SupportsEnergyConnector = true,
            IsBuiltIn = false,
            IsSolverReady = false
        };
    }

    private static IEnumerable<UnitOperationDescriptor> BuiltInDescriptors()
    {
        yield return D("MaterialStream", "Material Stream", "Streams", "MAT",
            "DWSIM.Thermodynamics.Streams.MaterialStream", "Streams",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.MaterialStreamGraphic", 1, 1, 20, 20);
        yield return D("EnergyStream", "Energy Stream", "Streams", "EN",
            "DWSIM.UnitOperations.Streams.EnergyStream", "Streams",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.EnergyStreamGraphic", 1, 1, 20, 20, true);

        yield return D("NodeIn", "Stream Mixer", "Mixers/Splitters", "MIST",
            "DWSIM.UnitOperations.UnitOperations.Mixer", "MixersSplitters",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.MixerGraphic", 6, 1, 20, 20, false, null,
            "Mixer");
        yield return D("NodeOut", "Stream Splitter", "Mixers/Splitters", "DIV",
            "DWSIM.UnitOperations.UnitOperations.Splitter", "MixersSplitters",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.SplitterGraphic", 1, 3, 20, 20, false, null,
            "Splitter");

        yield return D("Pump", "Pump", "Pressure Changers", "BB",
            "DWSIM.UnitOperations.UnitOperations.Pump", "PressureChangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.PumpGraphic", 2, 1, 25, 25, true);
        yield return D("Compressor", "Compressor", "Pressure Changers", "COMP",
            "DWSIM.UnitOperations.UnitOperations.Compressor", "PressureChangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CompressorGraphic", 2, 1, 25, 25, true);
        yield return D("Expander", "Expander", "Pressure Changers", "TURB",
            "DWSIM.UnitOperations.UnitOperations.Expander", "PressureChangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.TurbineGraphic", 1, 1, 25, 25, true,
            null, "Expander (Turbine)");
        yield return D("Valve", "Valve", "Pressure Changers", "VALV",
            "DWSIM.UnitOperations.UnitOperations.Valve", "PressureChangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ValveGraphic", 1, 1, 20, 20);
        yield return D("Pipe", "Pipe Segment", "Pressure Changers", "TUB",
            "DWSIM.UnitOperations.UnitOperations.Pipe", "PressureChangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.PipeSegmentGraphic", 1, 1, 80, 20, true);
        yield return D("OrificePlate", "Orifice Plate", "Pressure Changers", "OP",
            "DWSIM.UnitOperations.UnitOperations.OrificePlate", "PressureChangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.OrificePlateGraphic", 1, 1, 25, 25);

        yield return D("Heater", "Heater", "Heat Exchangers", "AQ",
            "DWSIM.UnitOperations.UnitOperations.Heater", "Exchangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.HeaterGraphic", 2, 1, 25, 25, true);
        yield return D("Cooler", "Cooler", "Heat Exchangers", "RESF",
            "DWSIM.UnitOperations.UnitOperations.Cooler", "Exchangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CoolerGraphic", 2, 1, 25, 25, true);
        yield return D("HeatExchanger", "Heat Exchanger", "Heat Exchangers", "HE",
            "DWSIM.UnitOperations.UnitOperations.HeatExchanger", "Exchangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.HeatExchangerGraphic", 2, 2, 30, 30);

        yield return D("Tank", "Tank", "Separators", "TQ",
            "DWSIM.UnitOperations.UnitOperations.Tank", "Separators",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.TankGraphic", 1, 1, 50, 50);
        yield return D("Vessel", "Gas-Liquid Separator", "Separators", "SEP",
            "DWSIM.UnitOperations.UnitOperations.Vessel", "Separators",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.VesselGraphic", 7, 3, 50, 70);
        yield return D("ComponentSeparator", "Compound Separator", "Separators", "CS",
            "DWSIM.UnitOperations.UnitOperations.ComponentSeparator", "Separators",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ComponentSeparatorGraphic", 1, 2, 50, 50, true);
        yield return D("SolidSeparator", "Solids Separator", "Solids", "SS",
            "DWSIM.UnitOperations.UnitOperations.SolidsSeparator", "Solids",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.SolidsSeparatorGraphic", 1, 2, 50, 50);
        yield return D("Filter", "Filter", "Solids", "FT",
            "DWSIM.UnitOperations.UnitOperations.Filter", "Solids",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.FilterGraphic", 1, 2, 50, 50, true);

        yield return D("ShortcutColumn", "Shortcut Column", "Columns", "SC",
            "DWSIM.UnitOperations.UnitOperations.ShortcutColumn", "Columns",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ShortcutColumnGraphic", 2, 2, 144, 180, true);
        yield return D("DistillationColumn", "Distillation Column", "Columns", "DC",
            "DWSIM.UnitOperations.UnitOperations.DistillationColumn", "Columns",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.RigorousColumnGraphic", 11, 11, 144, 180, true);
        yield return D("AbsorptionColumn", "Absorption Column", "Columns", "ABS",
            "DWSIM.UnitOperations.UnitOperations.AbsorptionColumn", "Columns",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.AbsorptionColumnGraphic", 10, 10, 144, 180, false,
            null, "Absorption/Extraction Column");
        yield return D("RefluxedAbsorber", "Refluxed Absorber", "Columns", "RABS",
            "DWSIM.UnitOperations.UnitOperations.DistillationColumn", "Columns",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.RigorousColumnGraphic", 10, 10, 144, 180, true);
        yield return D("ReboiledAbsorber", "Reboiled Absorber", "Columns", "BABS",
            "DWSIM.UnitOperations.UnitOperations.DistillationColumn", "Columns",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.RigorousColumnGraphic", 10, 10, 144, 180, true);

        yield return D("RCT_Conversion", "Conversion Reactor", "Reactors", "RC",
            "DWSIM.UnitOperations.Reactors.Reactor_Conversion", "Reactors",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ConversionReactorGraphic", 2, 2, 50, 50, true);
        yield return D("RCT_Equilibrium", "Equilibrium Reactor", "Reactors", "RE",
            "DWSIM.UnitOperations.Reactors.Reactor_Equilibrium", "Reactors",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.EquilibriumReactorGraphic", 2, 2, 50, 50, true);
        yield return D("RCT_Gibbs", "Gibbs Reactor", "Reactors", "RG",
            "DWSIM.UnitOperations.Reactors.Reactor_Gibbs", "Reactors",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.GibbsReactorGraphic", 2, 2, 50, 50, true);
        yield return D("RCT_CSTR", "Continuous Stirred Tank Reactor (CSTR)", "Reactors", "CSTR",
            "DWSIM.UnitOperations.Reactors.Reactor_CSTR", "Reactors",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CSTRGraphic", 2, 2, 50, 50, true);
        yield return D("RCT_PFR", "Plug-Flow Reactor (PFR)", "Reactors", "PFR",
            "DWSIM.UnitOperations.Reactors.Reactor_PFR", "Reactors",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.PFRGraphic", 2, 1, 70, 20, true);
        yield return D("RCT_GibbsReaktoro", "Gibbs Reactor (Reaktoro)", "Reactors", "RGIBBSR",
            "DWSIM.UnitOperations.Reactors.Reactor_ReaktoroGibbs", "Reactors",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ExternalUnitOperationGraphic", 1, 2, 40, 40, true,
            "External");

        yield return D("OT_Adjust", "Controller Block", "Logical", "AJ",
            "DWSIM.UnitOperations.SpecialOps.Adjust", "Logical",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.AdjustGraphic", 0, 0, 20, 20);
        yield return D("OT_Spec", "Specification Block", "Logical", "ES",
            "DWSIM.UnitOperations.SpecialOps.Spec", "Logical",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.SpecGraphic", 0, 0, 20, 20);
        yield return D("OT_Recycle", "Recycle Block", "Logical", "REC",
            "DWSIM.UnitOperations.SpecialOps.Recycle", "Logical",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.RecycleGraphic", 1, 1, 20, 20);
        yield return D("OT_EnergyRecycle", "Energy Recycle Block", "Logical", "EREC",
            "DWSIM.UnitOperations.SpecialOps.EnergyRecycle", "Logical",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.EnergyRecycleGraphic", 1, 1, 20, 20, true);

        yield return D("CustomUO", "Python Script", "User Models", "UO",
            "DWSIM.UnitOperations.UnitOperations.CustomUO", "UserModels",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ScriptGraphic", 7, 7, 25, 25, true,
            null, "Custom Unit Operation", "PythonScript");
        yield return D("ExcelUO", "Spreadsheet", "User Models", "EXL",
            "DWSIM.UnitOperations.UnitOperations.ExcelUO", "UserModels",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.SpreadsheetGraphic", 5, 4, 25, 25, true);
        yield return D("FlowsheetUO", "Flowsheet", "User Models", "FS",
            "DWSIM.UnitOperations.UnitOperations.Flowsheet", "UserModels",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.FlowsheetGraphic", 10, 10, 25, 25);
        yield return D("CapeOpenUO", "CAPE-OPEN Unit Operation", "User Models", "COUO",
            "DWSIM.UnitOperations.UnitOperations.CapeOpenUO", "CAPEOPEN",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CAPEOPENGraphic", 2, 2, 40, 40);
        yield return D("External", "External Unit Operation", "User Models", "EXT",
            "DWSIM.UnitOperations.UnitOperations.DummyUnitOperation", "UserModels",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ExternalUnitOperationGraphic", 0, 0, 40, 40);

        yield return D("DigitalGauge", "Digital Gauge", "Indicators", "DG",
            "DWSIM.UnitOperations.UnitOperations.DigitalGauge", "Indicators",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.DigitalGaugeGraphic", 0, 0, 40, 20);
        yield return D("AnalogGauge", "Analog Gauge", "Indicators", "AG",
            "DWSIM.UnitOperations.UnitOperations.AnalogGauge", "Indicators",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.AnalogGaugeGraphic", 0, 0, 50, 50);
        yield return D("LevelGauge", "Level Gauge", "Indicators", "LG",
            "DWSIM.UnitOperations.UnitOperations.LevelGauge", "Indicators",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.LevelGaugeGraphic", 0, 0, 40, 70);
        yield return D("Controller_PID", "PID Controller", "Controllers", "PID",
            "DWSIM.UnitOperations.SpecialOps.PIDController", "Controllers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.PIDControllerGraphic", 0, 0, 50, 50);
        yield return D("Controller_Python", "Python Controller", "Controllers", "PC",
            "DWSIM.UnitOperations.SpecialOps.PythonController", "Controllers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.PythonControllerGraphic", 0, 0, 50, 50);
        yield return D("Input", "Input Box", "Inputs", "IN",
            "DWSIM.UnitOperations.UnitOperations.Input", "Inputs",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.InputGraphic", 0, 0, 50, 25);
        yield return D("Switch", "Switch", "Inputs", "SW",
            "DWSIM.UnitOperations.UnitOperations.Switch", "Switches",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.SwitchGraphic", 0, 0, 50, 40);

        yield return D("WindTurbine", "Wind Turbine", "Clean Power Sources", "WT",
            "DWSIM.UnitOperations.UnitOperations.WindTurbine", "CleanPowerSources",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ExternalUnitOperationGraphic", 0, 1, 40, 40, true,
            "External");
        yield return D("HydroelectricTurbine", "Hydroelectric Turbine", "Clean Power Sources", "HYT",
            "DWSIM.UnitOperations.UnitOperations.HydroelectricTurbine", "CleanPowerSources",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ExternalUnitOperationGraphic", 1, 2, 40, 40, true,
            "External");
        yield return D("SolarPanel", "Solar Panel", "Clean Power Sources", "SP",
            "DWSIM.UnitOperations.UnitOperations.SolarPanel", "CleanPowerSources",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ExternalUnitOperationGraphic", 0, 1, 40, 40, true,
            "External");
        yield return D("WaterElectrolyzer", "Water Electrolyzer", "Clean Power Sources", "WELEC",
            "DWSIM.UnitOperations.UnitOperations.WaterElectrolyzer", "CleanPowerSources",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ExternalUnitOperationGraphic", 2, 2, 40, 40, true,
            "External");
        yield return D("PEMFuelCell", "PEM Fuel Cell (Amphlett)", "Clean Power Sources", "PEMFC",
            "DWSIM.UnitOperations.UnitOperations.PEMFC_Amphlett", "CleanPowerSources",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ExternalUnitOperationGraphic", 2, 2, 40, 40, true,
            "External");

        yield return D("AirCooler2", "Air Cooler 2", "Heat Exchangers", "AC",
            "DWSIM.UnitOperations.UnitOperations.AirCooler2", "Exchangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.HeatExchangerGraphic", 2, 2, 40, 40, true);
        yield return D("CompressorExpander", "Compressor/Expander", "Pressure Changers", "CX",
            "DWSIM.UnitOperations.UnitOperations.Compressor", "PressureChangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.CompressorExpanderGraphic", 2, 1, 25, 25, true);
        yield return D("HeaterCooler", "Heater/Cooler", "Heat Exchangers", "HC",
            "DWSIM.UnitOperations.UnitOperations.Heater", "Exchangers",
            "DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.HeaterCoolerGraphic", 2, 1, 25, 25, true);

        yield return D("GO_Text", "Text", "Annotations", "TEXT", "",
            "Annotations", "DWSIM.Drawing.SkiaSharp.GraphicObjects.TextGraphic", 0, 0, 120, 20);
    }

    private static UnitOperationDescriptor D(
        string objectType,
        string displayName,
        string category,
        string prefix,
        string simulationType,
        string objectClass,
        string graphicType,
        int inputConnectorCount,
        int outputConnectorCount,
        double width,
        double height,
        bool supportsEnergyConnector = false,
        string? graphicObjectType = null,
        params string[] aliases) =>
        new()
        {
            ObjectType = objectType,
            DisplayName = displayName,
            Category = category,
            Prefix = prefix,
            SimulationType = simulationType,
            ObjectClass = objectClass,
            GraphicType = graphicType,
            GraphicObjectType = graphicObjectType ?? objectType,
            InputConnectorCount = inputConnectorCount,
            OutputConnectorCount = outputConnectorCount,
            Width = width,
            Height = height,
            SupportsEnergyConnector = supportsEnergyConnector,
            IsBuiltIn = true,
            IsSolverReady = false,
            Aliases = aliases
        };
}
