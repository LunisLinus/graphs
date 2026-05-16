namespace Graph.Gui;

public sealed class GraphRenderState
{
    public IReadOnlyDictionary<string, PointF> Positions { get; set; } = new Dictionary<string, PointF>();
    public IReadOnlyList<(string From, string To, double Weight)> Edges { get; set; } = Array.Empty<(string, string, double)>();
    public IReadOnlyList<string> Vertices { get; set; } = Array.Empty<string>();
    public bool IsDirected { get; set; }
    public bool IsWeighted { get; set; }
    public IReadOnlySet<string> HighlightVertices { get; set; } = new HashSet<string>();
    public IReadOnlySet<GraphEdgeKey> HighlightEdges { get; set; } = new HashSet<GraphEdgeKey>();
    public IReadOnlySet<string> VisitedVertices { get; set; } = new HashSet<string>();
    public IReadOnlySet<GraphEdgeKey> CommittedEdges { get; set; } = new HashSet<GraphEdgeKey>();
    public IReadOnlyDictionary<string, string> VertexAnnotations { get; set; } = new Dictionary<string, string>();
    public string? SelectedVertex { get; set; }
}
