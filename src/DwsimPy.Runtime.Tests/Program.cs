using System.IO.Compression;
using System.Text;
using DwsimPy.Runtime;

var tests = new (string Name, Action Body)[]
{
    ("registry contains DWSIM palette", RegistryContainsDwsimPalette),
    ("registry resolves aliases", RegistryResolvesAliases),
    ("interfaces are headless contracts", InterfacesAreHeadlessContracts),
    ("global settings are headless", GlobalSettingsAreHeadless),
    ("shared classes csharp are headless", SharedClassesCSharpAreHeadless),
    ("dwxml graph edit roundtrip", DwxmlGraphEditRoundtrip),
    ("dwxmz save and load roundtrip", DwxmzSaveAndLoadRoundtrip),
    ("external graphic resolves to simulation object type", ExternalGraphicResolvesToSimulationObjectType),
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.Error.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failed > 0)
{
    return 1;
}

Console.WriteLine($"PASS {tests.Length} tests");
return 0;

static void RegistryContainsDwsimPalette()
{
    var registry = UnitOperationRegistry.CreateDefault();
    var expectedObjectTypes = new[]
    {
        "OT_Adjust",
        "OT_Spec",
        "OT_Recycle",
        "OT_EnergyRecycle",
        "NodeIn",
        "NodeOut",
        "Pump",
        "Tank",
        "Vessel",
        "MaterialStream",
        "EnergyStream",
        "Compressor",
        "Expander",
        "Heater",
        "Cooler",
        "Pipe",
        "Valve",
        "RCT_Conversion",
        "RCT_Equilibrium",
        "RCT_Gibbs",
        "RCT_PFR",
        "RCT_CSTR",
        "HeatExchanger",
        "ShortcutColumn",
        "DistillationColumn",
        "AbsorptionColumn",
        "ComponentSeparator",
        "SolidSeparator",
        "Filter",
        "OrificePlate",
        "CustomUO",
        "ExcelUO",
        "FlowsheetUO",
        "CapeOpenUO",
        "DigitalGauge",
        "AnalogGauge",
        "LevelGauge",
        "Controller_PID",
        "Controller_Python",
        "Input",
        "Switch",
        "AirCooler2",
        "RCT_GibbsReaktoro",
        "WindTurbine",
        "HydroelectricTurbine",
        "SolarPanel",
        "WaterElectrolyzer",
        "PEMFuelCell",
    };

    foreach (var objectType in expectedObjectTypes)
    {
        Assert(registry.TryGet(objectType, out var descriptor), $"Missing descriptor: {objectType}");
        Assert(descriptor.ObjectType == objectType, $"Descriptor key mismatch for {objectType}");
        Assert(descriptor.Width > 0, $"Invalid width for {objectType}");
        Assert(descriptor.Height > 0, $"Invalid height for {objectType}");
        Assert(descriptor.InputConnectorCount >= 0, $"Invalid input connector count for {objectType}");
        Assert(descriptor.OutputConnectorCount >= 0, $"Invalid output connector count for {objectType}");
    }
}

static void RegistryResolvesAliases()
{
    var registry = UnitOperationRegistry.CreateDefault();
    Assert(registry.Resolve("Mixer").ObjectType == "NodeIn", "Mixer should resolve to NodeIn");
    Assert(registry.Resolve("Stream Mixer").ObjectType == "NodeIn", "Stream Mixer should resolve to NodeIn");
    Assert(registry.Resolve("Splitter").ObjectType == "NodeOut", "Splitter should resolve to NodeOut");
    Assert(registry.Resolve("Expander (Turbine)").ObjectType == "Expander", "Display alias should resolve to Expander");
    Assert(registry.Resolve("PEM Fuel Cell (Amphlett)").ObjectType == "PEMFuelCell", "Display alias should resolve to PEMFuelCell");
}

