using System.Text;

namespace Graph
{
    public struct Edge<T>
    {
        public T From { get; }
        public T To { get; }
        public double Weight { get; }

        public Edge(T from, T to, double weight = 1.0)
        {
            From = from;
            To = to;
            Weight = weight;
        }

        public override string ToString()
        {
            return $"{From} -> {To} : {Weight}";
        }
    }

    public enum GraphSaveFormat
    {
        EdgeList,
        AdjacencyList
    }

    public class Graph<T> where T : notnull, IComparable<T>
    {
        private Dictionary<T, Dictionary<T, double>> _adjacencyList;

        public bool IsDirected { get; }
        public bool IsWeighted { get; }

        public Graph(bool isDirected = false, bool isWeighted = false)
        {
            IsDirected = isDirected;
            IsWeighted = isWeighted;
            _adjacencyList = new Dictionary<T, Dictionary<T, double>>();
        }

        public Graph(Graph<T> other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            IsDirected = other.IsDirected;
            IsWeighted = other.IsWeighted;
            _adjacencyList = new Dictionary<T, Dictionary<T, double>>();

            foreach (var vertex in other._adjacencyList)
            {
                var neighbors = new Dictionary<T, double>(vertex.Value);
                _adjacencyList.Add(vertex.Key, neighbors);
            }
        }

        public Graph(string filePath, Func<string, T> parser, Action<string>? log = null)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("Файл не найден", filePath);

