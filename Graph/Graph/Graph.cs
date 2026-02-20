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

        public Graph(string filePath, Func<string, T> parser)
        {
            if (!File.Exists(filePath)) throw new FileNotFoundException("File not found", filePath);

            var lines = File.ReadAllLines(filePath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (lines.Length < 2) throw new ArgumentException("Invalid file format: too few lines.");

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
                    Console.WriteLine($"Warning processing line {i + 1}: {ex.Message}");
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
            if (!_adjacencyList.ContainsKey(from)) throw new ArgumentException($"Vertex {from} does not exist.");
            if (!_adjacencyList.ContainsKey(to)) throw new ArgumentException($"Vertex {to} does not exist.");

            if (!IsWeighted) weight = 1.0;

            if (_adjacencyList[from].ContainsKey(to))
                throw new InvalidOperationException($"Edge from {from} to {to} already exists.");

            _adjacencyList[from][to] = weight;

            if (!IsDirected)
            {
                if (!from.Equals(to))
                {
                    if (_adjacencyList[to].ContainsKey(from))
                        throw new InvalidOperationException(
                            $"Edge from {to} to {from} already exists (Consistency error).");

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
            sb.AppendLine($"Graph (Directed: {IsDirected}, Weighted: {IsWeighted})");
            sb.AppendLine($"Vertices: {_adjacencyList.Count}");
            foreach (var kvp in _adjacencyList)
            {
                sb.Append($"{kvp.Key}: ");
                if (kvp.Value.Count == 0)
                {
                    sb.AppendLine("(isolated)");
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
    }
}