static void InterfacesAreHeadlessContracts()
{
    Assert(typeof(DWSIM.Interfaces.IFlowsheet).IsInterface, "IFlowsheet should be available");
    Assert(typeof(DWSIM.Interfaces.ISimulationObject).IsInterface, "ISimulationObject should be available");
    Assert(typeof(DWSIM.Interfaces.IPropertyPackage).IsInterface, "IPropertyPackage should be available");
    Assert(Enum.GetNames(typeof(DWSIM.Interfaces.Enums.GraphicObjects.ObjectType))
        .Contains("MaterialStream"), "Graphic object enum should include MaterialStream");

    var getEditingForm = typeof(DWSIM.Interfaces.ISimulationObject).GetMethod("GetEditingForm");
    Assert(getEditingForm?.ReturnType == typeof(object), "GetEditingForm should not expose WinForms");

    var getIconAsBitmap = typeof(DWSIM.Interfaces.IGraphicObject).GetMethod("GetIconAsBitmap");
    Assert(getIconAsBitmap?.ReturnType == typeof(object), "GetIconAsBitmap should not expose System.Drawing");
}

static void GlobalSettingsAreHeadless()
{
    var platform = DWSIM.GlobalSettings.Settings.GetPlatform();
    Assert(platform is "Windows" or "Linux" or "Mac", $"Unexpected platform: {platform}");
    Assert(DWSIM.GlobalSettings.Settings.GetEnvironment() is 32 or 64, "Environment bitness should be 32 or 64");
    Assert(DWSIM.GlobalSettings.Settings.TaskCancellationTokenSource is not null, "Cancellation token source should be available");

    DWSIM.GlobalSettings.Settings.PythonInitialized = false;
    AssertThrows<PlatformNotSupportedException>(
        () => DWSIM.GlobalSettings.Settings.InitializePythonEnvironment(),
        "Python.NET initialization should not be part of GlobalSettings");

    var path = Path.Combine(Path.GetTempPath(), $"dwsimpy-globalsettings-{Guid.NewGuid():N}.ini");
    try
    {
        var source = new Nini.Config.IniConfigSource(path);
        source.AddConfig("Misc").Set("SolverMode", 2);
        source["Misc"].Set("EnableParallelProcessing", true);
        source.Save();

        var reloaded = new Nini.Config.IniConfigSource(path);
        Assert(reloaded["Misc"].GetInt("SolverMode", 0) == 2, "INI integer roundtrip failed");
        Assert(reloaded["Misc"].GetBoolean("EnableParallelProcessing", false), "INI boolean roundtrip failed");
    }
    finally
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

static void SharedClassesCSharpAreHeadless()
{
    var request = new DWSIM.AI.ConvergenceAssistant.Classes.ConvergenceHelperTrainingData
    {
        RequestType = DWSIM.Interfaces.ConvergenceHelperRequestType.PVFlash,
        ModelName = "smoke",
        NumberOfCompounds = 2,
        CompoundNames = new[] { "Water", "Ethanol" },
        Temperature = "300",
        Pressure = "101325",
    };
    Assert(!string.IsNullOrWhiteSpace(request.GetBase64StringHash()), "AI training data hash should be generated");

    var curve = new DWSIM.SharedClassesCSharp.Solids.SolidShapeCurve
    {
        Name = "PSD",
        Data = new List<DWSIM.Interfaces.ISolidParticleSize>
        {
            new DWSIM.SharedClassesCSharp.Solids.SolidParticleSize { Size = 1.0, MassFraction = 0.25 },
            new DWSIM.SharedClassesCSharp.Solids.SolidParticleSize { Size = 2.0, MassFraction = 0.75 },
        },
    };
    AssertNear(0.5, curve.GetValue(1.5), 1e-12, "Solid curve interpolation");
    var clone = curve.Clone();
    Assert(clone.Data.Count == 2, "Solid curve clone should preserve data");

    var allowedType = new DWSIM.SharedClassesCSharp.FilePicker.FilePickerAllowedType("DWSIM", ".dwxmz");
    Assert(allowedType.AllowedExtensions.Single() == ".dwxmz", "Allowed file type should preserve extension");

    var service = DWSIM.SharedClassesCSharp.FilePicker.FilePickerService.GetInstance();
    AssertThrows<PlatformNotSupportedException>(
        () => service.GetFilePicker(),
        "Headless file picker service should require an injected factory");
}

static void DwxmlGraphEditRoundtrip()
{
    var engine = new XmlFlowsheetEngine();
    var created = engine.Create("test.dwxml");

    var feed = engine.AddNode(created.Id, new AddNodeRequest("MaterialStream", "Feed", 40, 80));
    var mixer = engine.AddNode(created.Id, new AddNodeRequest("Mixer", "MIX-001", 140, 80));
    var edge = engine.Connect(created.Id, "Feed", "MIX-001", "material");
    engine.MoveNode(created.Id, "Feed", 55, 90);
    engine.RenameNode(created.Id, "MIX-001", "MixerA");

    Assert(feed.ObjectType == "MaterialStream", "Feed type mismatch");
    Assert(mixer.ObjectType == "NodeIn", "Mixer alias should create NodeIn");
    Assert(edge.StreamType == "material", "Edge stream type mismatch");

    var bytes = engine.Save(created.Id, compressed: false);
    var loaded = engine.Load(bytes, "roundtrip.dwxml");
    var graph = engine.GetGraph(loaded.Id);

    Assert(graph.Nodes.Count == 2, $"Expected 2 nodes, got {graph.Nodes.Count}");
    Assert(graph.Edges.Count == 1, $"Expected 1 edge, got {graph.Edges.Count}");
    Assert(graph.Nodes.Single(n => n.Label == "Feed").X == 55, "Moved X coordinate not preserved");
    Assert(graph.Nodes.Single(n => n.Label == "MixerA").ObjectType == "NodeIn", "Renamed mixer not preserved");

    engine.DeleteNode(loaded.Id, "MixerA");
    var graphAfterDelete = engine.GetGraph(loaded.Id);
    Assert(graphAfterDelete.Nodes.Count == 1, $"Expected 1 node after delete, got {graphAfterDelete.Nodes.Count}");
    Assert(graphAfterDelete.Edges.Count == 0, $"Expected 0 edges after delete, got {graphAfterDelete.Edges.Count}");
}

static void DwxmzSaveAndLoadRoundtrip()
{
    var engine = new XmlFlowsheetEngine();
    var created = engine.Create("compressed.dwxmz");
    engine.AddNode(created.Id, new AddNodeRequest("SolarPanel", "PV-001", 20, 30));
    var archiveBytes = engine.Save(created.Id, compressed: true);

    using (var archive = new ZipArchive(new MemoryStream(archiveBytes), ZipArchiveMode.Read))
    {
        Assert(archive.Entries.Any(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)),
            "DWXMZ archive should contain an XML entry");
    }

    var loaded = engine.Load(archiveBytes, "compressed.dwxmz");
    var graph = engine.GetGraph(loaded.Id);
    Assert(graph.Nodes.Count == 1, $"Expected 1 compressed node, got {graph.Nodes.Count}");
    Assert(graph.Nodes[0].ObjectType == "SolarPanel", "Compressed SolarPanel type mismatch");
}

