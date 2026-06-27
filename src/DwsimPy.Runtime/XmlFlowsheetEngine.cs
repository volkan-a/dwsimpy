namespace DwsimPy.Runtime;

public sealed class XmlFlowsheetEngine : IFlowsheetEngine
{
    private readonly Dictionary<string, DwsimDocument> _documents = new(StringComparer.Ordinal);
    private readonly UnitOperationRegistry _unitOperations;

    public XmlFlowsheetEngine(UnitOperationRegistry? unitOperations = null)
    {
        _unitOperations = unitOperations ?? UnitOperationRegistry.CreateDefault();
    }

    public IReadOnlyList<UnitOperationDescriptor> UnitOperations => _unitOperations.All;

    public LoadedFlowsheet Create(string fileName)
    {
        var document = DwsimDocument.Create(fileName, _unitOperations);
        var id = Guid.NewGuid().ToString("N");
        _documents.Add(id, document);
        return new LoadedFlowsheet(id, fileName);
    }

    public LoadedFlowsheet Load(byte[] document, string fileName)
    {
        var loaded = DwsimDocument.Load(document, fileName, _unitOperations);
        var id = Guid.NewGuid().ToString("N");
        _documents.Add(id, loaded);
        return new LoadedFlowsheet(id, fileName);
    }

    public byte[] Save(string flowsheetId, bool compressed) => GetDocument(flowsheetId).Save(compressed);

    public SolveResult Solve(string flowsheetId)
    {
        _ = GetDocument(flowsheetId);
        throw new NotSupportedException(
            "The pure .NET 10 XML runtime can manipulate flowsheet documents, but the DWSIM solver port is not implemented yet.");
    }

    public FlowsheetGraph GetGraph(string flowsheetId) => GetDocument(flowsheetId).GetGraph();

    public IReadOnlyDictionary<string, object?> GetProperties(string flowsheetId, string objectId) =>
        GetDocument(flowsheetId).GetProperties(objectId);

    public void SetProperty(string flowsheetId, string objectId, string propertyName, object? value) =>
        GetDocument(flowsheetId).SetProperty(objectId, propertyName, value);

    public FlowsheetNode AddNode(string flowsheetId, AddNodeRequest request) =>
        GetDocument(flowsheetId).AddNode(request);

    public void MoveNode(string flowsheetId, string objectId, double x, double y) =>
        GetDocument(flowsheetId).MoveNode(objectId, x, y);

    public void RenameNode(string flowsheetId, string objectId, string label) =>
        GetDocument(flowsheetId).RenameNode(objectId, label);

    public FlowsheetEdge Connect(
        string flowsheetId,
        string sourceId,
        string targetId,
        string streamType = "material",
        int sourceConnectorIndex = 0,
        int targetConnectorIndex = 0) =>
        GetDocument(flowsheetId).Connect(sourceId, targetId, streamType, sourceConnectorIndex, targetConnectorIndex);

    public void DeleteNode(string flowsheetId, string objectId) => GetDocument(flowsheetId).DeleteNode(objectId);

    public void Close(string flowsheetId) => _documents.Remove(flowsheetId);

    private DwsimDocument GetDocument(string flowsheetId)
    {
        if (_documents.TryGetValue(flowsheetId, out var document))
        {
            return document;
        }

        throw new KeyNotFoundException($"Flowsheet not loaded: {flowsheetId}");
    }
}
