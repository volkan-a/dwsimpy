using System.IO.Compression;
using System.Text;
using DwsimPy.Runtime;

var tests = new (string Name, Action Body)[]
{
    ("registry contains DWSIM palette", RegistryContainsDwsimPalette),
    ("registry resolves aliases", RegistryResolvesAliases),
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
