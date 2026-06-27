using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace DwsimPy.Runtime;

public sealed class DwsimDocument
{
    private readonly XDocument _document;
    private readonly string _zipEntryName;
    private readonly UnitOperationRegistry _unitOperations;

    private DwsimDocument(
        XDocument document,
        string fileName,
        string? zipEntryName,
        UnitOperationRegistry unitOperations)
    {
        _document = document;
        FileName = fileName;
        _zipEntryName = string.IsNullOrWhiteSpace(zipEntryName) ? "flowsheet.xml" : zipEntryName;
        _unitOperations = unitOperations;
    }

    public string FileName { get; }

    public static DwsimDocument Create(string fileName, UnitOperationRegistry? unitOperations = null)
    {
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("DWSIM_Simulation_Data",
                new XElement("GeneralInfo",
                    new XElement("BuildVersion", "dwsimpy-net10"),
                    new XElement("SavedOn", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
                    new XElement("SavedWith", "DwsimPy.Runtime")),
                new XElement("SimulationObjects"),
                new XElement("Settings"),
                new XElement("DynamicProperties"),
                new XElement("GraphicObjects"),
                new XElement("PropertyPackages"),
                new XElement("Compounds"),
                new XElement("ReactionSets"),
                new XElement("Reactions"),
                new XElement("StoredSolutions"),
                new XElement("DynamicsManager"),
                new XElement("OptimizationCases"),
                new XElement("SensitivityAnalysis"),
                new XElement("PetroleumAssays"),
                new XElement("WatchItems"),
                new XElement("ScriptItems"),
                new XElement("ChartItems"),
                new XElement("Spreadsheet"),
                new XElement("PanelLayout")));

        return new DwsimDocument(document, fileName, "flowsheet.xml", unitOperations ?? UnitOperationRegistry.CreateDefault());
    }

    public static DwsimDocument Load(
        byte[] document,
        string fileName,
        UnitOperationRegistry? unitOperations = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var registry = unitOperations ?? UnitOperationRegistry.CreateDefault();

        if (IsZipPayload(document, fileName))
        {
            using var stream = new MemoryStream(document, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            var entry = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name))
                .OrderByDescending(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .ThenBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (entry is null)
            {
                throw new InvalidDataException("The DWXMZ archive does not contain a flowsheet XML entry.");
            }

            using var entryStream = entry.Open();
            return new DwsimDocument(
                XDocument.Load(entryStream, LoadOptions.PreserveWhitespace),
                fileName,
                entry.FullName,
                registry);
        }

        using var xmlStream = new MemoryStream(document, writable: false);
        return new DwsimDocument(XDocument.Load(xmlStream, LoadOptions.PreserveWhitespace), fileName, null, registry);
    }

    public byte[] Save(bool compressed)
    {
        var xml = SaveXml();
        if (!compressed)
        {
            return xml;
        }

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(_zipEntryName, CompressionLevel.Optimal);
            using var entryStream = entry.Open();
            entryStream.Write(xml);
        }

