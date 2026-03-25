namespace Graph
{
    public class ConsoleInterface
    {
        private Graph<string> _graph;

        public ConsoleInterface()
        {
            _graph = new Graph<string>(isDirected: false, isWeighted: false);
        }

        public void Run()
        {
            while (true)
            {
                Console.WriteLine("\n--- Консольный Интерфейс Графа ---");
                Console.WriteLine("1. Создать новый граф (пустой)");
                Console.WriteLine("2. Загрузить граф из файла");
                Console.WriteLine("3. Добавить вершину");
                Console.WriteLine("4. Удалить вершину");
                Console.WriteLine("5. Добавить ребро (дугу)");
                Console.WriteLine("6. Удалить ребро (дугу)");
                Console.WriteLine("7. Показать список смежности");
                Console.WriteLine("8. Вывести не смежные вершины");
                Console.WriteLine("9. Вывести изолированные вершины");
                Console.WriteLine("10. Удалить ребра в висячие вершины");
                Console.WriteLine("11. Найти фундаментальные циклы (DFS)");
                Console.WriteLine("12. Найти фундаментальные циклы (BFS)");
                Console.WriteLine("13. Найти вершину с равными путями (BFS)");
                Console.WriteLine("14. Найти минимальное остовное дерево (Boruvka)");
                Console.WriteLine("15. Найти кратчайший путь (Dijkstra)");
                Console.WriteLine("16. Найти K кратчайших путей (Bellman-Ford)");
                Console.WriteLine("17. Найти циклы отрицательного веса (Floyd-Warshall)");
                Console.WriteLine("18. Сохранить в файл");
                Console.WriteLine("19. Выход");
                Console.Write("Выберите опцию: ");

                string choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            CreateNewGraph();
                            break;
                        case "2":
                            LoadGraph();
                            break;
                        case "3":
                            AddVertex();
                            break;
                        case "4":
                            RemoveVertex();
                            break;
                        case "5":
                            AddEdge();
                            break;
                        case "6":
                            RemoveEdge();
                            break;
                        case "7":
                            Console.WriteLine(_graph.ToString());
                            break;
                        case "8":
                            ShowNonAdjacent();
                            break;
                        case "9":
                            ShowIsolated();
                            break;
                        case "10":
                            RemoveEdgesToPendant();
                            break;
                        case "11":
                            ShowCyclesDFS();
                            break;
                        case "12":
                            ShowCyclesBFS();
                            break;
                        case "13":
                            FindEquidistant();
                            break;
                        case "14":
                            ShowMSTBoruvka();
                            break;
                        case "15":
                            FindShortestPathDijkstra();
                            break;
                        case "16":
                            FindKShortestPathsBellmanFord();
                            break;
                        case "17":
                            FindNegativeCyclesFloyd();
                            break;
                        case "18":
                            SaveGraph();
                            break;
                        case "19":
                            return;
                        default:
                            Console.WriteLine("Неверная опция.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                }
            }
        }

        private void CreateNewGraph()
        {
            Console.Write("Ориентированный? (y/n): ");
            bool directed = Console.ReadLine()?.ToLower() == "y";
            Console.Write("Взвешенный? (y/n): ");
            bool weighted = Console.ReadLine()?.ToLower() == "y";
            _graph = new Graph<string>(directed, weighted);
            Console.WriteLine("Новый пустой граф создан.");
        }

        private void LoadGraph()
        {
            Console.Write("Введите путь к файлу: ");
            string path = Console.ReadLine();
            _graph = new Graph<string>(path, s => s); 
            Console.WriteLine("Граф успешно загружен.");
        }

        private void ShowNonAdjacent()
        {
            Console.Write("Введите имя вершины: ");
            string v = Console.ReadLine();
            
            if (!_graph.ContainsVertex(v))
            {
                Console.WriteLine($"Вершина '{v}' не найдена.");
                return;
            }

            var nonAdjacent = _graph.GetNonAdjacentVertices(v);
            if (nonAdjacent.Count == 0)
            {
                Console.WriteLine($"У вершины '{v}' нет не смежных вершин (она смежна со всеми другими).");
            }
            else
            {
                Console.WriteLine($"Вершины, не смежные с '{v}': {string.Join(", ", nonAdjacent)}");
            }
        }

        private void ShowIsolated()
        {
            var isolated = _graph.GetIsolatedVertices();
            if (isolated.Count == 0)
            {
                Console.WriteLine("В графе нет изолированных вершин.");
            }
            else
            {
                Console.WriteLine($"Изолированные вершины: {string.Join(", ", isolated)}");
            }
        }

        private void RemoveEdgesToPendant()
        {
            var newGraph = _graph.RemoveEdgesToPendantVertices();
            _graph = newGraph;
            Console.WriteLine("Ребра, ведущие в висячие вершины, удалены. Граф обновлен.");
        }

        private void ShowCyclesDFS()
        {
            var cycles = _graph.GetFundamentalCyclesDFS();
            Console.WriteLine($"Найдено циклов (DFS): {cycles.Count}");
            foreach (var cycle in cycles)
            {
                Console.WriteLine(string.Join(" -> ", cycle));
            }
        }

        private void ShowCyclesBFS()
        {
            var cycles = _graph.GetFundamentalCyclesBFS();
            Console.WriteLine($"Найдено циклов (BFS): {cycles.Count}");
            foreach (var cycle in cycles)
            {
                Console.WriteLine(string.Join(" -> ", cycle));
            }
        }

        private void FindEquidistant()
        {
            Console.Write("Введите вершину U: ");
            string u = Console.ReadLine();
            Console.Write("Введите вершину V: ");
            string v = Console.ReadLine();

            try
            {
                var bfsResult = _graph.FindEquidistantVertexBFS(u, v);
                if (!string.IsNullOrEmpty(bfsResult))
                {
                    Console.WriteLine($"[BFS] Найдена вершина: {bfsResult}");
                }
                else
                {
                    Console.WriteLine("[BFS] Вершина с равными путями не найдена (среди кратчайших).");
                }

                var dfsResult = _graph.FindEquidistantVertexDFS(u, v);
                if (!string.IsNullOrEmpty(dfsResult))
                {
                    Console.WriteLine($"[DFS] Найдена вершина: {dfsResult}");
                }
                else
                {
                    Console.WriteLine("[DFS] Вершина с равными путями не найдена (среди путей DFS-дерева).");
                }
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private void ShowMSTBoruvka()
        {
            try
            {
                var mst = _graph.GetMinimumSpanningTreeBoruvka();
                Console.WriteLine("Минимальное остовное дерево (Алгоритм Борувки):");
                
                var edges = mst.GetEdgeList();
                double totalWeight = 0;
                
                foreach (var edge in edges)
                {
                    Console.WriteLine(edge.ToString());
                    totalWeight += edge.Weight;
                }
                
                Console.WriteLine($"Общий вес: {totalWeight}");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private void FindShortestPathDijkstra()
        {
            Console.Write("Введите начальную вершину: ");
            string start = Console.ReadLine();
            Console.Write("Введите конечную вершину: ");
            string end = Console.ReadLine();

            try
            {
                var (distance, paths) = _graph.GetShortestPathsDijkstra(start, end);

                if (double.IsPositiveInfinity(distance))
                {
                    Console.WriteLine($"Путь из {start} в {end} не существует.");
                }
                else
                {
                    Console.WriteLine($"Кратчайшее расстояние: {distance}");
                    Console.WriteLine($"Найдено путей: {paths.Count}");
                    foreach (var path in paths)
                    {
                        Console.WriteLine(string.Join(" -> ", path));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private void FindKShortestPathsBellmanFord()
        {
            Console.Write("Введите начальную вершину: ");
            string start = Console.ReadLine();
            Console.Write("Введите конечную вершину: ");
            string end = Console.ReadLine();
            Console.Write("Введите количество путей (K): ");
            if (!int.TryParse(Console.ReadLine(), out int k))
            {
                Console.WriteLine("Некорректное число.");
                return;
            }

            try
            {
                var paths = _graph.GetKShortestPathsBellmanFord(start, end, k);

                if (paths.Count == 0)
                {
                    Console.WriteLine($"Пути из {start} в {end} не найдены.");
                }
                else
                {
                    Console.WriteLine($"Найдено {paths.Count} кратчайших путей (Bellman-Ford):");
                    for (int i = 0; i < paths.Count; i++)
                    {
                        Console.WriteLine($"#{i + 1} (Длина: {paths[i].distance}): {string.Join(" -> ", paths[i].path)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private void SaveGraph()
        {
            Console.Write("Введите путь к файлу: ");
            string path = Console.ReadLine();

            Console.WriteLine("Выберите формат сохранения:");
            Console.WriteLine("1. Список граней (Edge List)");
            Console.WriteLine("2. Список смежности (Adjacency List)");
            Console.Write("Ваш выбор: ");
            string choice = Console.ReadLine();

            GraphSaveFormat format = GraphSaveFormat.EdgeList;
            if (choice == "2")
            {
                format = GraphSaveFormat.AdjacencyList;
            }

            _graph.SaveToFile(path, format);
            Console.WriteLine("Граф успешно сохранен.");
        }

        private void FindNegativeCyclesFloyd()
        {
            try
            {
                var cycles = _graph.GetNegativeCyclesFloydWarshall();
                
                if (cycles.Count == 0)
                {
                    Console.WriteLine("Циклов отрицательного веса не найдено.");
                }
                else
                {
                    Console.WriteLine($"Найдено циклов отрицательного веса: {cycles.Count}");
                    foreach (var cycle in cycles)
                    {
                        Console.WriteLine(string.Join(" -> ", cycle));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }

        private void AddVertex()
        {
            Console.Write("Введите имя вершины: ");
            string v = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(v)) return;

            if (_graph.AddVertex(v))
                Console.WriteLine($"Вершина '{v}' добавлена.");
            else
                Console.WriteLine($"Вершина '{v}' уже существует.");
        }

        private void RemoveVertex()
        {
            Console.Write("Введите имя вершины: ");
            string v = Console.ReadLine();
            if (_graph.RemoveVertex(v))
                Console.WriteLine($"Вершина '{v}' удалена.");
            else
                Console.WriteLine($"Вершина '{v}' не найдена.");
        }

        private void AddEdge()
        {
            Console.Write("Введите исходную вершину: ");
            string src = Console.ReadLine();
            Console.Write("Введите конечную вершину: ");
            string dest = Console.ReadLine();

            double weight = 1.0;
            if (_graph.IsWeighted)
            {
                Console.Write("Введите вес: ");
                if (!double.TryParse(Console.ReadLine(), out weight))
                {
                    Console.WriteLine("Некорректный вес. Используется 1.0.");
                    weight = 1.0;
                }
            }

            _graph.AddEdge(src, dest, weight);
            Console.WriteLine("Ребро добавлено.");
        }

        private void RemoveEdge()
        {
            Console.Write("Введите исходную вершину: ");
            string src = Console.ReadLine();
            Console.Write("Введите конечную вершину: ");
            string dest = Console.ReadLine();

            if (_graph.RemoveEdge(src, dest))
                Console.WriteLine("Ребро удалено.");
            else
                Console.WriteLine("Ребро не найдено.");
        }
    }
}
