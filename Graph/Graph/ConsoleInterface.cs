using System;

namespace Graph
{
    public class ConsoleInterface
    {
        private Graph<string> _graph;

        public ConsoleInterface()
        {
            // Начинаем с пустого графа по умолчанию
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
                Console.WriteLine("8. Сохранить в файл");
                Console.WriteLine("9. Выход");
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
                            SaveGraph();
                            break;
                        case "9":
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
            // Используем парсер "как есть" для строк
            _graph = new Graph<string>(path, s => s); 
            Console.WriteLine("Граф успешно загружен.");
        }

        private void SaveGraph()
        {
            Console.Write("Введите путь к файлу: ");
            string path = Console.ReadLine();
            _graph.SaveToFile(path);
            Console.WriteLine("Граф успешно сохранен.");
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
