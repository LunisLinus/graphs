using System.Text.RegularExpressions;

namespace Graph.Gui;

public static partial class GraphStepParsing
{
    private static readonly Regex BoruvkaEdgeCheck = new(@"Проверяем ребро\s+(?<u>.+?)\s*-\s*(?<v>.+?)\s*\(", RegexOptions.Compiled);
    private static readonly Regex BoruvkaEdgeAdd = new(@"Добавлено ребро\s+(?<u>.+?)\s*-\s*(?<v>.+?)\s*\(", RegexOptions.Compiled);
    private static readonly Regex DijkstraStart = new(@"Dijkstra:\s*старт\s+(?<s>.+?)\s*,\s*цель\s+(?<t>.+)$", RegexOptions.Compiled);
    private static readonly Regex DijkstraExtract = new(@"Извлечена вершина\s+(?<u>.+?)\s+с расстоянием\s+(?<d>[-+0-9.,Ee]+)", RegexOptions.Compiled);
    private static readonly Regex DijkstraRelax = new(@"Релаксация:\s+(?<u>.+?)\s*->\s*(?<v>.+?)\s*,\s*новое расстояние\s+(?<d>[-+0-9.,Ee]+)", RegexOptions.Compiled);
    private static readonly Regex DijkstraAlt = new(@"Альтернатива:\s+(?<u>.+?)\s*->\s*(?<v>.+?)\s*,\s*расстояние\s+(?<d>[-+0-9.,Ee]+)", RegexOptions.Compiled);
    private static readonly Regex BellmanUpdate = new(@"Обновление:\s+найден путь до\s+(?<v>.+?)\s+со стоимостью\s+(?<d>[-+0-9.,Ee]+)", RegexOptions.Compiled);
    private static readonly Regex FloydIntermediate = new(@"промежуточная вершина\s+(?<u>.+?)\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FloydUpdate = new(@"Обновление:\s+(?<i>.+?)\s*->\s*(?<j>.+?)\s*=\s*(?<d>[-+0-9.,Ee]+)\s+через\s+(?<k>.+)$", RegexOptions.Compiled);
    private static readonly Regex EdmondsKarpPath = new(@"Найдён увеличивающий путь:\s+(?<path>.+?)\s+с потоком\s+(?<f>[-+0-9.,Ee]+)", RegexOptions.Compiled);
    private static readonly Regex TraversalEdge = new(@"^(?<alg>DFS|BFS):\s*ребро\s+(?<u>.+?)\s*->\s*(?<v>.+)$", RegexOptions.Compiled);
    private static readonly Regex TraversalVisit = new(@"^(?<alg>DFS|BFS):\s*посещаем\s+(?<u>.+)$", RegexOptions.Compiled);
    private static readonly Regex CycleFound = new(@"^(?<alg>DFS|BFS):\s*найден цикл\s+(?<path>.+)$", RegexOptions.Compiled);

    public static GraphStep Parse(string text, bool isDirected)
    {
        var vertices = new List<string>();
        var edges = new List<GraphEdgeKey>();
        var kind = GraphStepKind.None;
        string? a = null;
        string? b = null;
        string? c = null;
        double? value = null;

        var m = BoruvkaEdgeAdd.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.BoruvkaAddEdge;
            a = m.Groups["u"].Value.Trim();
            b = m.Groups["v"].Value.Trim();
            vertices.Add(a);
            vertices.Add(b);
            edges.Add(NormalizeEdge(a, b, isDirected));
            return new GraphStep(kind, text, vertices, edges, a, b, null, null);
        }

        m = BoruvkaEdgeCheck.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.BoruvkaCheckEdge;
            a = m.Groups["u"].Value.Trim();
            b = m.Groups["v"].Value.Trim();
            vertices.Add(a);
            vertices.Add(b);
            edges.Add(NormalizeEdge(a, b, isDirected));
            return new GraphStep(kind, text, vertices, edges, a, b, null, null);
        }

