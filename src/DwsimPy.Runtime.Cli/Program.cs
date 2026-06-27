using System.Text.Json;
using DwsimPy.Runtime;

if (args.Length < 1 || args[0] is not (
    "unitops"
    or "inspect"
    or "properties"
    or "set-property"
    or "create"
    or "add-node"
    or "move-node"
    or "rename-node"
    or "connect"
    or "delete-node"
    or "save-copy"))
{
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  DwsimPy.Runtime.Cli unitops");
    Console.Error.WriteLine("  DwsimPy.Runtime.Cli inspect <flowsheet.dwxml|flowsheet.dwxmz>");
    Console.Error.WriteLine("  DwsimPy.Runtime.Cli properties <flowsheet.dwxml|flowsheet.dwxmz> <object-id-or-tag>");
    Console.Error.WriteLine("  DwsimPy.Runtime.Cli set-property <flowsheet.dwxml|flowsheet.dwxmz> <object-id-or-tag> <property> <value> <output.dwxml|output.dwxmz>");
    Console.Error.WriteLine("  DwsimPy.Runtime.Cli create <output.dwxml|output.dwxmz>");
    Console.Error.WriteLine("  DwsimPy.Runtime.Cli add-node <input> <object-type> <label> <x> <y> <output>");
    Console.Error.WriteLine("  DwsimPy.Runtime.Cli move-node <input> <object-id-or-tag> <x> <y> <output>");
    Console.Error.WriteLine("  DwsimPy.Runtime.Cli rename-node <input> <object-id-or-tag> <label> <output>");
    Console.Error.WriteLine("  DwsimPy.Runtime.Cli connect <input> <source-id-or-tag> <target-id-or-tag> <material|energy> <output>");
    Console.Error.WriteLine("  DwsimPy.Runtime.Cli delete-node <input> <object-id-or-tag> <output>");
    Console.Error.WriteLine("  DwsimPy.Runtime.Cli save-copy <flowsheet.dwxml|flowsheet.dwxmz> <output.dwxml|output.dwxmz>");
    return 2;
}

var engine = new XmlFlowsheetEngine();
var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

if (args[0] == "unitops")
{
    var unitOperations = engine.UnitOperations
        .OrderBy(u => u.Category)
        .ThenBy(u => u.DisplayName)
        .ThenBy(u => u.ObjectType)
        .ToList();
    Console.WriteLine(JsonSerializer.Serialize(unitOperations, jsonOptions));
    return 0;
}

if (args[0] == "create")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Missing output path.");
        return 2;
    }

    var outputPath = args[1];
    var created = engine.Create(Path.GetFileName(outputPath));
    Save(engine, created.Id, outputPath);
    return 0;
}

if (args.Length < 2)
{
    Console.Error.WriteLine("Missing input path.");
    return 2;
}

var inputPath = args[1];
var loaded = engine.Load(File.ReadAllBytes(inputPath), Path.GetFileName(inputPath));

if (args[0] == "save-copy")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Missing output path.");
        return 2;
    }

    var outputPath = args[2];
    Save(engine, loaded.Id, outputPath);
    return 0;
}

if (args[0] == "properties")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Missing object id or tag.");
        return 2;
    }

    var properties = engine.GetProperties(loaded.Id, args[2]);
    Console.WriteLine(JsonSerializer.Serialize(properties, jsonOptions));
    return 0;
}

if (args[0] == "set-property")
{
    if (args.Length < 6)
    {
        Console.Error.WriteLine("Missing object id, property name, value, or output path.");
        return 2;
    }

    var outputPath = args[5];
    engine.SetProperty(loaded.Id, args[2], args[3], args[4]);
    Save(engine, loaded.Id, outputPath);
    return 0;
}

if (args[0] == "add-node")
{
    if (args.Length < 7)
    {
        Console.Error.WriteLine("Missing object type, label, coordinates, or output path.");
        return 2;
    }

    var node = engine.AddNode(loaded.Id, new AddNodeRequest(
        args[2],
        args[3],
        double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture),
        double.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture)));
    Save(engine, loaded.Id, args[6]);
    Console.WriteLine(JsonSerializer.Serialize(node, jsonOptions));
    return 0;
}

if (args[0] == "move-node")
{
    if (args.Length < 6)
    {
        Console.Error.WriteLine("Missing object id, coordinates, or output path.");
        return 2;
    }

    engine.MoveNode(
        loaded.Id,
        args[2],
        double.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture),
        double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture));
    Save(engine, loaded.Id, args[5]);
    return 0;
}

if (args[0] == "rename-node")
{
    if (args.Length < 5)
    {
        Console.Error.WriteLine("Missing object id, label, or output path.");
        return 2;
    }

    engine.RenameNode(loaded.Id, args[2], args[3]);
    Save(engine, loaded.Id, args[4]);
    return 0;
}

if (args[0] == "connect")
{
    if (args.Length < 6)
    {
        Console.Error.WriteLine("Missing source, target, stream type, or output path.");
        return 2;
    }

    var edge = engine.Connect(loaded.Id, args[2], args[3], args[4]);
    Save(engine, loaded.Id, args[5]);
    Console.WriteLine(JsonSerializer.Serialize(edge, jsonOptions));
    return 0;
}

if (args[0] == "delete-node")
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("Missing object id or output path.");
        return 2;
    }

    engine.DeleteNode(loaded.Id, args[2]);
    Save(engine, loaded.Id, args[3]);
    return 0;
}

var graph = engine.GetGraph(loaded.Id);
var payload = new
{
    loaded.FileName,
    NodeCount = graph.Nodes.Count,
    EdgeCount = graph.Edges.Count,
    Nodes = graph.Nodes.Take(20),
    Edges = graph.Edges.Take(20)
};

Console.WriteLine(JsonSerializer.Serialize(payload, jsonOptions));
return 0;

static void Save(XmlFlowsheetEngine engine, string flowsheetId, string outputPath)
{
    var compressed = Path.GetExtension(outputPath).Equals(".dwxmz", StringComparison.OrdinalIgnoreCase);
    File.WriteAllBytes(outputPath, engine.Save(flowsheetId, compressed));
}