            var lines = File.ReadAllLines(filePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (lines.Length < 2) throw new ArgumentException("Неверный формат файла: слишком мало строк.");

            var headerParts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            IsDirected = headerParts.Contains("DIRECTED", StringComparer.OrdinalIgnoreCase);
            IsWeighted = headerParts.Contains("WEIGHTED", StringComparer.OrdinalIgnoreCase);

            _adjacencyList = new Dictionary<T, Dictionary<T, double>>();

            var vertices = lines[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var vStr in vertices)
            {
                AddVertex(parser(vStr));
            }

            for (int i = 2; i < lines.Length; i++)
            {
                var parts = lines[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                T from = parser(parts[0]);
                T to = parser(parts[1]);
                double weight = 1.0;

                if (IsWeighted && parts.Length > 2)
                {
                    if (!double.TryParse(parts[2], out weight))
                        weight = 1.0;
                }

                try
                {
                    AddEdge(from, to, weight);
                }
                catch (Exception ex)
                {
                    log?.Invoke($"Ошибка при обработке строки {i + 1}: {ex.Message}");
                }
            }
        }

        public Graph(IEnumerable<T> vertices, bool isDirected, bool isWeighted) : this(isDirected, isWeighted)
        {
            foreach (var v in vertices)
            {
                AddVertex(v);
            }
        }

        public bool AddVertex(T vertex)
        {
            if (_adjacencyList.ContainsKey(vertex))
                return false;

            _adjacencyList[vertex] = new Dictionary<T, double>();
            return true;
        }

        public bool RemoveVertex(T vertex)
        {
            if (!_adjacencyList.ContainsKey(vertex))
                return false;

            _adjacencyList.Remove(vertex);

            foreach (var v in _adjacencyList)
            {
                if (v.Value.ContainsKey(vertex))
                {
                    v.Value.Remove(vertex);
                }
            }

            return true;
        }

        public void AddEdge(T from, T to, double weight = 1.0)
        {
            if (!_adjacencyList.ContainsKey(from)) throw new ArgumentException($"Вершина {from} не существует.");
            if (!_adjacencyList.ContainsKey(to)) throw new ArgumentException($"Вершина {to} не существует.");

            if (!IsWeighted) weight = 1.0;

            if (_adjacencyList[from].ContainsKey(to))
                throw new InvalidOperationException($"Ребро из {from} в {to} уже существует.");

            _adjacencyList[from][to] = weight;

            if (!IsDirected)
            {
                if (!from.Equals(to))
                {
                    if (_adjacencyList[to].ContainsKey(from))
                        throw new InvalidOperationException(
                            $"Ребро из {to} в {from} уже существует (Ошибка согласованности).");

                    _adjacencyList[to][from] = weight;
                }
            }
        }

        public bool RemoveEdge(T from, T to)
        {
            if (!_adjacencyList.ContainsKey(from)) return false;

            bool removed = _adjacencyList[from].Remove(to);

            if (!IsDirected && removed && !from.Equals(to))
            {
                if (_adjacencyList.ContainsKey(to))
                {
                    _adjacencyList[to].Remove(from);
                }
            }

            return removed;
        }

        public List<Edge<T>> GetEdgeList()
        {
            var edges = new List<Edge<T>>();
            var seen = new HashSet<string>();

            foreach (var kvp in _adjacencyList)
            {
                T from = kvp.Key;
                foreach (var innerKvp in kvp.Value)
                {
                    T to = innerKvp.Key;
                    double w = innerKvp.Value;

                    if (!IsDirected)
                    {
                        if (from.CompareTo(to) <= 0)
                        {
                            edges.Add(new Edge<T>(from, to, w));
                        }
                    }
                    else
                    {
                        edges.Add(new Edge<T>(from, to, w));
                    }
                }
            }

            return edges;
        }

        public Graph<T> ConvertTo(bool isDirected, bool isWeighted, double defaultWeightForNewWeighted = 1.0)
        {
            var converted = new Graph<T>(isDirected, isWeighted);
            foreach (var v in _adjacencyList.Keys)
            {
                converted.AddVertex(v);
            }

            double WeightFor(Edge<T> e) => isWeighted ? (IsWeighted ? e.Weight : defaultWeightForNewWeighted) : 1.0;

            if (!isDirected && IsDirected)
            {
                var pairs = new Dictionary<(T, T), double>();
                foreach (var e in GetEdgeList())
                {
                    var a = e.From;
                    var b = e.To;
                    var u = a.CompareTo(b) <= 0 ? a : b;
                    var v = a.CompareTo(b) <= 0 ? b : a;

                    var w = WeightFor(e);
                    if (pairs.TryGetValue((u, v), out var existing))
                    {
                        pairs[(u, v)] = Math.Min(existing, w);
                    }
                    else
                    {
                        pairs[(u, v)] = w;
                    }
                }

                foreach (var kvp in pairs)
                {
                    converted.AddEdge(kvp.Key.Item1, kvp.Key.Item2, kvp.Value);
                }

                return converted;
            }

            if (isDirected && !IsDirected)
            {
                foreach (var e in GetEdgeList())
                {
                    var w = WeightFor(e);
                    converted.AddEdge(e.From, e.To, w);
                }

                return converted;
            }

            foreach (var e in GetEdgeList())
            {
                converted.AddEdge(e.From, e.To, WeightFor(e));
            }

            return converted;
        }

        public void SaveToFile(string filePath, GraphSaveFormat format = GraphSaveFormat.EdgeList)
        {
            using (var writer = new StreamWriter(filePath))
            {
                string type = IsDirected ? "DIRECTED" : "UNDIRECTED";
                string weighted = IsWeighted ? "WEIGHTED" : "UNWEIGHTED";
                writer.WriteLine($"{type} {weighted}");

                writer.WriteLine(string.Join(" ", _adjacencyList.Keys));

                if (format == GraphSaveFormat.EdgeList)
                {
                    var edges = GetEdgeList();
                    foreach (var edge in edges)
                    {
                        string line = $"{edge.From} {edge.To}";
                        if (IsWeighted)
                        {
                            line += $" {edge.Weight}";
                        }

                        writer.WriteLine(line);
                    }
                }
                else
                {
                    foreach (var kvp in _adjacencyList)
                    {
                        var sb = new StringBuilder();
                        sb.Append(kvp.Key);

                        if (kvp.Value.Count > 0)
                        {
                            sb.Append(":");
                            foreach (var neighbor in kvp.Value)
                            {
                                if (IsWeighted)
                                    sb.Append($" {neighbor.Key}({neighbor.Value})");
                                else
                                    sb.Append($" {neighbor.Key}");
                            }
                        }

                        writer.WriteLine(sb.ToString());
                    }
                }
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            string directedStr = IsDirected ? "Да" : "Нет";
            string weightedStr = IsWeighted ? "Да" : "Нет";
            sb.AppendLine($"Граф (Ориентированный: {directedStr}, Взвешенный: {weightedStr})");
            sb.AppendLine($"Количество вершин: {_adjacencyList.Count}");
            foreach (var kvp in _adjacencyList)
            {
                sb.Append($"{kvp.Key}: ");
                if (kvp.Value.Count == 0)
                {
                    sb.AppendLine("(изолированная)");
                }
                else
                {
                    var neighbors = kvp.Value.Select(n => IsWeighted ? $"{n.Key}({n.Value})" : $"{n.Key}");
                    sb.AppendLine(string.Join(", ", neighbors));
                }
            }

            return sb.ToString();
        }

        public bool ContainsVertex(T vertex) => _adjacencyList.ContainsKey(vertex);

        public IReadOnlyCollection<T> Vertices => _adjacencyList.Keys;

        public IReadOnlyDictionary<T, IReadOnlyDictionary<T, double>> GetAdjacencyListSnapshot()
        {
            var snapshot = new Dictionary<T, IReadOnlyDictionary<T, double>>(_adjacencyList.Count);
            foreach (var kvp in _adjacencyList)
            {
                snapshot[kvp.Key] = new Dictionary<T, double>(kvp.Value);
            }

            return snapshot;
        }

        public List<T> GetNonAdjacentVertices(T vertex)
        {
            if (!_adjacencyList.ContainsKey(vertex))
                throw new ArgumentException($"Вершина {vertex} не найдена.");

            var nonAdjacent = new List<T>();
            var neighbors = _adjacencyList[vertex];

            foreach (var v in _adjacencyList.Keys)
            {
                if (!v.Equals(vertex) && !neighbors.ContainsKey(v))
                {
                    nonAdjacent.Add(v);
                }
            }

            return nonAdjacent;
        }

        public List<T> GetIsolatedVertices()
        {
            var isolated = new List<T>();
            var allVertices = _adjacencyList.Keys.ToList();

            var incomingEdges = new HashSet<T>();
            foreach (var kvp in _adjacencyList)
            {
                foreach (var neighbor in kvp.Value.Keys)
                {
                    incomingEdges.Add(neighbor);
                }
            }

            foreach (var v in allVertices)
            {
                bool hasOutgoing = _adjacencyList[v].Count > 0;

                if (IsDirected)
                {
                    bool hasIncoming = incomingEdges.Contains(v);
                    if (!hasOutgoing && !hasIncoming)
                    {
                        isolated.Add(v);
                    }
                }
                else
                {
                    if (!hasOutgoing)
                    {
                        isolated.Add(v);
                    }
                }
            }

            return isolated;
        }

        public Graph<T> RemoveEdgesToPendantVertices()
        {
            var newGraph = new Graph<T>(this);

            var degrees = new Dictionary<T, int>();
            foreach (var v in _adjacencyList.Keys)
            {
                degrees[v] = 0;
            }

            foreach (var kvp in _adjacencyList)
            {
                T u = kvp.Key;
                degrees[u] += kvp.Value.Count;

                foreach (var v in kvp.Value.Keys)
                {
                    if (IsDirected)
                    {
                        if (!degrees.ContainsKey(v)) degrees[v] = 0;
                        degrees[v]++;
                    }
                }
            }

            if (!IsDirected)
            {
                foreach (var v in _adjacencyList.Keys)
                {
                    int degree = 0;
                    if (_adjacencyList[v].ContainsKey(v))
                    {
                        degree = _adjacencyList[v].Count + 1;
                    }
                    else
                    {
                        degree = _adjacencyList[v].Count;
                    }

                    degrees[v] = degree;
                }
            }

            var pendantVertices = degrees.Where(kvp => kvp.Value == 1).Select(kvp => kvp.Key).ToHashSet();

            var edgesToRemove = new List<(T From, T To)>();

            foreach (var kvp in _adjacencyList)
            {
                T u = kvp.Key;
                foreach (var v in kvp.Value.Keys)
                {
                    if (pendantVertices.Contains(v))
                    {
                        edgesToRemove.Add((u, v));
                    }
                }
            }

            foreach (var edge in edgesToRemove)
            {
                newGraph.RemoveEdge(edge.From, edge.To);
            }

            return newGraph;
        }

        public List<List<T>> GetFundamentalCyclesDFS(Action<string>? log = null)
        {
            var cycles = new List<List<T>>();
            var visited = new HashSet<T>();
            var recursionStack = new HashSet<T>();
            var parent = new Dictionary<T, T>();

            foreach (var vertex in _adjacencyList.Keys)
            {
                if (!visited.Contains(vertex))
                {
                    DFSFindCycles(vertex, visited, recursionStack, parent, cycles, log);
                }
            }

            return cycles;
        }

        private void DFSFindCycles(T current, HashSet<T> visited, HashSet<T> recursionStack,
            Dictionary<T, T> parent, List<List<T>> cycles, Action<string>? log)
        {
            visited.Add(current);
            recursionStack.Add(current);
            log?.Invoke($"DFS: посещаем {current}");

            if (_adjacencyList.ContainsKey(current))
            {
                foreach (var neighbor in _adjacencyList[current].Keys)
                {
                    log?.Invoke($"DFS: ребро {current} -> {neighbor}");
                    if (recursionStack.Contains(neighbor))
                    {
                        if (!IsDirected && parent.ContainsKey(current) && parent[current].Equals(neighbor))
                            continue;

                        var cycle = new List<T>();
                        cycle.Add(neighbor);

                        var temp = current;
                        while (!temp.Equals(neighbor))
                        {
                            cycle.Add(temp);
                            if (parent.ContainsKey(temp))
                                temp = parent[temp];
                            else
                                break;
                        }

                        cycle.Reverse();

                        var correctCycle = new List<T>();
                        var p = current;
                        while (!p.Equals(neighbor))
                        {
                            correctCycle.Add(p);
                            p = parent[p];
                        }

                        correctCycle.Add(neighbor);
                        correctCycle.Reverse();
                        correctCycle.Add(neighbor);

                        cycles.Add(correctCycle);
                        log?.Invoke($"DFS: найден цикл {string.Join(" -> ", correctCycle)}");
                    }
                    else if (!visited.Contains(neighbor))
                    {
                        parent[neighbor] = current;
                        DFSFindCycles(neighbor, visited, recursionStack, parent, cycles, log);
                    }
                }
            }

            recursionStack.Remove(current);
        }

        public List<List<T>> GetFundamentalCyclesBFS(Action<string>? log = null)
        {
            var cycles = new List<List<T>>();
            var visited = new HashSet<T>();
            var parent = new Dictionary<T, T>();
            var treeEdges = new HashSet<(T, T)>();

            foreach (var start in _adjacencyList.Keys)
            {
                if (visited.Contains(start)) continue;

                var queue = new Queue<T>();
                queue.Enqueue(start);
                visited.Add(start);

                while (queue.Count > 0)
                {
                    var u = queue.Dequeue();
                    log?.Invoke($"BFS: посещаем {u}");

                    foreach (var v in _adjacencyList[u].Keys)
                    {
                        log?.Invoke($"BFS: ребро {u} -> {v}");
                        if (!visited.Contains(v))
                        {
                            visited.Add(v);
                            parent[v] = u;
                            treeEdges.Add((u, v));
                            queue.Enqueue(v);
                        }
                    }
                }
            }

            foreach (var u in _adjacencyList.Keys)
            {
                foreach (var v in _adjacencyList[u].Keys)
                {
                    if (treeEdges.Contains((u, v)) || treeEdges.Contains((v, u)))
                        continue;

                    if (!IsDirected && u.CompareTo(v) > 0)
                        continue;

                    if (!parent.ContainsKey(u) && !parent.ContainsKey(v))
                        continue;

                    var pathU = GetPathToRoot(u, parent);
                    var pathV = GetPathToRoot(v, parent);

                    var setV = new HashSet<T>(pathV);

                    T lca = default!;
                    bool found = false;

                    foreach (var node in pathU)
                    {
                        if (setV.Contains(node))
                        {
                            lca = node;
                            found = true;
                            break;
                        }
                    }

                    if (!found) continue;

                    var cycle = new List<T>();

                    var temp = u;
                    while (!temp.Equals(lca))
                    {
                        cycle.Add(temp);
                        temp = parent[temp];
                    }

                    cycle.Add(lca);

                    var stack = new Stack<T>();
                    temp = v;

                    while (!temp.Equals(lca))
                    {
                        stack.Push(temp);
                        temp = parent[temp];
                    }

                    while (stack.Count > 0)
                        cycle.Add(stack.Pop());

                    cycle.Add(u);

                    cycles.Add(cycle);
                    log?.Invoke($"BFS: найден цикл {string.Join(" -> ", cycle)}");
                }
            }

            return cycles;
        }

        private List<T> GetPathToRoot(T node, Dictionary<T, T> parent)
        {
            var path = new List<T>();
            var current = node;

            path.Add(current);

            while (parent.ContainsKey(current))
            {
                current = parent[current];
                path.Add(current);
            }

            return path;
        }

        public T FindEquidistantVertexBFS(T u, T v)
        {
            if (!_adjacencyList.ContainsKey(u)) throw new ArgumentException($"Вершина {u} не найдена.");
            if (!_adjacencyList.ContainsKey(v)) throw new ArgumentException($"Вершина {v} не найдена.");

            var distU = GetDistancesBFS(u);
            var distV = GetDistancesBFS(v);

            foreach (var node in _adjacencyList.Keys)
            {
                if (distU.ContainsKey(node) && distV.ContainsKey(node))
                {
                    if (distU[node] == distV[node])
                    {
                        return node;
                    }
                }
            }

            return default;
        }

        private Dictionary<T, int> GetDistancesBFS(T start)
        {
            var dist = new Dictionary<T, int>();
            var queue = new Queue<T>();

            dist[start] = 0;
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var curr = queue.Dequeue();
                if (_adjacencyList.ContainsKey(curr))
                {
                    foreach (var neighbor in _adjacencyList[curr].Keys)
                    {
                        if (!dist.ContainsKey(neighbor))
                        {
                            dist[neighbor] = dist[curr] + 1;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            return dist;
        }

        public T FindEquidistantVertexDFS(T u, T v)
        {
            if (!_adjacencyList.ContainsKey(u)) throw new ArgumentException($"Вершина {u} не найдена.");
            if (!_adjacencyList.ContainsKey(v)) throw new ArgumentException($"Вершина {v} не найдена.");

            var depthU = new Dictionary<T, int>();
            DFSCollectDepths(u, 0, new HashSet<T>(), depthU);

            return DFSCheckDepths(v, 0, new HashSet<T>(), depthU);
        }

        private void DFSCollectDepths(T current, int depth, HashSet<T> visited, Dictionary<T, int> depths)
        {
            visited.Add(current);
            if (!depths.ContainsKey(current))
            {
                depths[current] = depth;
            }

            if (_adjacencyList.ContainsKey(current))
            {
                foreach (var neighbor in _adjacencyList[current].Keys)
                {
                    if (!visited.Contains(neighbor))
                    {
                        DFSCollectDepths(neighbor, depth + 1, visited, depths);
                    }
                }
            }
        }

        private T DFSCheckDepths(T current, int depth, HashSet<T> visited, Dictionary<T, int> depthsU)
        {
            visited.Add(current);

            if (depthsU.ContainsKey(current) && depthsU[current] == depth)
            {
                return current;
            }

            if (_adjacencyList.ContainsKey(current))
            {
                foreach (var neighbor in _adjacencyList[current].Keys)
                {
                    if (!visited.Contains(neighbor))
                    {
                        var result = DFSCheckDepths(neighbor, depth + 1, visited, depthsU);
                        if (!EqualityComparer<T>.Default.Equals(result, default)) return result;
                    }
                }
            }

            return default;
        }

        public Graph<T> GetMinimumSpanningTreeBoruvka(Action<string>? log = null)
        {
            if (IsDirected)
                throw new InvalidOperationException("Алгоритм Борувка работает только для неориентированных графов.");

            var mst = new Graph<T>(false, IsWeighted);

            foreach (var v in _adjacencyList.Keys)
                mst.AddVertex(v);

            var parent = new Dictionary<T, T>();
            var rank = new Dictionary<T, int>();

            foreach (var v in _adjacencyList.Keys)
            {
                parent[v] = v;
                rank[v] = 0;
            }

            T Find(T i)
            {
                if (!parent[i].Equals(i))
                    parent[i] = Find(parent[i]);
                return parent[i];
            }

            void Union(T i, T j)
            {
                T rootI = Find(i);
                T rootJ = Find(j);

                if (!rootI.Equals(rootJ))
                {
                    if (rank[rootI] < rank[rootJ])
                        parent[rootI] = rootJ;
                    else if (rank[rootI] > rank[rootJ])
                        parent[rootJ] = rootI;
                    else
                    {
                        parent[rootJ] = rootI;
                        rank[rootI]++;
                    }

                    log?.Invoke($"Union: {rootI} и {rootJ} объединены");
                }
            }

            int numTrees = _adjacencyList.Count;
            int step = 1;

            while (numTrees > 1)
            {
                log?.Invoke($"\n=== Шаг {step} ===");

                var cheapest = new Dictionary<T, (T u, T v, double w)>();
                bool edgeAdded = false;

                foreach (var u in _adjacencyList.Keys)
                {
                    foreach (var kvp in _adjacencyList[u])
                    {
                        T v = kvp.Key;
                        double w = kvp.Value;

                        if (!IsDirected)
                        {
                            int comparison;

                            if (u is IComparable<T> cGeneric)
                                comparison = cGeneric.CompareTo(v);
                            else if (u is IComparable c)
                                comparison = c.CompareTo(v);
                            else
                                comparison = u.GetHashCode().CompareTo(v.GetHashCode());

                            if (comparison > 0) continue;
                        }

                        T setU = Find(u);
                        T setV = Find(v);

                        log?.Invoke($"Проверяем ребро {u} - {v} (вес {w})");

                        if (!setU.Equals(setV))
                        {
                            log?.Invoke($"Компоненты: {setU} и {setV}");

                            if (!cheapest.ContainsKey(setU) || w < cheapest[setU].w)
                            {
                                cheapest[setU] = (u, v, w);
                                log?.Invoke($"Минимальное ребро для компоненты {setU}: {u}-{v} ({w})");
                            }

                            if (!cheapest.ContainsKey(setV) || w < cheapest[setV].w)
                            {
                                cheapest[setV] = (u, v, w);
                                log?.Invoke($"Минимальное ребро для компоненты {setV}: {u}-{v} ({w})");
                            }
                        }
                    }
                }

                var edgesToAdd = cheapest.Values.Distinct().ToList();

                log?.Invoke("\nДобавляем рёбра:");

                foreach (var edge in edgesToAdd)
                {
                    T setU = Find(edge.u);
                    T setV = Find(edge.v);

                    if (!setU.Equals(setV))
                    {
                        log?.Invoke($"Добавлено ребро {edge.u} - {edge.v} (вес {edge.w})");

                        mst.AddEdge(edge.u, edge.v, edge.w);
                        Union(setU, setV);

                        numTrees--;
                        edgeAdded = true;
                    }
                }

                if (!edgeAdded)
                {
                    log?.Invoke("Нет рёбер для объединения. Граф несвязный.");
                    break;
                }

                step++;
            }

            log?.Invoke("\n=== Готовое минимальное остовное дерево ===");

            foreach (var edge in mst.GetEdgeList())
            {
                log?.Invoke(edge.ToString());
            }

            return mst;
        }

        public (double distance, List<List<T>> paths) GetShortestPathsDijkstra(T start, T end, Action<string>? log = null)
        {
            if (!_adjacencyList.ContainsKey(start)) throw new ArgumentException($"Вершина {start} не найдена.");
            if (!_adjacencyList.ContainsKey(end)) throw new ArgumentException($"Вершина {end} не найдена.");

            foreach (var u in _adjacencyList)
            {
                foreach (var v in u.Value)
                {
                    if (v.Value < 0)
                        throw new InvalidOperationException(
                            "Граф содержит ребра с отрицательным весом. Алгоритм Дейкстры неприменим.");
                }
            }

            var distances = new Dictionary<T, double>();
            var predecessors = new Dictionary<T, HashSet<T>>();
            var pq = new PriorityQueue<T, double>();

            foreach (var v in _adjacencyList.Keys)
            {
                distances[v] = double.PositiveInfinity;
            }

            distances[start] = 0;
            pq.Enqueue(start, 0);

            double minEndDist = double.PositiveInfinity;
            log?.Invoke($"Dijkstra: старт {start}, цель {end}");

            while (pq.Count > 0)
            {
                if (!pq.TryDequeue(out T u, out double d)) break;
                if (d > minEndDist) break;

                if (d > distances[u]) continue;
                log?.Invoke($"Извлечена вершина {u} с расстоянием {d}");

                if (u.Equals(end))
                {
                    minEndDist = d;
                    log?.Invoke($"Достигнута цель {end} с расстоянием {d}");
                }

                if (_adjacencyList.ContainsKey(u))
                {
                    foreach (var kvp in _adjacencyList[u])
                    {
                        T v = kvp.Key;
                        double weight = kvp.Value;

                        double newDist = distances[u] + weight;

                        if (newDist < distances[v])
                        {
                            distances[v] = newDist;
                            predecessors[v] = new HashSet<T> { u };
                            pq.Enqueue(v, newDist);
                            log?.Invoke($"Релаксация: {u} -> {v}, новое расстояние {newDist}");
                        }
                        else if (Math.Abs(newDist - distances[v]) < 1e-9)
                        {
                            if (!predecessors.ContainsKey(v)) predecessors[v] = new HashSet<T>();
                            predecessors[v].Add(u);
                            log?.Invoke($"Альтернатива: {u} -> {v}, расстояние {newDist}");
                        }
                    }
                }
            }


            if (!distances.ContainsKey(end) || double.IsPositiveInfinity(distances[end]))
            {
                return (double.PositiveInfinity, new List<List<T>>());
            }

            var paths = new List<List<T>>();
            var pathStack = new List<T>();
            ReconstructPathsDFS(end, start, predecessors, pathStack, paths);

            return (distances[end], paths);
        }

        public (double distance, List<List<T>> paths) GetShortestPathsBellmanFord(T start, T end)
        {
            if (!_adjacencyList.ContainsKey(start)) throw new ArgumentException($"Вершина {start} не найдена.");
            if (!_adjacencyList.ContainsKey(end)) throw new ArgumentException($"Вершина {end} не найдена.");

            var distances = new Dictionary<T, double>();
            var predecessors = new Dictionary<T, HashSet<T>>();

            foreach (var v in _adjacencyList.Keys)
            {
                distances[v] = double.PositiveInfinity;
            }

            distances[start] = 0;

            int V = _adjacencyList.Count;

            var allEdges = new List<(T u, T v, double w)>();
            foreach (var u in _adjacencyList.Keys)
            {
                foreach (var kvp in _adjacencyList[u])
                {
                    allEdges.Add((u, kvp.Key, kvp.Value));
                }
            }

            for (int i = 0; i < V - 1; i++)
            {
                bool changed = false;

                foreach (var edge in allEdges)
                {
                    T u = edge.u;
                    T v = edge.v;
                    double w = edge.w;

                    if (double.IsPositiveInfinity(distances[u])) continue;

                    if (distances[u] + w < distances[v] - 1e-9)
                    {
                        distances[v] = distances[u] + w;
                        predecessors[v] = new HashSet<T> { u };
                        changed = true;
                    }
                    else if (Math.Abs(distances[u] + w - distances[v]) < 1e-9)
                    {
                        if (!predecessors.ContainsKey(v)) predecessors[v] = new HashSet<T>();
                        if (predecessors[v].Add(u))
                        {
                        }
                    }
                }

                if (!changed) break;
            }

            foreach (var edge in allEdges)
            {
                T u = edge.u;
                T v = edge.v;
                double w = edge.w;

                if (double.IsPositiveInfinity(distances[u])) continue;

                if (distances[u] + w < distances[v] - 1e-9)
                {
                    throw new InvalidOperationException("Граф содержит цикл отрицательного веса.");
                }
            }

            if (double.IsPositiveInfinity(distances[end]))
            {
                return (double.PositiveInfinity, new List<List<T>>());
            }

            var paths = new List<List<T>>();
            var pathStack = new List<T>();
            var reconstructionVisited = new HashSet<T>();

            ReconstructPathsDFS(end, start, predecessors, pathStack, paths, reconstructionVisited);

            return (distances[end], paths);
        }

        private void ReconstructPathsDFS(T current, T start, Dictionary<T, HashSet<T>> predecessors,
            List<T> currentPath, List<List<T>> paths)
        {
            ReconstructPathsDFS(current, start, predecessors, currentPath, paths, new HashSet<T>());
        }

        private void ReconstructPathsDFS(T current, T start, Dictionary<T, HashSet<T>> predecessors,
            List<T> currentPath, List<List<T>> paths, HashSet<T> pathVisited)
        {
            if (pathVisited.Contains(current)) return;

            pathVisited.Add(current);
            currentPath.Add(current);

            if (current.Equals(start))
            {
                var fullPath = new List<T>(currentPath);
                fullPath.Reverse();
                paths.Add(fullPath);
            }
            else
            {
                if (predecessors.ContainsKey(current))
                {
                    foreach (var pred in predecessors[current])
                    {
                        ReconstructPathsDFS(pred, start, predecessors, currentPath, paths, pathVisited);
                    }
                }
            }

            currentPath.RemoveAt(currentPath.Count - 1);
            pathVisited.Remove(current);
        }

        public List<(double distance, List<T> path)> GetKShortestPathsBellmanFord(T start, T end, int k, Action<string>? log = null)
        {
            if (!_adjacencyList.ContainsKey(start)) throw new ArgumentException($"Вершина {start} не найдена.");
            if (!_adjacencyList.ContainsKey(end)) throw new ArgumentException($"Вершина {end} не найдена.");
            if (k <= 0) throw new ArgumentException("K должно быть больше 0.");

            log?.Invoke($"\n--- Поиск {k} простых кратчайших путей из {start} в {end} (модифицированный Беллман-Форд) ---");

            var dist = new Dictionary<T, List<(double cost, T pred, int predIdx, HashSet<T> visitedNodes)>>();

            foreach (var v in _adjacencyList.Keys)
            {
                dist[v] = new List<(double, T, int, HashSet<T>)>();
            }

            var startVisited = new HashSet<T> { start };
            dist[start].Add((0, default, -1, startVisited));

            int V = _adjacencyList.Count;
            int maxIterations = V + k + 5; 

            var allEdges = new List<(T u, T v, double w)>();
            foreach (var u in _adjacencyList.Keys)
            {
                foreach (var kvp in _adjacencyList[u])
                {
                    allEdges.Add((u, kvp.Key, kvp.Value));
                }
            }

            for (int i = 0; i < maxIterations; i++)
            {
                bool changed = false;
                log?.Invoke($"\nИтерация {i + 1}:");

                foreach (var edge in allEdges)
                {
                    T u = edge.u;
                    T v = edge.v;
                    double w = edge.w;

                    if (dist[u].Count == 0) continue;

                    for (int idx = 0; idx < dist[u].Count; idx++)
                    {
                        var currentState = dist[u][idx];
                        
                        if (currentState.visitedNodes.Contains(v)) continue;

                        double newCost = currentState.cost + w;
                        var vList = dist[v];

                        if (vList.Count >= k && newCost >= vList.Last().cost - 1e-9)
                        {
                            if (newCost > vList.Last().cost + 1e-9) continue;
                        }

                        bool exists = false;
                        foreach (var item in vList)
                        {
                            if (Math.Abs(item.cost - newCost) < 1e-9 &&
                                EqualityComparer<T>.Default.Equals(item.pred, u) &&
                                item.predIdx == idx)
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (exists) continue;

                        var newVisited = new HashSet<T>(currentState.visitedNodes);
                        newVisited.Add(v);

                        vList.Add((newCost, u, idx, newVisited));
                        vList.Sort((a, b) => a.cost.CompareTo(b.cost));

                        if (vList.Count > k)
                        {
                            vList.RemoveRange(k, vList.Count - k);
                        }

                        changed = true;
                        log?.Invoke($"  Обновление: найден путь до {v} со стоимостью {newCost} (через {u})");
                    }
                }

                if (!changed)
                {
                    log?.Invoke("  Изменений нет, ранняя остановка.");
                    break;
                }
            }

            log?.Invoke("\n--- Восстановление путей ---");
            var result = new List<(double distance, List<T> path)>();

            foreach (var finalState in dist[end])
            {
                var path = new List<T>();
                T curr = end;
                T pred = finalState.pred;
                int predIdx = finalState.predIdx;

                path.Add(curr);

                int limit = maxIterations * 2;
                while (limit-- > 0)
                {
                    if (EqualityComparer<T>.Default.Equals(curr, start) && predIdx == -1)
                    {
                        break;
                    }

                    if (predIdx == -1) break;

                    curr = pred;
                    path.Add(curr);

                    if (dist.ContainsKey(curr) && predIdx >= 0 && predIdx < dist[curr].Count)
                    {
                        var prevState = dist[curr][predIdx];
                        pred = prevState.pred;
                        predIdx = prevState.predIdx;
                    }
                    else
                    {
                        break;
                    }
                }

                path.Reverse();
                if (path.Count > 0 && path[0].Equals(start) && path.Last().Equals(end))
                {
                    result.Add((finalState.cost, path));
                    log?.Invoke($"Восстановлен путь: {string.Join(" -> ", path)} со стоимостью {finalState.cost}");
                }
            }

            return result;
        }

        public List<List<T>> GetNegativeCyclesFloydWarshall(Action<string>? log = null)
        {
            var vertices = _adjacencyList.Keys.ToList();
            int n = vertices.Count;
            var vertexToIndex = new Dictionary<T, int>();
            for (int i = 0; i < n; i++)
            {
                vertexToIndex[vertices[i]] = i;
            }

            double[,] dist = new double[n, n];
            int?[,] parent = new int?[n, n];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    dist[i, j] = double.PositiveInfinity;
                    parent[i, j] = null;
                }

                dist[i, i] = 0;
            }

            foreach (var u in _adjacencyList)
            {
                int uIdx = vertexToIndex[u.Key];
                foreach (var v in u.Value)
                {
                    int vIdx = vertexToIndex[v.Key];
                    dist[uIdx, vIdx] = v.Value;
                    parent[uIdx, vIdx] = uIdx;
                }
            }

            for (int k = 0; k < n; k++)
            {
                log?.Invoke($"Floyd-Warshall: промежуточная вершина {vertices[k]} ({k + 1}/{n})");
                for (int i = 0; i < n; i++)
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (!double.IsPositiveInfinity(dist[i, k]) && !double.IsPositiveInfinity(dist[k, j]))
                        {
                            if (dist[i, k] + dist[k, j] < dist[i, j])
                            {
                                dist[i, j] = dist[i, k] + dist[k, j];
                                parent[i, j] = parent[k, j];
                                log?.Invoke($"Обновление: {vertices[i]} -> {vertices[j]} = {dist[i, j]} через {vertices[k]}");
                            }
                        }
                    }
                }
            }

            var cycles = new List<List<T>>();
            var seenCycles = new HashSet<string>();

            for (int i = 0; i < n; i++)
            {
                if (dist[i, i] < 0)
                {
                    int curr = i;
                    for (int step = 0; step < n; step++)
                    {
                        if (parent[i, curr] == null) break;
                        curr = parent[i, curr].Value;
                    }

                    var cycle = new List<T>();
                    int startNode = curr;
                    bool cycleFound = false;

                    cycle.Add(vertices[startNode]);

                    int temp = startNode;
                    int limit = n + 1;
                    while (limit-- > 0)
                    {
                        if (parent[i, temp] == null) break;
                        temp = parent[i, temp].Value;

                        if (temp == startNode)
                        {
                            cycleFound = true;
                            break;
                        }

                        cycle.Add(vertices[temp]);
                    }

                    if (cycleFound)
                    {
                        cycle.Add(vertices[startNode]);
                        cycle.Reverse();

                        var minVertex = cycle.Min();
                        int minIdx = cycle.IndexOf(minVertex);
                        var normalizedCycle = new List<T>();

                        for (int k = 0; k < cycle.Count - 1; k++)
                        {
                            normalizedCycle.Add(cycle[(minIdx + k) % (cycle.Count - 1)]);
                        }

                        normalizedCycle.Add(normalizedCycle[0]);

                        string hash = string.Join("->", normalizedCycle);
                        if (seenCycles.Add(hash))
                        {
                            cycles.Add(normalizedCycle);
                        }
                    }
                }
            }

            return cycles;
        }

        public double GetMaxFlowEdmondsKarp(T source, T sink, Action<string>? log = null)
        {
            if (!_adjacencyList.ContainsKey(source)) throw new ArgumentException($"Вершина {source} не найдена.");
            if (!_adjacencyList.ContainsKey(sink)) throw new ArgumentException($"Вершина {sink} не найдена.");
            if (source.Equals(sink)) return 0;

            var residualCapacity = new Dictionary<T, Dictionary<T, double>>();
            foreach (var u in _adjacencyList.Keys)
            {
                residualCapacity[u] = new Dictionary<T, double>();
            }

            foreach (var u in _adjacencyList)
            {
                foreach (var v in u.Value)
                {
                    T from = u.Key;
                    T to = v.Key;
                    double capacity = v.Value;

                    if (capacity < 0)
                        throw new InvalidOperationException("Алгоритм Эдмондса-Карпа не поддерживает отрицательные пропускные способности.");

                    if (!residualCapacity[from].ContainsKey(to))
                        residualCapacity[from][to] = 0;
                    residualCapacity[from][to] += capacity;
                    
                    if (!residualCapacity.ContainsKey(to))
                    {
                        residualCapacity[to] = new Dictionary<T, double>();
                    }
                    if (!residualCapacity[to].ContainsKey(from))
                    {
                        residualCapacity[to][from] = 0;
                    }
                }
            }

            double maxFlow = 0;

            while (true)
            {
                var parent = new Dictionary<T, T>();
                var queue = new Queue<T>();
                
                queue.Enqueue(source);
                parent[source] = source; 

                bool reachedSink = false;

                while (queue.Count > 0 && !reachedSink)
                {
                    var u = queue.Dequeue();

                    if (!residualCapacity.ContainsKey(u)) continue;

                    foreach (var kvp in residualCapacity[u])
                    {
                        var v = kvp.Key;
                        var capacity = kvp.Value;

                        if (!parent.ContainsKey(v) && capacity > 0)
                        {
                            parent[v] = u;
                            queue.Enqueue(v);

                            if (v.Equals(sink))
                            {
                                reachedSink = true;
                                break;
                            }
                        }
                    }
                }

                if (!reachedSink)
                    break;

                double pathFlow = double.PositiveInfinity;
                T curr = sink;
                while (!curr.Equals(source))
                {
                    T prev = parent[curr];
                    pathFlow = Math.Min(pathFlow, residualCapacity[prev][curr]);
                    curr = prev;
                }

                curr = sink;
                var pathNodes = new List<T>();
                pathNodes.Add(curr);

                while (!curr.Equals(source))
                {
                    T prev = parent[curr];
                    residualCapacity[prev][curr] -= pathFlow;
                    residualCapacity[curr][prev] += pathFlow;
                    curr = prev;
                    pathNodes.Add(curr);
                }

                pathNodes.Reverse();
                log?.Invoke($"Найдён увеличивающий путь: {string.Join(" -> ", pathNodes)} с потоком {pathFlow}");

                maxFlow += pathFlow;
            }

            return maxFlow;
        }
    }
}
