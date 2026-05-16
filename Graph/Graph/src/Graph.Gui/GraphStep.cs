namespace Graph.Gui;

public readonly record struct GraphEdgeKey(string From, string To);

public enum GraphStepKind
{
    None,
    BoruvkaCheckEdge,
    BoruvkaAddEdge,
    DijkstraStart,
    DijkstraExtract,
    DijkstraRelax,
    DijkstraAlt,
    BellmanUpdate,
    FloydIntermediate,
    FloydUpdate,
    EdmondsKarpPath,
    TraversalEdge,
    TraversalVisit,
    CycleFound
}

public sealed record GraphStep(
    GraphStepKind Kind,
    string Text,
    IReadOnlyList<string> HighlightVertices,
    IReadOnlyList<GraphEdgeKey> HighlightEdges,
    string? A,
    string? B,
    string? C,
    double? Value
);