        m = TraversalEdge.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.TraversalEdge;
            a = m.Groups["u"].Value.Trim();
            b = m.Groups["v"].Value.Trim();
            vertices.Add(a);
            vertices.Add(b);
            edges.Add(NormalizeEdge(a, b, isDirected));
            return new GraphStep(kind, text, vertices, edges, a, b, null, null);
        }

        m = TraversalVisit.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.TraversalVisit;
            a = m.Groups["u"].Value.Trim();
            vertices.Add(a);
            return new GraphStep(kind, text, vertices, edges, a, null, null, null);
        }

        m = CycleFound.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.CycleFound;
            var nodes = m.Groups["path"].Value.Split("->", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < nodes.Length; i++)
            {
                vertices.Add(nodes[i]);
            }

            for (int i = 0; i + 1 < nodes.Length; i++)
            {
                edges.Add(NormalizeEdge(nodes[i], nodes[i + 1], isDirected));
            }

            return new GraphStep(kind, text, vertices, edges, null, null, null, null);
        }

        m = DijkstraStart.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.DijkstraStart;
            a = m.Groups["s"].Value.Trim();
            b = m.Groups["t"].Value.Trim();
            vertices.Add(a);
            vertices.Add(b);
            return new GraphStep(kind, text, vertices, edges, a, b, null, null);
        }

        m = DijkstraExtract.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.DijkstraExtract;
            a = m.Groups["u"].Value.Trim();
            vertices.Add(a);
            value = TryParseDouble(m.Groups["d"].Value);
            return new GraphStep(kind, text, vertices, edges, a, null, null, value);
        }

        m = DijkstraRelax.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.DijkstraRelax;
            var u = m.Groups["u"].Value.Trim();
            var v = m.Groups["v"].Value.Trim();
            vertices.Add(u);
            vertices.Add(v);
            edges.Add(NormalizeEdge(u, v, isDirected));
            value = TryParseDouble(m.Groups["d"].Value);
            return new GraphStep(kind, text, vertices, edges, u, v, null, value);
        }

        m = DijkstraAlt.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.DijkstraAlt;
            var u = m.Groups["u"].Value.Trim();
            var v = m.Groups["v"].Value.Trim();
            vertices.Add(u);
            vertices.Add(v);
            edges.Add(NormalizeEdge(u, v, isDirected));
            value = TryParseDouble(m.Groups["d"].Value);
            return new GraphStep(kind, text, vertices, edges, u, v, null, value);
        }

        m = BellmanUpdate.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.BellmanUpdate;
            a = m.Groups["v"].Value.Trim();
            vertices.Add(a);
            value = TryParseDouble(m.Groups["d"].Value);
            return new GraphStep(kind, text, vertices, edges, a, null, null, value);
        }

        m = FloydIntermediate.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.FloydIntermediate;
            a = m.Groups["u"].Value.Trim();
            vertices.Add(a);
            return new GraphStep(kind, text, vertices, edges, a, null, null, null);
        }

        m = FloydUpdate.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.FloydUpdate;
            a = m.Groups["i"].Value.Trim();
            b = m.Groups["j"].Value.Trim();
            c = m.Groups["k"].Value.Trim();
            value = TryParseDouble(m.Groups["d"].Value);
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            edges.Add(NormalizeEdge(a, b, true));
            return new GraphStep(kind, text, vertices, edges, a, b, c, value);
        }

        m = EdmondsKarpPath.Match(text);
        if (m.Success)
        {
            kind = GraphStepKind.EdmondsKarpPath;
            var nodes = m.Groups["path"].Value.Split("->", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < nodes.Length; i++)
            {
                vertices.Add(nodes[i]);
            }

            for (int i = 0; i + 1 < nodes.Length; i++)
            {
                edges.Add(NormalizeEdge(nodes[i], nodes[i + 1], true));
            }

            value = TryParseDouble(m.Groups["f"].Value);
            a = nodes.Length > 0 ? nodes[0] : null;
            b = nodes.Length > 0 ? nodes[^1] : null;
            return new GraphStep(kind, text, vertices, edges, a, b, null, value);
        }

        return new GraphStep(kind, text, vertices, edges, a, b, c, value);
    }

    private static double? TryParseDouble(string text)
    {
        if (double.TryParse(text.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
            return v;

        if (double.TryParse(text, out v)) return v;
        return null;
    }

    public static GraphEdgeKey NormalizeEdge(string from, string to, bool isDirected)
    {
        if (isDirected) return new GraphEdgeKey(from, to);

        return string.CompareOrdinal(from, to) <= 0
            ? new GraphEdgeKey(from, to)
            : new GraphEdgeKey(to, from);
    }
}
