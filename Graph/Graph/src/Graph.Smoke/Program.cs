using Graph;

static List<string> ResolveFiles(string[] args)
{
    if (args.Length > 0)
    {
        var result = new List<string>();
        foreach (var a in args)
        {
            if (File.Exists(a))
            {
                result.Add(a);
                continue;
            }

            var dir = Path.GetDirectoryName(a);
            var pat = Path.GetFileName(a);

            if (string.IsNullOrWhiteSpace(dir)) dir = Directory.GetCurrentDirectory();
            if (string.IsNullOrWhiteSpace(pat)) pat = "*";

            if (Directory.Exists(dir))
            {
                result.AddRange(Directory.GetFiles(dir, pat));
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
    }

    var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    if (string.IsNullOrWhiteSpace(documents) || !Directory.Exists(documents))
    {
        documents = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
    }

    if (string.IsNullOrWhiteSpace(documents) || !Directory.Exists(documents))
    {
        documents = Directory.GetCurrentDirectory();
    }

    return Directory.GetFiles(documents, "graph*.txt").OrderBy(x => x).ToList();
}

static (int passed, int failed) RunCase(string name, Action action)
{
    try
    {
        action();
        Console.WriteLine($"  OK  {name}");
        return (1, 0);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL {name}: {ex.GetType().Name}: {ex.Message}");
        return (0, 1);
    }
}

static Action<string> SilentLog() => _ => { };

var files = ResolveFiles(args);
if (files.Count == 0)
{
    Console.WriteLine("Файлы не найдены.");
    return;
}

int totalPassed = 0;
int totalFailed = 0;

foreach (var path in files)
{
    Console.WriteLine($"=== {Path.GetFileName(path)} ===");

    Graph<string> g;
    try
    {
        g = new Graph<string>(path, s => s, null);
        Console.WriteLine($"  Загружено. Ориентированный: {g.IsDirected}, взвешенный: {g.IsWeighted}, V={g.Vertices.Count}, E={g.GetEdgeList().Count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  FAIL load: {ex.GetType().Name}: {ex.Message}");
        totalFailed++;
        continue;
    }

    var vs = g.Vertices.OrderBy(v => v).ToList();
    var start = vs.FirstOrDefault();
    var end = vs.LastOrDefault();

    if (vs.Count == 0)
    {
        Console.WriteLine("  Пустой граф. Пропуск алгоритмов.");
        continue;
    }

    (int passed, int failed) r;

    r = RunCase("ToString()", () => _ = g.ToString());
    totalPassed += r.passed; totalFailed += r.failed;

    r = RunCase("GetIsolatedVertices()", () => _ = g.GetIsolatedVertices());
    totalPassed += r.passed; totalFailed += r.failed;

    r = RunCase("GetNonAdjacentVertices(start)", () => _ = g.GetNonAdjacentVertices(start!));
    totalPassed += r.passed; totalFailed += r.failed;

    r = RunCase("RemoveEdgesToPendantVertices()", () => _ = g.RemoveEdgesToPendantVertices().GetEdgeList());
    totalPassed += r.passed; totalFailed += r.failed;

    r = RunCase("SaveToFile(edge-list)", () =>
    {
        var outPath = Path.Combine(Path.GetTempPath(), $"graph_smoke_{Guid.NewGuid():N}_edges.txt");
        g.SaveToFile(outPath, GraphSaveFormat.EdgeList);
        _ = File.ReadAllText(outPath);
    });
    totalPassed += r.passed; totalFailed += r.failed;

    r = RunCase("SaveToFile(adjacency-list)", () =>
    {
        var outPath = Path.Combine(Path.GetTempPath(), $"graph_smoke_{Guid.NewGuid():N}_adj.txt");
        g.SaveToFile(outPath, GraphSaveFormat.AdjacencyList);
        _ = File.ReadAllText(outPath);
    });
    totalPassed += r.passed; totalFailed += r.failed;

    r = RunCase("GetFundamentalCyclesDFS(log)", () => _ = g.GetFundamentalCyclesDFS(SilentLog()));
    totalPassed += r.passed; totalFailed += r.failed;

    r = RunCase("GetFundamentalCyclesBFS(log)", () => _ = g.GetFundamentalCyclesBFS(SilentLog()));
    totalPassed += r.passed; totalFailed += r.failed;

    if (vs.Count >= 2)
    {
        r = RunCase("FindEquidistantVertexBFS(u,v)", () => _ = g.FindEquidistantVertexBFS(vs[0], vs[1]));
        totalPassed += r.passed; totalFailed += r.failed;

        r = RunCase("FindEquidistantVertexDFS(u,v)", () => _ = g.FindEquidistantVertexDFS(vs[0], vs[1]));
        totalPassed += r.passed; totalFailed += r.failed;
    }

    if (!g.IsDirected && g.IsWeighted)
    {
        r = RunCase("GetMinimumSpanningTreeBoruvka(log)", () => _ = g.GetMinimumSpanningTreeBoruvka(SilentLog()).GetEdgeList());
        totalPassed += r.passed; totalFailed += r.failed;
    }
    else
    {
        Console.WriteLine("  SKIP MST(Boruvka): требуется неориентированный взвешенный граф");
    }

    if (g.IsWeighted && start != null && end != null)
    {
        if (g.GetEdgeList().Any(e => e.Weight < 0))
        {
            Console.WriteLine("  SKIP Dijkstra: в графе есть отрицательные веса");
        }
        else
        {
            r = RunCase("GetShortestPathsDijkstra(start,end,log)", () => _ = g.GetShortestPathsDijkstra(start, end, SilentLog()));
            totalPassed += r.passed; totalFailed += r.failed;
        }

        r = RunCase("GetKShortestPathsBellmanFord(start,end,k,log)", () => _ = g.GetKShortestPathsBellmanFord(start, end, 3, SilentLog()));
        totalPassed += r.passed; totalFailed += r.failed;

        r = RunCase("GetNegativeCyclesFloydWarshall(log)", () => _ = g.GetNegativeCyclesFloydWarshall(SilentLog()));
        totalPassed += r.passed; totalFailed += r.failed;
    }
    else
    {
        Console.WriteLine("  SKIP Dijkstra/Bellman/Floyd: требуется взвешенный граф");
    }

    if (g.IsDirected && g.IsWeighted && start != null && end != null)
    {
        if (g.GetEdgeList().Any(e => e.Weight < 0))
        {
            Console.WriteLine("  SKIP MaxFlow: в графе есть отрицательные пропускные способности");
        }
        else
        {
            r = RunCase("GetMaxFlowEdmondsKarp(source,sink,log)", () => _ = g.GetMaxFlowEdmondsKarp(start, end, SilentLog()));
            totalPassed += r.passed; totalFailed += r.failed;
        }
    }
    else
    {
        Console.WriteLine("  SKIP MaxFlow: требуется ориентированный взвешенный граф");
    }
}

Console.WriteLine($"=== Итог: OK={totalPassed}, FAIL={totalFailed} ===");
