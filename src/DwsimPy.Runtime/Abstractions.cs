namespace DwsimPy.Runtime;

public sealed record LoadedFlowsheet(string Id, string FileName);

public sealed record SolveResult(
    bool Success,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, object?> Results);

public sealed record FlowsheetGraph(
    IReadOnlyList<FlowsheetNode> Nodes,
    IReadOnlyList<FlowsheetEdge> Edges);

public sealed record FlowsheetNode(
    string Id,
    string Label,
    string ObjectType,
    double X,
    double Y,
    double Width,
    double Height,
    string? Status);

public sealed record FlowsheetEdge(
    string Id,
    string SourceId,
    string TargetId,
    string StreamType,
    int SourceConnectorIndex,
    int TargetConnectorIndex);

public sealed record AddNodeRequest(
    string ObjectType,
    string Label,
    double X,
    double Y,
    string? Id = null);

public interface IFlowsheetEngine
{
    IReadOnlyList<UnitOperationDescriptor> UnitOperations { get; }

    LoadedFlowsheet Create(string fileName);

    LoadedFlowsheet Load(byte[] document, string fileName);

    byte[] Save(string flowsheetId, bool compressed);

    SolveResult Solve(string flowsheetId);

    FlowsheetGraph GetGraph(string flowsheetId);

    IReadOnlyDictionary<string, object?> GetProperties(string flowsheetId, string objectId);

    void SetProperty(string flowsheetId, string objectId, string propertyName, object? value);

    FlowsheetNode AddNode(string flowsheetId, AddNodeRequest request);

    void MoveNode(string flowsheetId, string objectId, double x, double y);

    void RenameNode(string flowsheetId, string objectId, string label);

    FlowsheetEdge Connect(
        string flowsheetId,
        string sourceId,
        string targetId,
        string streamType = "material",
        int sourceConnectorIndex = 0,
        int targetConnectorIndex = 0);

    void DeleteNode(string flowsheetId, string objectId);

    void Close(string flowsheetId);
}
