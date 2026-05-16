using System.Globalization;
using System.Text.RegularExpressions;

namespace Graph.Web.Models;

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

        var match = BoruvkaEdgeAdd.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.BoruvkaAddEdge;
            a = match.Groups["u"].Value.Trim();
            b = match.Groups["v"].Value.Trim();
            vertices.Add(a);
            vertices.Add(b);
            edges.Add(NormalizeEdge(a, b, isDirected));
            return new GraphStep(kind, text, vertices, edges, a, b, null, null);
        }

        match = BoruvkaEdgeCheck.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.BoruvkaCheckEdge;
            a = match.Groups["u"].Value.Trim();
            b = match.Groups["v"].Value.Trim();
            vertices.Add(a);
            vertices.Add(b);
            edges.Add(NormalizeEdge(a, b, isDirected));
            return new GraphStep(kind, text, vertices, edges, a, b, null, null);
        }

        match = TraversalEdge.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.TraversalEdge;
            a = match.Groups["u"].Value.Trim();
            b = match.Groups["v"].Value.Trim();
            vertices.Add(a);
            vertices.Add(b);
            edges.Add(NormalizeEdge(a, b, isDirected));
            return new GraphStep(kind, text, vertices, edges, a, b, null, null);
        }

        match = TraversalVisit.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.TraversalVisit;
            a = match.Groups["u"].Value.Trim();
            vertices.Add(a);
            return new GraphStep(kind, text, vertices, edges, a, null, null, null);
        }

        match = CycleFound.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.CycleFound;
            var nodes = match.Groups["path"].Value.Split("->", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            vertices.AddRange(nodes);
            for (var i = 0; i + 1 < nodes.Length; i++)
            {
                edges.Add(NormalizeEdge(nodes[i], nodes[i + 1], isDirected));
            }

            return new GraphStep(kind, text, vertices, edges, null, null, null, null);
        }

        match = DijkstraStart.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.DijkstraStart;
            a = match.Groups["s"].Value.Trim();
            b = match.Groups["t"].Value.Trim();
            vertices.Add(a);
            vertices.Add(b);
            return new GraphStep(kind, text, vertices, edges, a, b, null, null);
        }

        match = DijkstraExtract.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.DijkstraExtract;
            a = match.Groups["u"].Value.Trim();
            vertices.Add(a);
            value = TryParseDouble(match.Groups["d"].Value);
            return new GraphStep(kind, text, vertices, edges, a, null, null, value);
        }

        match = DijkstraRelax.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.DijkstraRelax;
            a = match.Groups["u"].Value.Trim();
            b = match.Groups["v"].Value.Trim();
            vertices.Add(a);
            vertices.Add(b);
            edges.Add(NormalizeEdge(a, b, isDirected));
            value = TryParseDouble(match.Groups["d"].Value);
            return new GraphStep(kind, text, vertices, edges, a, b, null, value);
        }

        match = DijkstraAlt.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.DijkstraAlt;
            a = match.Groups["u"].Value.Trim();
            b = match.Groups["v"].Value.Trim();
            vertices.Add(a);
            vertices.Add(b);
            edges.Add(NormalizeEdge(a, b, isDirected));
            value = TryParseDouble(match.Groups["d"].Value);
            return new GraphStep(kind, text, vertices, edges, a, b, null, value);
        }

        match = BellmanUpdate.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.BellmanUpdate;
            a = match.Groups["v"].Value.Trim();
            vertices.Add(a);
            value = TryParseDouble(match.Groups["d"].Value);
            return new GraphStep(kind, text, vertices, edges, a, null, null, value);
        }

        match = FloydIntermediate.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.FloydIntermediate;
            a = match.Groups["u"].Value.Trim();
            vertices.Add(a);
            return new GraphStep(kind, text, vertices, edges, a, null, null, null);
        }

        match = FloydUpdate.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.FloydUpdate;
            a = match.Groups["i"].Value.Trim();
            b = match.Groups["j"].Value.Trim();
            c = match.Groups["k"].Value.Trim();
            value = TryParseDouble(match.Groups["d"].Value);
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            edges.Add(NormalizeEdge(a, b, true));
            return new GraphStep(kind, text, vertices, edges, a, b, c, value);
        }

        match = EdmondsKarpPath.Match(text);
        if (match.Success)
        {
            kind = GraphStepKind.EdmondsKarpPath;
            var nodes = match.Groups["path"].Value.Split("->", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            vertices.AddRange(nodes);
            for (var i = 0; i + 1 < nodes.Length; i++)
            {
                edges.Add(NormalizeEdge(nodes[i], nodes[i + 1], true));
            }

            value = TryParseDouble(match.Groups["f"].Value);
            a = nodes.FirstOrDefault();
            b = nodes.LastOrDefault();
            return new GraphStep(kind, text, vertices, edges, a, b, null, value);
        }

        return new GraphStep(kind, text, vertices, edges, a, b, c, value);
    }

    public static GraphEdgeKey NormalizeEdge(string from, string to, bool isDirected)
    {
        if (isDirected)
        {
            return new GraphEdgeKey(from, to);
        }

        return string.CompareOrdinal(from, to) <= 0
            ? new GraphEdgeKey(from, to)
            : new GraphEdgeKey(to, from);
    }

    private static double? TryParseDouble(string text)
    {
        if (double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        return double.TryParse(text, out value) ? value : null;
    }
}