        return stream.ToArray();
    }

    public FlowsheetGraph GetGraph()
    {
        var nodes = GraphicObjects()
            .Select(go => new FlowsheetNode(
                Id: ChildValue(go, "Name") ?? "",
                Label: ChildValue(go, "Tag") ?? ChildValue(go, "Name") ?? "",
                ObjectType: ResolveObjectType(go),
                X: ParseDouble(ChildValue(go, "X")),
                Y: ParseDouble(ChildValue(go, "Y")),
                Width: ParseDouble(ChildValue(go, "Width")),
                Height: ParseDouble(ChildValue(go, "Height")),
                Status: ChildValue(go, "Status")))
            .Where(n => !string.IsNullOrWhiteSpace(n.Id))
            .ToList();

        var edges = new Dictionary<string, FlowsheetEdge>(StringComparer.Ordinal);
        foreach (var go in GraphicObjects())
        {
            AddOutputEdges(go, edges);
            AddInputEdges(go, edges);
        }

        return new FlowsheetGraph(nodes, edges.Values.ToList());
    }

    public IReadOnlyDictionary<string, object?> GetProperties(string objectId)
    {
        var so = FindSimulationObject(objectId);
        if (so is null)
        {
            throw new KeyNotFoundException($"Simulation object not found: {objectId}");
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var child in so.Elements().Where(e => !e.HasElements))
        {
            result[child.Name.LocalName] = child.Value;
        }

        var go = FindGraphicObject(objectId);
        if (go is not null)
        {
            foreach (var child in go.Elements().Where(e => !e.HasElements))
            {
                result[$"graphic.{child.Name.LocalName}"] = child.Value;
            }
        }

        return result;
    }

    public FlowsheetNode AddNode(AddNodeRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ObjectType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Label);

        var descriptor = _unitOperations.Resolve(request.ObjectType);
        var id = string.IsNullOrWhiteSpace(request.Id)
            ? $"{descriptor.Prefix}-{Guid.NewGuid()}"
            : request.Id;

        if (FindGraphicObject(id) is not null)
        {
            throw new InvalidOperationException($"Object already exists: {id}");
        }

        GraphicObjectsElement().Add(CreateGraphicObject(id, request.Label, request.X, request.Y, descriptor));
        if (!string.IsNullOrWhiteSpace(descriptor.SimulationType))
        {
            SimulationObjectsElement().Add(CreateSimulationObject(id, request.Label, descriptor));
        }

        return new FlowsheetNode(
            id,
            request.Label,
            descriptor.ObjectType,
            request.X,
            request.Y,
            descriptor.Width,
            descriptor.Height,
            "NotCalculated");
    }

    public void MoveNode(string objectId, double x, double y)
    {
        var go = FindGraphicObject(objectId) ?? throw new KeyNotFoundException($"Graphic object not found: {objectId}");
        SetChildValue(go, "X", FormatDouble(x));
        SetChildValue(go, "Y", FormatDouble(y));
    }

    public void RenameNode(string objectId, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        var go = FindGraphicObject(objectId) ?? throw new KeyNotFoundException($"Graphic object not found: {objectId}");
        SetChildValue(go, "Tag", label);

        var so = FindSimulationObject(objectId);
        if (so is not null)
        {
            SetChildValue(so, "ComponentDescription", label);
        }
    }

    public FlowsheetEdge Connect(
        string sourceId,
        string targetId,
        string streamType = "material",
        int sourceConnectorIndex = 0,
        int targetConnectorIndex = 0)
    {
        var source = FindGraphicObject(sourceId) ?? throw new KeyNotFoundException($"Source graphic object not found: {sourceId}");
        var target = FindGraphicObject(targetId) ?? throw new KeyNotFoundException($"Target graphic object not found: {targetId}");
        var normalizedStreamType = string.Equals(streamType, "energy", StringComparison.OrdinalIgnoreCase) ? "energy" : "material";

        var output = EnsureConnector(source, "OutputConnectors", sourceConnectorIndex);
        output.SetAttributeValue("IsAttached", "true");
        output.SetAttributeValue("ConnType", "ConOut");
        output.SetAttributeValue("AttachedToObjID", ChildValue(target, "Name"));
        output.SetAttributeValue("AttachedToConnIndex", targetConnectorIndex.ToString(CultureInfo.InvariantCulture));
        output.SetAttributeValue("AttachedToEnergyConn", normalizedStreamType == "energy" ? "True" : "False");

        var input = EnsureConnector(target, "InputConnectors", targetConnectorIndex);
        input.SetAttributeValue("IsAttached", "true");
        input.SetAttributeValue("ConnType", normalizedStreamType == "energy" ? "ConEn" : "ConIn");
        input.SetAttributeValue("AttachedFromObjID", ChildValue(source, "Name"));
        input.SetAttributeValue("AttachedFromConnIndex", sourceConnectorIndex.ToString(CultureInfo.InvariantCulture));
        input.SetAttributeValue("AttachedFromEnergyConn", normalizedStreamType == "energy" ? "True" : "False");

        var edgeId = $"{ChildValue(source, "Name")}->{ChildValue(target, "Name")}:{normalizedStreamType}:{sourceConnectorIndex}:{targetConnectorIndex}";
        return new FlowsheetEdge(
            edgeId,
            ChildValue(source, "Name") ?? "",
            ChildValue(target, "Name") ?? "",
            normalizedStreamType,
            sourceConnectorIndex,
            targetConnectorIndex);
    }

    public void DeleteNode(string objectId)
    {
        var go = FindGraphicObject(objectId) ?? throw new KeyNotFoundException($"Graphic object not found: {objectId}");
        var id = ChildValue(go, "Name") ?? objectId;

        foreach (var connector in GraphicObjects().SelectMany(AllConnectors))
        {
            if (string.Equals(connector.Attribute("AttachedToObjID")?.Value, id, StringComparison.Ordinal)
                || string.Equals(connector.Attribute("AttachedFromObjID")?.Value, id, StringComparison.Ordinal))
            {
                ClearConnector(connector);
            }
        }

        go.Remove();
        FindSimulationObject(id)?.Remove();
    }

    public void SetProperty(string objectId, string propertyName, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var targetGraphic = propertyName.StartsWith("graphic.", StringComparison.Ordinal);
        var elementName = targetGraphic ? propertyName["graphic.".Length..] : propertyName;
        var parent = targetGraphic ? FindGraphicObject(objectId) : FindSimulationObject(objectId);
        if (parent is null)
        {
            throw new KeyNotFoundException($"Object not found: {objectId}");
        }

        var element = parent.Element(elementName);
        if (element is null)
        {
            throw new KeyNotFoundException($"Property '{propertyName}' not found on object '{objectId}'.");
        }
        if (element.HasElements)
        {
            throw new InvalidOperationException(
                $"Property '{propertyName}' is structured XML and cannot be set as a scalar value.");
        }

        element.Value = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }

    private byte[] SaveXml()
    {
        using var stream = new MemoryStream();
        var settings = new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            OmitXmlDeclaration = _document.Declaration is null
        };
        using (var writer = XmlWriter.Create(stream, settings))
        {
            _document.Save(writer);
        }

        return stream.ToArray();
    }

    private static bool IsZipPayload(byte[] document, string fileName)
    {
        if (Path.GetExtension(fileName).Equals(".dwxmz", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return document.Length >= 4
            && document[0] == 0x50
            && document[1] == 0x4B
            && document[2] == 0x03
            && document[3] == 0x04;
    }

    private XElement SimulationObjectsElement() =>
        _document.Root?.Element("SimulationObjects")
        ?? throw new InvalidDataException("Missing SimulationObjects section.");

    private XElement GraphicObjectsElement() =>
        _document.Root?.Element("GraphicObjects")
        ?? throw new InvalidDataException("Missing GraphicObjects section.");

    private IEnumerable<XElement> SimulationObjects() =>
        _document.Root?.Element("SimulationObjects")?.Elements("SimulationObject") ?? Enumerable.Empty<XElement>();

    private IEnumerable<XElement> GraphicObjects() =>
        _document.Root?.Element("GraphicObjects")?.Elements("GraphicObject") ?? Enumerable.Empty<XElement>();

    private string ResolveObjectType(XElement graphicObject)
    {
        var graphicObjectType = ChildValue(graphicObject, "ObjectType") ?? ChildValue(graphicObject, "Type") ?? "";
        var simulationType = FindSimulationObjectByName(ChildValue(graphicObject, "Name") ?? "")?.Element("Type")?.Value ?? "";

        if (!string.IsNullOrWhiteSpace(simulationType)
            && _unitOperations.TryResolveBySimulationType(simulationType, out var bySimulationType)
            && (string.Equals(bySimulationType.GraphicObjectType, graphicObjectType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(bySimulationType.ObjectType, graphicObjectType, StringComparison.OrdinalIgnoreCase)
                || string.Equals(graphicObjectType, "External", StringComparison.OrdinalIgnoreCase)))
        {
            return bySimulationType.ObjectType;
        }

        if (!string.IsNullOrWhiteSpace(graphicObjectType)
            && _unitOperations.TryGet(graphicObjectType, out var byGraphicObjectType))
        {
            return byGraphicObjectType.ObjectType;
        }

        return graphicObjectType;
    }

    private static XElement CreateSimulationObject(string id, string label, UnitOperationDescriptor descriptor) =>
        new("SimulationObject",
            new XElement("Type", descriptor.SimulationType),
            new XElement("ObjectClass", descriptor.ObjectClass),
            new XElement("SupportsDynamicMode", "false"),
            new XElement("HasPropertiesForDynamicMode", "false"),
            new XElement("MobileCompatible", "true"),
            new XElement("ComponentDescription", label),
            new XElement("ComponentName", id),
            new XElement("Name2", id),
            new XElement("DynamicsOnly", "false"),
            new XElement("Visible", "true"),
            new XElement("OverrideCalculationRoutine", "false"),
            new XElement("Calculated", "false"),
            new XElement("DebugMode", "false"),
            new XElement("ErrorMessage", ""),
            new XElement("IsAdjustAttached", "false"),
            new XElement("AttachedAdjustId", ""),
            new XElement("AdjustVarType", "None"),
            new XElement("IsSpecAttached", "false"),
            new XElement("AttachedSpecId", ""),
            new XElement("SpecVarType", "None"),
            new XElement("Name", id),
            new XElement("DynamicProperties", ""),
            new XElement("DynamicPropertiesDescriptions", ""),
            new XElement("DynamicPropertiesUnitTypes", ""),
            new XElement("AttachedUtilities", ""),
            new XElement("PropertyPackage", ""));

    private static XElement CreateGraphicObject(
        string id,
        string label,
        double x,
        double y,
        UnitOperationDescriptor descriptor) =>
        new("GraphicObject",
            new XElement("Type", descriptor.GraphicType),
            new XElement("SemiTransparent", "false"),
            new XElement("LineWidth", "1"),
            new XElement("GradientMode", "true"),
            new XElement("LineColor", "#ff4682b4"),
            new XElement("LineColorDark", "#fff5f5f5"),
            new XElement("Fill", "false"),
            new XElement("FillColor", "#fff5f5f5"),
            new XElement("FillColorDark", "#ffffffff"),
            new XElement("GradientColor1", "#00000000"),
            new XElement("GradientColor2", "#ffffffff"),
            new XElement("FontSize", "10"),
            new XElement("OverrideColors", "false"),
            new XElement("Calculated", "false"),
            new XElement("Active", "true"),
            new XElement("Description", label),
            new XElement("FlippedH", "false"),
            new XElement("FlippedV", "false"),
            new XElement("IsEnergyStream", string.Equals(descriptor.ObjectType, "EnergyStream", StringComparison.OrdinalIgnoreCase) ? "true" : "false"),
            new XElement("ObjectType", descriptor.GraphicObjectType),
            new XElement("Shape", "0"),
            new XElement("ShapeOverride", "DefaultShape"),
            new XElement("Status", "NotCalculated"),
            new XElement("AutoSize", "false"),
            new XElement("Height", FormatDouble(descriptor.Height)),
            new XElement("IsConnector", "false"),
            new XElement("Name", id),
            new XElement("Tag", label),
            new XElement("Width", FormatDouble(descriptor.Width)),
            new XElement("X", FormatDouble(x)),
            new XElement("Y", FormatDouble(y)),
            new XElement("Selected", "false"),
            new XElement("Rotation", "0"),
            new XElement("InputConnectors", Enumerable.Range(0, descriptor.InputConnectorCount).Select(_ => NewConnector())),
            new XElement("OutputConnectors", Enumerable.Range(0, descriptor.OutputConnectorCount).Select(_ => NewConnector())),
            new XElement("EnergyConnector", NewConnector()),
            new XElement("SpecialConnectors"));

    private static XElement NewConnector() => new("Connector", new XAttribute("IsAttached", "false"));

    private XElement? FindSimulationObjectByName(string objectId) =>
        SimulationObjects().FirstOrDefault(so =>
            string.Equals(ChildValue(so, "Name"), objectId, StringComparison.Ordinal)
            || string.Equals(ChildValue(so, "ComponentName"), objectId, StringComparison.Ordinal));

    private XElement? FindSimulationObject(string objectId) =>
        SimulationObjects().FirstOrDefault(so =>
            string.Equals(ChildValue(so, "Name"), objectId, StringComparison.Ordinal)
            || string.Equals(ChildValue(so, "ComponentName"), objectId, StringComparison.Ordinal)
            || string.Equals(FindGraphicObject(objectId)?.Element("Name")?.Value, ChildValue(so, "Name"), StringComparison.Ordinal));

    private XElement? FindGraphicObject(string objectId) =>
        GraphicObjects().FirstOrDefault(go =>
            string.Equals(ChildValue(go, "Name"), objectId, StringComparison.Ordinal)
            || string.Equals(ChildValue(go, "Tag"), objectId, StringComparison.Ordinal));

    private static string? ChildValue(XElement element, string name) => element.Element(name)?.Value;

    private static void SetChildValue(XElement element, string name, string value)
    {
        var child = element.Element(name);
        if (child is null)
        {
            element.Add(new XElement(name, value));
            return;
        }

        child.Value = value;
    }

    private static string FormatDouble(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);

    private static double ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0d;

    private static int ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : -1;

    private static bool IsAttached(XElement connector) =>
        bool.TryParse(connector.Attribute("IsAttached")?.Value, out var attached) && attached;

    private static IEnumerable<XElement> AllConnectors(XElement graphicObject) =>
        (graphicObject.Element("InputConnectors")?.Elements("Connector") ?? Enumerable.Empty<XElement>())
        .Concat(graphicObject.Element("OutputConnectors")?.Elements("Connector") ?? Enumerable.Empty<XElement>())
        .Concat(graphicObject.Element("EnergyConnector")?.Elements("Connector") ?? Enumerable.Empty<XElement>())
        .Concat(graphicObject.Element("SpecialConnectors")?.Elements("Connector") ?? Enumerable.Empty<XElement>());

    private static XElement EnsureConnector(XElement graphicObject, string sectionName, int index)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Connector index must be non-negative.");
        }

        var section = graphicObject.Element(sectionName);
        if (section is null)
        {
            section = new XElement(sectionName);
            graphicObject.Add(section);
        }

        while (section.Elements("Connector").Count() <= index)
        {
            section.Add(NewConnector());
        }

        return section.Elements("Connector").ElementAt(index);
    }

    private static void ClearConnector(XElement connector)
    {
        connector.RemoveAttributes();
        connector.SetAttributeValue("IsAttached", "false");
    }

    private static string StreamTypeFor(XElement go, XElement connector)
    {
        if (string.Equals(ChildValue(go, "ObjectType"), "EnergyStream", StringComparison.OrdinalIgnoreCase))
        {
            return "energy";
        }
        if (string.Equals(connector.Attribute("AttachedToEnergyConn")?.Value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(connector.Attribute("AttachedFromEnergyConn")?.Value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(connector.Attribute("ConnType")?.Value, "ConEn", StringComparison.OrdinalIgnoreCase))
        {
            return "energy";
        }

        return "material";
    }

    private static void AddEdge(
        IDictionary<string, FlowsheetEdge> edges,
        XElement go,
        string sourceId,
        string targetId,
        string streamType,
        int sourceConnectorIndex,
        int targetConnectorIndex)
    {
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
        {
            return;
        }

        var id = $"{sourceId}->{targetId}:{streamType}:{sourceConnectorIndex}:{targetConnectorIndex}";
        edges.TryAdd(id, new FlowsheetEdge(
            id,
            sourceId,
            targetId,
            streamType,
            sourceConnectorIndex,
            targetConnectorIndex));
    }

    private static void AddOutputEdges(XElement go, IDictionary<string, FlowsheetEdge> edges)
    {
        var sourceId = ChildValue(go, "Name") ?? "";
        var index = 0;
        foreach (var connector in go.Element("OutputConnectors")?.Elements("Connector") ?? Enumerable.Empty<XElement>())
        {
            if (IsAttached(connector))
            {
                AddEdge(
                    edges,
                    go,
                    sourceId,
                    connector.Attribute("AttachedToObjID")?.Value ?? "",
                    StreamTypeFor(go, connector),
                    index,
                    ParseInt(connector.Attribute("AttachedToConnIndex")?.Value));
            }

            index++;
        }
    }

    private static void AddInputEdges(XElement go, IDictionary<string, FlowsheetEdge> edges)
    {
        var targetId = ChildValue(go, "Name") ?? "";
        var index = 0;
        foreach (var connector in go.Element("InputConnectors")?.Elements("Connector") ?? Enumerable.Empty<XElement>())
        {
            if (IsAttached(connector))
            {
                AddEdge(
                    edges,
                    go,
                    connector.Attribute("AttachedFromObjID")?.Value ?? "",
                    targetId,
                    StreamTypeFor(go, connector),
                    ParseInt(connector.Attribute("AttachedFromConnIndex")?.Value),
                    index);
            }

            index++;
        }
    }

}
