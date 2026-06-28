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
    ("shared classes units are headless", SharedClassesUnitsAreHeadless),
    ("shared classes flowsheet data are headless", SharedClassesFlowsheetDataAreHeadless),
    ("shared classes analysis data are headless", SharedClassesAnalysisDataAreHeadless),
    ("shared classes exceptions are headless", SharedClassesExceptionsAreHeadless),
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

static void SharedClassesUnitsAreHeadless()
{
    var si = new DWSIM.SharedClasses.SystemsOfUnits.SI();
    Assert(si.GetCurrentUnits(DWSIM.Interfaces.Enums.UnitOfMeasure.temperature) == "K", "SI temperature unit mismatch");
    Assert(si.GetUnitSet(DWSIM.Interfaces.Enums.UnitOfMeasure.pressure).Contains("bar"), "Pressure unit set should include bar");

    AssertNear(298.15, DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI("C", 25.0), 1e-12, "Celsius to SI");
    AssertNear(25.0, DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertFromSI("C", 298.15), 1e-12, "SI to Celsius");
    AssertNear(100000.0, DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertToSI("bar", 1.0), 1e-9, "bar to SI");
    AssertNear(1.0, DWSIM.SharedClasses.SystemsOfUnits.Converter.ConvertFromSI("bar", 100000.0), 1e-12, "SI to bar");

    var dimension = new DWSIM.SharedClasses.Dimension
    {
        Name = DWSIM.Interfaces.Enums.DimensionName.Volume,
        Value = 12.5,
        IsUserDefined = true,
        UserDefinedValue = 10.0,
    };
    Assert(dimension.GetDisplayName() == "Volume", "Dimension display name mismatch");
    Assert(dimension.GetUnitsType() == DWSIM.Interfaces.Enums.UnitOfMeasure.volume, "Dimension units type mismatch");

    var loaded = new DWSIM.SharedClasses.Dimension();
    loaded.LoadData(dimension.SaveData());
    Assert(loaded.Name == DWSIM.Interfaces.Enums.DimensionName.Volume, "Dimension XML name roundtrip failed");
    AssertNear(12.5, loaded.Value, 1e-12, "Dimension XML value roundtrip");
}

static void SharedClassesFlowsheetDataAreHeadless()
{
    var options = new DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetVariables
    {
        SimulationName = "HeadlessSmoke",
        NumberFormat = "G5",
        MassBalanceRelativeTolerance = 0.001,
    };
    options.VisibleProperties["MaterialStream"] = new List<string> { "Temperature", "Pressure" };

    var loadedOptions = new DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetVariables();
    loadedOptions.LoadData(options.SaveData());
    Assert(loadedOptions.SimulationName == "HeadlessSmoke", "Flowsheet options name roundtrip failed");
    Assert(loadedOptions.VisibleProperties["MaterialStream"].SequenceEqual(new[] { "Temperature", "Pressure" }),
        "Flowsheet visible properties roundtrip failed");
    Assert(loadedOptions.CurrentWeather.Temperature_C == 30, "Default weather data should be available");

    var transition = new DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetTransitionRestore
    {
        FeatureName = "feature",
        FeatureType = "runtime",
        Action = "restore",
        Location = "flowsheet",
        Position = new List<double> { 10.0, 20.0 },
    };
    var loadedTransition = new DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetTransitionRestore();
    loadedTransition.LoadData(transition.SaveData());
    Assert(loadedTransition.FeatureName == "feature", "Transition restore XML roundtrip failed");

    var results = new DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetResults
    {
        TotalCAPEX = 12.0,
        TotalOPEX = 5.0,
        ResidualMassBalance = 0.25,
        TotalEnergyBalance = -0.5,
    };
    var additional = (IDictionary<string, object?>)results.Additional;
    additional["PaybackYears"] = 3.5;
    additional["Scenario"] = "base";

    var loadedResults = new DWSIM.SharedClasses.DWSIM.Flowsheet.FlowsheetResults();
    loadedResults.LoadData(results.SaveData());
    var loadedAdditional = (IDictionary<string, object?>)loadedResults.Additional;
    AssertNear(12.0, loadedResults.TotalCAPEX, 1e-12, "Flowsheet result CAPEX roundtrip");
    AssertNear(3.5, Convert.ToDouble(loadedAdditional["PaybackYears"]), 1e-12, "Additional result double roundtrip");
    Assert(loadedAdditional["Scenario"]?.ToString() == "base", "Additional result string roundtrip");

    var weather = new DWSIM.SharedClasses.WeatherData
    {
        CurrentCondition = DWSIM.Interfaces.WeatherCondition.Cloudy,
        Temperature_C = 18.0,
        RelativeHumidity_pct = 60.0,
    };
    var loadedWeather = new DWSIM.SharedClasses.WeatherData();
    loadedWeather.LoadData(weather.SaveData());
    Assert(loadedWeather.CurrentCondition == DWSIM.Interfaces.WeatherCondition.Cloudy, "Weather condition roundtrip failed");
    AssertNear(18.0, loadedWeather.Temperature_C, 1e-12, "Weather temperature roundtrip");

    var eventArgs = new DWSIM.SharedClasses.DWSIM.Flowsheet.NewDataLoadedEventArgs
    {
        Tag = "loaded",
        DataType = DWSIM.Interfaces.Enums.SnapshotType.ObjectData,
        ShouldResetWindows = true,
    };
    Assert(eventArgs.Tag == "loaded", "New data event args tag mismatch");
    Assert(eventArgs.ShouldResetWindows, "New data event args reset flag mismatch");
}