static void ExternalGraphicResolvesToSimulationObjectType()
{
    var xml = """
        <?xml version="1.0" encoding="utf-8"?>
        <DWSIM_Simulation_Data>
          <SimulationObjects>
            <SimulationObject>
              <Type>DWSIM.UnitOperations.UnitOperations.SolarPanel</Type>
              <ObjectClass>CleanPowerSources</ObjectClass>
              <ComponentName>sp-1</ComponentName>
              <Name>sp-1</Name>
            </SimulationObject>
          </SimulationObjects>
          <GraphicObjects>
            <GraphicObject>
              <Type>DWSIM.Drawing.SkiaSharp.GraphicObjects.Shapes.ExternalUnitOperationGraphic</Type>
              <ObjectType>External</ObjectType>
              <Name>sp-1</Name>
              <Tag>PV</Tag>
              <Width>40</Width>
              <Height>40</Height>
              <X>10</X>
              <Y>20</Y>
              <Status>Calculated</Status>
              <InputConnectors />
              <OutputConnectors>
                <Connector IsAttached="false" />
              </OutputConnectors>
              <EnergyConnector>
                <Connector IsAttached="false" />
              </EnergyConnector>
              <SpecialConnectors />
            </GraphicObject>
          </GraphicObjects>
        </DWSIM_Simulation_Data>
        """;

    var engine = new XmlFlowsheetEngine();
    var loaded = engine.Load(Encoding.UTF8.GetBytes(xml), "external.dwxml");
    var graph = engine.GetGraph(loaded.Id);
    Assert(graph.Nodes.Count == 1, $"Expected 1 external node, got {graph.Nodes.Count}");
    Assert(graph.Nodes[0].ObjectType == "SolarPanel", $"Expected SolarPanel, got {graph.Nodes[0].ObjectType}");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static void AssertNear(double expected, double actual, double tolerance, string message)
{
    if (Math.Abs(expected - actual) > tolerance)
    {
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