static void SharedClassesAnalysisDataAreHeadless()
{
    var optimization = new DWSIM.SharedClasses.Flowsheet.Optimization.OptimizationCase
    {
        name = "minimum-duty",
        description = "Minimize heater duty",
        expression = "heaterDuty",
        objfunctype = DWSIM.SharedClasses.Flowsheet.Optimization.OPTObjectiveFunctionType.Expression,
        type = DWSIM.SharedClasses.Flowsheet.Optimization.OPTType.Minimization,
        maxits = 250,
        tolerance = 1e-7,
    };
    optimization.variables["feedTemperature"] = new DWSIM.SharedClasses.Flowsheet.Optimization.OPTVariable
    {
        objectID = "Feed",
        propID = "Temperature",
        unit = "K",
        lowerlimit = 280.0,
        upperlimit = 500.0,
        initialvalue = 320.0,
        currentvalue = 325.0,
    };
    optimization.results.Add(new[] { 325.0, 42.0 });

    var loadedOptimization = new DWSIM.SharedClasses.Flowsheet.Optimization.OptimizationCase();
    loadedOptimization.LoadData(optimization.SaveData());
    Assert(loadedOptimization.name == "minimum-duty", "Optimization case XML name roundtrip failed");
    Assert(loadedOptimization.maxits == 250, "Optimization iteration limit roundtrip failed");
    AssertNear(500.0, loadedOptimization.variables["feedTemperature"].upperlimit!.Value, 1e-12,
        "Optimization variable upper bound roundtrip");

    var clonedOptimization =
        (DWSIM.SharedClasses.Flowsheet.Optimization.OptimizationCase)optimization.Clone();
    clonedOptimization.variables["feedTemperature"].currentvalue = 400.0;
    ((double[])clonedOptimization.results[0]!)[0] = 400.0;
    AssertNear(325.0, optimization.variables["feedTemperature"].currentvalue, 1e-12,
        "Optimization clone should own independent variables");
    AssertNear(325.0, ((double[])optimization.results[0]!)[0], 1e-12,
        "Optimization clone should own cloneable result values");

    var sensitivity = new DWSIM.SharedClasses.Flowsheet.Optimization.SensitivityAnalysisCase
    {
        name = "temperature-sweep",
        numvar = 1,
        depvartype = DWSIM.SharedClasses.Flowsheet.Optimization.SADependentVariableType.Expression,
        expression = "productFlow",
    };
    sensitivity.iv1.objectID = "Feed";
    sensitivity.iv1.propID = "Temperature";
    sensitivity.iv1.lowerlimit = 290.0;
    sensitivity.iv1.upperlimit = 350.0;
    sensitivity.iv1.points = 7;
    sensitivity.variables["feedTemperature"] =
        (DWSIM.SharedClasses.Flowsheet.Optimization.SAVariable)sensitivity.iv1.Clone();
    sensitivity.depvariables["productFlow"] = new DWSIM.SharedClasses.Flowsheet.Optimization.SAVariable
    {
        objectID = "Product",
        propID = "Mass Flow",
        unit = "kg/s",
    };

    var loadedSensitivity = new DWSIM.SharedClasses.Flowsheet.Optimization.SensitivityAnalysisCase();
    loadedSensitivity.LoadData(sensitivity.SaveData());
    Assert(loadedSensitivity.name == "temperature-sweep", "Sensitivity case XML name roundtrip failed");
    Assert(loadedSensitivity.iv1.points == 7, "Sensitivity point count roundtrip failed");
    Assert(loadedSensitivity.depvariables.ContainsKey("productFlow"),
        "Sensitivity dependent variable roundtrip failed");

    var clonedSensitivity =
        (DWSIM.SharedClasses.Flowsheet.Optimization.SensitivityAnalysisCase)sensitivity.Clone();
    clonedSensitivity.iv1.points = 11;
    clonedSensitivity.depvariables["productFlow"].unit = "kg/h";
    Assert(sensitivity.iv1.points == 7, "Sensitivity clone should own independent variables");
    Assert(sensitivity.depvariables["productFlow"].unit == "kg/s",
        "Sensitivity clone should own dependent variables");

    var collections = new DWSIM.SharedClasses.DWSIM.Flowsheet.ObjectCollection();
    collections.OPT_OptimizationCollection.Add(optimization);
    collections.OPT_SensAnalysisCollection.Add(sensitivity);
    Assert(collections.OPT_OptimizationCollection.Single().name == "minimum-duty",
        "Object collection should expose concrete optimization cases");
    Assert(collections.OPT_SensAnalysisCollection.Single().name == "temperature-sweep",
        "Object collection should expose concrete sensitivity cases");

    var assemblyReferences = typeof(DWSIM.SharedClasses.Flowsheet.Optimization.OptimizationCase)
        .Assembly.GetReferencedAssemblies()
        .Select(reference => reference.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    Assert(!assemblyReferences.Contains("Ciloci.Flee"), "Analysis data assembly should not reference Ciloci.Flee");
    Assert(typeof(DWSIM.SharedClasses.Flowsheet.Optimization.OptimizationCase)
            .GetField("econtext")?.FieldType == typeof(object),
        "Expression runtime state should stay solver-owned");
}

static void SharedClassesExceptionsAreHeadless()
{
    var source = new InvalidOperationException("root failure");
    source.Data["DetailedDescription"] = "detailed failure";
    source.Data["UserAction"] = "retry";

    var processed = DWSIM.SharedClasses.ExceptionProcessing.ExceptionParser.ParseException(source);
    Assert(processed.Name == nameof(InvalidOperationException), "Processed exception name mismatch");
    Assert(processed.OriginalDescription == "root failure", "Processed exception message mismatch");
    Assert(processed.DetailedDescription == "detailed failure", "Processed exception details mismatch");
    Assert(processed.UserAction == "retry", "Processed exception user action mismatch");
    Assert(ReferenceEquals(processed.ExceptionObject, source), "Processed exception object mismatch");

    var aggregate = new AggregateException(new Exception("outer", new ApplicationException("inner")));
    var first = DWSIM.SharedClasses.ExceptionProcessing.ExceptionParser.GetFirstException(aggregate);
    Assert(first is ApplicationException, "Aggregate exception parser should return base exception");

    var id = Guid.NewGuid().ToString("N");
    DWSIM.SharedClasses.ExceptionProcessing.ExceptionList.Exceptions[id] = source;
    Assert(ReferenceEquals(DWSIM.SharedClasses.ExceptionProcessing.ExceptionList.Exceptions[id], source),
        "Exception list should store exception by id");
    DWSIM.SharedClasses.ExceptionProcessing.ExceptionList.Exceptions.Remove(id);

    var componentException = new DWSIM.SharedClasses.ComponentNotFoundException
    {
        ProductName = "Plugin",
        ProductVersion = "1.0",
        Base = source,
    };
    Assert(componentException.ProductName == "Plugin", "Component exception product name mismatch");
    Assert(ReferenceEquals(componentException.Base, source), "Component exception base mismatch");
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
