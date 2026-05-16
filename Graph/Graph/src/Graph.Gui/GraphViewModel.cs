using System.ComponentModel;
using System.Runtime.CompilerServices;
using Graph;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace Graph.Gui;

public sealed class GraphViewModel : INotifyPropertyChanged
{
    private readonly IUiDialogService _dialogs;
    private readonly GraphRenderState _renderState;
    private readonly Action _invalidate;
    private Graph<string> _graph;
    private readonly Dictionary<string, PointF> _positions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _visitedVertices = new(StringComparer.Ordinal);
    private readonly HashSet<GraphEdgeKey> _committedEdges = new();
    private readonly Dictionary<string, string> _vertexAnnotations = new(StringComparer.Ordinal);
    private bool _isStepMode;
    private bool _hasAlgorithmState;
    private List<GraphStep> _stepSessionSteps = new();
    private int _stepIndex = -1;
    private float _viewportWidth;
    private float _viewportHeight;
    private string? _dragVertex;
    private bool _isPanning;
    private PointF _lastPointer;
    private PointF _downPointer;
    private long _downTimestamp;
    private bool _pointerMoved;
    private string? _edgeFromVertex;
    private const float VertexHitRadius = 26f;

    public GraphViewModel(IUiDialogService dialogs, Action invalidate)
    {
        _dialogs = dialogs;
        _invalidate = invalidate;
        _graph = new Graph<string>(isDirected: false, isWeighted: false);

        _renderState = new GraphRenderState();
        Drawable = new GraphDrawable(_renderState);

        NewGraphCommand = new Command(async () => await NewGraph());
        LoadGraphCommand = new Command(async () => await LoadGraph());
        SaveGraphCommand = new Command(async () => await SaveGraph());
        ShowAdjacencyCommand = new Command(async () => await ShowAdjacency());
        ConvertGraphCommand = new Command(async () => await ConvertGraph());

        AddVertexCommand = new Command(async () => await AddVertex());
        RemoveVertexCommand = new Command(async () => await RemoveVertex());
        AddEdgeCommand = new Command(async () => await AddEdge());
        RemoveEdgeCommand = new Command(async () => await RemoveEdge());

        ShowNonAdjacentCommand = new Command(async () => await ShowNonAdjacent());
        ShowIsolatedCommand = new Command(async () => await ShowIsolated());
        RemovePendantEdgesCommand = new Command(async () => await RemovePendantEdges());

        CyclesDfsCommand = new Command(async () => await ShowCyclesDfs());
        CyclesBfsCommand = new Command(async () => await ShowCyclesBfs());
        FindEquidistantCommand = new Command(async () => await FindEquidistant());
        MstBoruvkaCommand = new Command(async () => await ShowMstBoruvka());
        DijkstraCommand = new Command(async () => await FindShortestPathDijkstra());
        KShortestPathsCommand = new Command(async () => await FindKShortestPathsBellmanFord());
        NegativeCyclesCommand = new Command(async () => await FindNegativeCyclesFloyd());
        MaxFlowCommand = new Command(async () => await FindMaxFlowEdmondsKarp());

        PrevStepCommand = new Command(PrevStep, () => CanPrevStep);
        NextStepCommand = new Command(NextStep, () => CanNextStep);
        ExitAlgorithmCommand = new Command(ExitAlgorithm, () => HasAlgorithmState);

        TitleText = "Интерактивный граф";
        UpdateRender();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public GraphDrawable Drawable { get; }

    public Command NewGraphCommand { get; }
    public Command LoadGraphCommand { get; }
    public Command SaveGraphCommand { get; }
    public Command ShowAdjacencyCommand { get; }
    public Command ConvertGraphCommand { get; }

    public Command AddVertexCommand { get; }
    public Command RemoveVertexCommand { get; }
    public Command AddEdgeCommand { get; }
    public Command RemoveEdgeCommand { get; }

    public Command ShowNonAdjacentCommand { get; }
    public Command ShowIsolatedCommand { get; }
    public Command RemovePendantEdgesCommand { get; }

    public Command CyclesDfsCommand { get; }
    public Command CyclesBfsCommand { get; }
    public Command FindEquidistantCommand { get; }
    public Command MstBoruvkaCommand { get; }
    public Command DijkstraCommand { get; }
    public Command KShortestPathsCommand { get; }
    public Command NegativeCyclesCommand { get; }
    public Command MaxFlowCommand { get; }

    public Command PrevStepCommand { get; }
    public Command NextStepCommand { get; }
    public Command ExitAlgorithmCommand { get; }

    private string _titleText = "";
    public string TitleText
    {
        get => _titleText;
        private set => SetField(ref _titleText, value);
    }

    private string _graphSummary = "";
    public string GraphSummary
    {
        get => _graphSummary;
        private set => SetField(ref _graphSummary, value);
    }

    private string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public bool IsStepMode
    {
        get => _isStepMode;
        set => SetField(ref _isStepMode, value);
    }

    public bool HasStepSession => _stepSessionSteps.Count > 0;

    public string StepProgress => HasStepSession ? $"{Math.Max(_stepIndex + 1, 0)}/{_stepSessionSteps.Count}" : "";

    private bool CanPrevStep => HasStepSession && _stepIndex >= 0;
    private bool CanNextStep => HasStepSession && _stepIndex < _stepSessionSteps.Count - 1;

    public bool HasAlgorithmState
    {
        get => _hasAlgorithmState;
        private set
        {
            if (!SetField(ref _hasAlgorithmState, value)) return;
            ExitAlgorithmCommand.ChangeCanExecute();
        }
    }

    public void SetViewport(float width, float height)
    {
        if (width <= 0 || height <= 0) return;
        if (Math.Abs(_viewportWidth - width) < 0.5f && Math.Abs(_viewportHeight - height) < 0.5f) return;

        _viewportWidth = width;
        _viewportHeight = height;
        UpdateLayout();
        _renderState.Positions = new Dictionary<string, PointF>(_positions);
        _invalidate();
    }

    public void PointerDown(PointF point)
    {
        _downPointer = point;
        _lastPointer = point;
        _downTimestamp = Environment.TickCount64;
        _pointerMoved = false;

        var hit = HitTestVertex(point);
        _dragVertex = hit;
        _isPanning = hit == null;

        if (hit == null)
        {
            _renderState.SelectedVertex = _edgeFromVertex;
        }
        else
        {
            _renderState.SelectedVertex = hit;
        }

        _invalidate();
    }

    public void PointerMove(PointF point)
    {
        var dx = point.X - _lastPointer.X;
        var dy = point.Y - _lastPointer.Y;

        if (Math.Abs(point.X - _downPointer.X) + Math.Abs(point.Y - _downPointer.Y) > 4)
        {
            _pointerMoved = true;
        }

        if (_dragVertex != null)
        {
            _positions[_dragVertex] = point;
            _renderState.Positions = new Dictionary<string, PointF>(_positions);
            _invalidate();
        }
        else if (_isPanning)
        {
            if (Math.Abs(dx) > 0.01f || Math.Abs(dy) > 0.01f)
            {
                foreach (var key in _positions.Keys.ToArray())
                {
                    var p = _positions[key];
                    _positions[key] = new PointF(p.X + dx, p.Y + dy);
                }

                _renderState.Positions = new Dictionary<string, PointF>(_positions);
                _invalidate();
            }
        }

        _lastPointer = point;
    }

    public async Task PointerUp(PointF point)
    {
        var duration = Environment.TickCount64 - _downTimestamp;
        var isTap = !_pointerMoved && duration < 350;

        _dragVertex = null;
        _isPanning = false;

        if (!isTap) return;

        var hit = HitTestVertex(point);
        if (hit != null)
        {
            await HandleVertexTap(hit);
        }
        else
        {
            await HandleEmptyTap(point);
        }
    }

    private async Task NewGraph()
    {
        var directedChoice = await _dialogs.Choose("Ориентированный граф?", "Отмена", "Да", "Нет");
        if (directedChoice == null) return;

        var weightedChoice = await _dialogs.Choose("Взвешенный граф?", "Отмена", "Да", "Нет");
        if (weightedChoice == null) return;

        bool directed = directedChoice == "Да";
        bool weighted = weightedChoice == "Да";

        ClearStepSession();
        ResetAlgorithmVisualState();
        _graph = new Graph<string>(directed, weighted);
        AppendLog("Новый пустой граф создан.");
        UpdateRender();
    }

    private async Task LoadGraph()
    {
        ClearStepSession();
        ResetAlgorithmVisualState();

        try
        {
            AppendLog("Открываю выбор файла...");
            await Task.Yield();

            var textFileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.iOS] = new[] { "public.plain-text", "public.text" },
                [DevicePlatform.MacCatalyst] = new[] { "public.plain-text", "public.text" },
                [DevicePlatform.WinUI] = new[] { ".txt" },
                [DevicePlatform.Android] = new[] { "text/plain" }
            });

            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Выберите файл графа (.txt)",
                FileTypes = textFileTypes
            });
            if (file == null)
            {
                await _dialogs.ShowMessage("Загрузка", "Файл не выбран.");
                return;
            }

            AppendLog($"Файл выбран: {file.FileName}");
            string path = file.FullPath ?? "";
            if (string.IsNullOrWhiteSpace(path))
            {
                using var stream = await file.OpenReadAsync();
                path = Path.Combine(FileSystem.CacheDirectory, file.FileName);
                using var outStream = File.Create(path);
                await stream.CopyToAsync(outStream);
            }

            _graph = new Graph<string>(path, s => s, AppendLog);
            AppendLog($"Граф успешно загружен. Вершины: {_graph.Vertices.Count}, рёбра: {_graph.GetEdgeList().Count}");
            UpdateRender();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }


    private async Task SaveGraph()
    {
        var name = await _dialogs.Prompt("Сохранение", "Имя файла (будет сохранён в Documents):", "graph.txt", "graph.txt");
        if (string.IsNullOrWhiteSpace(name)) return;

        var formatChoice = await _dialogs.Choose("Формат", "Отмена", "Список граней (Edge List)", "Список смежности (Adjacency List)");
        if (formatChoice == null) return;

        var folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var path = Path.Combine(folder, name);

        var format = formatChoice.StartsWith("Список смежности", StringComparison.OrdinalIgnoreCase)
            ? GraphSaveFormat.AdjacencyList
            : GraphSaveFormat.EdgeList;

        try
        {
            _graph.SaveToFile(path, format);
            AppendLog($"Сохранено: {path}");
            await _dialogs.ShowMessage("Готово", $"Файл сохранён:\n{path}");
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private Task ShowAdjacency()
    {
        return _dialogs.ShowMessage("Список смежности", _graph.ToString());
    }

    private async Task ConvertGraph()
    {
        var directedChoice = await _dialogs.Choose("Преобразование", "Отмена", "Ориентированный", "Неориентированный");
        if (directedChoice == null) return;

        var weightedChoice = await _dialogs.Choose("Преобразование", "Отмена", "Взвешенный", "Невзвешенный");
        if (weightedChoice == null) return;

        bool isDirected = directedChoice.StartsWith("Ориент", StringComparison.OrdinalIgnoreCase);
        bool isWeighted = weightedChoice.StartsWith("Взвеш", StringComparison.OrdinalIgnoreCase);

        double defaultWeight = 1.0;
        if (isWeighted && !_graph.IsWeighted)
        {
            var w = await _dialogs.Prompt("Преобразование", "Вес по умолчанию для всех рёбер:", "1", "1");
            if (string.IsNullOrWhiteSpace(w)) return;
            if (!double.TryParse(w.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out defaultWeight))
            {
                await _dialogs.ShowMessage("Ошибка", "Некорректный вес.");
                return;
            }
        }

        try
        {
            ExitAlgorithm();
            _graph = _graph.ConvertTo(isDirected, isWeighted, defaultWeight);
            AppendLog($"Граф преобразован: {(isDirected ? "DIRECTED" : "UNDIRECTED")} {(isWeighted ? "WEIGHTED" : "UNWEIGHTED")}");
            UpdateRender();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private async Task AddVertex()
    {
        var v = await _dialogs.Prompt("Добавить вершину", "Имя вершины:");
        if (string.IsNullOrWhiteSpace(v)) return;

        if (_graph.AddVertex(v))
        {
            AppendLog($"Вершина '{v}' добавлена.");
            UpdateRender();
        }
        else
        {
            await _dialogs.ShowMessage("Информация", $"Вершина '{v}' уже существует.");
        }
    }

    private async Task RemoveVertex()
    {
        var v = await _dialogs.Prompt("Удалить вершину", "Имя вершины:");
        if (string.IsNullOrWhiteSpace(v)) return;

        if (_graph.RemoveVertex(v))
        {
            AppendLog($"Вершина '{v}' удалена.");
            UpdateRender();
        }
        else
        {
            await _dialogs.ShowMessage("Информация", $"Вершина '{v}' не найдена.");
        }
    }

    private async Task AddEdge()
    {
        var from = await _dialogs.Prompt("Добавить ребро", "Исходная вершина:");
        if (string.IsNullOrWhiteSpace(from)) return;
        var to = await _dialogs.Prompt("Добавить ребро", "Конечная вершина:");
        if (string.IsNullOrWhiteSpace(to)) return;

        double weight = 1.0;
        if (_graph.IsWeighted)
        {
            var w = await _dialogs.Prompt("Добавить ребро", "Вес:", "1");
            if (!string.IsNullOrWhiteSpace(w) && double.TryParse(w, out var parsed)) weight = parsed;
        }

        try
        {
            _graph.AddEdge(from, to, weight);
            AppendLog($"Ребро добавлено: {from} -> {to}");
            UpdateRender();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private async Task RemoveEdge()
    {
        var from = await _dialogs.Prompt("Удалить ребро", "Исходная вершина:");
        if (string.IsNullOrWhiteSpace(from)) return;
        var to = await _dialogs.Prompt("Удалить ребро", "Конечная вершина:");
        if (string.IsNullOrWhiteSpace(to)) return;

        if (_graph.RemoveEdge(from, to))
        {
            AppendLog($"Ребро удалено: {from} -> {to}");
            UpdateRender();
        }
        else
        {
            await _dialogs.ShowMessage("Информация", "Ребро не найдено.");
        }
    }

    private async Task ShowNonAdjacent()
    {
        var v = await _dialogs.Prompt("Не смежные вершины", "Имя вершины:");
        if (string.IsNullOrWhiteSpace(v)) return;

        try
        {
            var list = _graph.GetNonAdjacentVertices(v);
            await _dialogs.ShowMessage("Результат", list.Count == 0 ? "Нет не смежных вершин." : string.Join(", ", list));
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private async Task ShowIsolated()
    {
        var list = _graph.GetIsolatedVertices();
        await _dialogs.ShowMessage("Изолированные вершины", list.Count == 0 ? "Нет изолированных вершин." : string.Join(", ", list));
    }

    private Task RemovePendantEdges()
    {
        _graph = _graph.RemoveEdgesToPendantVertices();
        AppendLog("Ребра, ведущие в висячие вершины, удалены. Граф обновлен.");
        UpdateRender();
        return Task.CompletedTask;
    }

    private async Task ShowCyclesDfs()
    {
        try
        {
            var cycles = await RunWithSteps(log => _graph.GetFundamentalCyclesDFS(log));
            if (cycles.Count == 0)
            {
                await _dialogs.ShowMessage("DFS", "Циклов не найдено.");
                return;
            }

            await _dialogs.ShowMessage("DFS", string.Join("\n", cycles.Select(c => string.Join(" -> ", c))));
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private async Task ShowCyclesBfs()
    {
        try
        {
            var cycles = await RunWithSteps(log => _graph.GetFundamentalCyclesBFS(log));
            if (cycles.Count == 0)
            {
                await _dialogs.ShowMessage("BFS", "Циклов не найдено.");
                return;
            }

            await _dialogs.ShowMessage("BFS", string.Join("\n", cycles.Select(c => string.Join(" -> ", c))));
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private async Task FindEquidistant()
    {
        var u = await _dialogs.Prompt("Равные пути", "Вершина U:");
        if (string.IsNullOrWhiteSpace(u)) return;
        var v = await _dialogs.Prompt("Равные пути", "Вершина V:");
        if (string.IsNullOrWhiteSpace(v)) return;

        try
        {
            var bfs = _graph.FindEquidistantVertexBFS(u, v);
            var dfs = _graph.FindEquidistantVertexDFS(u, v);

            var lines = new List<string>();
            lines.Add(!EqualityComparer<string>.Default.Equals(bfs, default) ? $"BFS: {bfs}" : "BFS: не найдено");
            lines.Add(!EqualityComparer<string>.Default.Equals(dfs, default) ? $"DFS: {dfs}" : "DFS: не найдено");
            await _dialogs.ShowMessage("Результат", string.Join("\n", lines));
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private async Task ShowMstBoruvka()
    {
        if (_graph.IsDirected)
        {
            await _dialogs.ShowMessage("Ошибка", "Алгоритм Борувка работает только для неориентированных графов.");
            return;
        }

        try
        {
            var mst = await RunWithSteps(log => _graph.GetMinimumSpanningTreeBoruvka(log));
            var edges = mst.GetEdgeList();
            var total = edges.Sum(e => e.Weight);
            await _dialogs.ShowMessage("MST (Boruvka)", $"{string.Join("\n", edges.Select(e => e.ToString()))}\n\nОбщий вес: {total:0.###}");

            var replace = await _dialogs.Confirm("Применить MST?", "Заменить текущий граф на минимальное остовное дерево?", "Да", "Нет");
            if (replace)
            {
                _graph = mst;
                UpdateRender();
            }
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private async Task FindShortestPathDijkstra()
    {
        var start = await _dialogs.Prompt("Dijkstra", "Начальная вершина:");
        if (string.IsNullOrWhiteSpace(start)) return;
        var end = await _dialogs.Prompt("Dijkstra", "Конечная вершина:");
        if (string.IsNullOrWhiteSpace(end)) return;

        if (!_graph.IsWeighted)
        {
            await _dialogs.ShowMessage("Dijkstra", "Алгоритм требует взвешенный граф.");
            return;
        }

        if (_graph.GetEdgeList().Any(e => e.Weight < 0))
        {
            await _dialogs.ShowMessage("Dijkstra", "Алгоритм Дейкстры неприменим: в графе есть ребра с отрицательным весом.");
            return;
        }

        try
        {
            var (distance, paths) = await RunWithSteps(log => _graph.GetShortestPathsDijkstra(start, end, log));
            if (double.IsPositiveInfinity(distance) || paths.Count == 0)
            {
                await _dialogs.ShowMessage("Dijkstra", "Путь не существует.");
                return;
            }

            var text = $"Дистанция: {distance:0.###}\n\n{string.Join("\n", paths.Select(p => string.Join(" -> ", p)))}";
            await _dialogs.ShowMessage("Dijkstra", text);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private async Task FindKShortestPathsBellmanFord()
    {
        var start = await _dialogs.Prompt("K кратчайших путей", "Начальная вершина:");
        if (string.IsNullOrWhiteSpace(start)) return;
        var end = await _dialogs.Prompt("K кратчайших путей", "Конечная вершина:");
        if (string.IsNullOrWhiteSpace(end)) return;
        var kText = await _dialogs.Prompt("K кратчайших путей", "K:", "3", "3");
        if (!int.TryParse(kText, out int k) || k <= 0) return;

        try
        {
            var paths = await RunWithSteps(log => _graph.GetKShortestPathsBellmanFord(start, end, k, log));
            if (paths.Count == 0)
            {
                await _dialogs.ShowMessage("Результат", "Пути не найдены.");
                return;
            }

            var text = string.Join("\n", paths.Select((p, i) => $"#{i + 1} (Длина: {p.distance:0.###}): {string.Join(" -> ", p.path)}"));
            await _dialogs.ShowMessage("Результат", text);
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private async Task FindNegativeCyclesFloyd()
    {
        try
        {
            var cycles = await RunWithSteps(log => _graph.GetNegativeCyclesFloydWarshall(log));
            if (cycles.Count == 0)
            {
                await _dialogs.ShowMessage("Floyd-Warshall", "Циклов отрицательного веса не найдено.");
                return;
            }

            await _dialogs.ShowMessage("Floyd-Warshall", string.Join("\n", cycles.Select(c => string.Join(" -> ", c))));
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private async Task FindMaxFlowEdmondsKarp()
    {
        var source = await _dialogs.Prompt("Edmonds-Karp", "Источник (Source):");
        if (string.IsNullOrWhiteSpace(source)) return;
        var sink = await _dialogs.Prompt("Edmonds-Karp", "Сток (Sink):");
        if (string.IsNullOrWhiteSpace(sink)) return;

        if (!_graph.IsDirected || !_graph.IsWeighted)
        {
            await _dialogs.ShowMessage("Edmonds-Karp", "Алгоритм требует ориентированный взвешенный граф (веса как пропускные способности).");
            return;
        }

        if (_graph.GetEdgeList().Any(e => e.Weight < 0))
        {
            await _dialogs.ShowMessage("Edmonds-Karp", "Алгоритм не поддерживает отрицательные пропускные способности (веса должны быть ≥ 0).");
            return;
        }

        try
        {
            var maxFlow = await RunWithSteps(log => _graph.GetMaxFlowEdmondsKarp(source, sink, log));
            await _dialogs.ShowMessage("Edmonds-Karp", $"Максимальный поток: {maxFlow:0.###}");
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private async Task<T> RunWithSteps<T>(Func<Action<string>, T> func)
    {
        ClearStepSession();
        ResetAlgorithmVisualState();
        StatusText = "Выполнение...";

        var steps = new List<GraphStep>();
        T result = await Task.Run(() =>
        {
            return func(text =>
            {
                lock (steps)
                {
                    steps.Add(GraphStepParsing.Parse(text, _graph.IsDirected));
                }
            });
        });

        if (IsStepMode)
        {
            StartStepSession(steps);
            StatusText = $"Шаги: {steps.Count}. Используйте ◀ ▶";
        }
        else
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    ApplyStep(step);
                    StatusText = step.Text;
                    _invalidate();
                });

                await Task.Delay(180);
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ClearHighlight();
                _invalidate();
            });

            StatusText = "Готово";
        }

        return result;
    }

    private void StartStepSession(List<GraphStep> steps)
    {
        _stepSessionSteps = steps;
        _stepIndex = -1;
        OnPropertyChanged(nameof(HasStepSession));
        OnPropertyChanged(nameof(StepProgress));
        PrevStepCommand.ChangeCanExecute();
        NextStepCommand.ChangeCanExecute();
        HasAlgorithmState = true;
        ClearHighlight();
    }

    private void ClearStepSession()
    {
        if (_stepSessionSteps.Count == 0 && _stepIndex == -1) return;
        _stepSessionSteps = new List<GraphStep>();
        _stepIndex = -1;
        OnPropertyChanged(nameof(HasStepSession));
        OnPropertyChanged(nameof(StepProgress));
        PrevStepCommand.ChangeCanExecute();
        NextStepCommand.ChangeCanExecute();
        UpdateHasAlgorithmState();
    }

    private void PrevStep()
    {
        if (!HasStepSession) return;

        int newIndex = _stepIndex - 1;
        ResetAlgorithmVisualState();

        if (newIndex >= 0)
        {
            for (int i = 0; i <= newIndex; i++)
            {
                ApplyStep(_stepSessionSteps[i]);
            }
        }
        else
        {
            ClearHighlight();
        }

        _stepIndex = newIndex;
        OnPropertyChanged(nameof(StepProgress));
        PrevStepCommand.ChangeCanExecute();
        NextStepCommand.ChangeCanExecute();
        _invalidate();
    }

    private void NextStep()
    {
        if (!HasStepSession) return;
        if (_stepIndex >= _stepSessionSteps.Count - 1) return;

        _stepIndex++;
        ApplyStep(_stepSessionSteps[_stepIndex]);
        StatusText = _stepSessionSteps[_stepIndex].Text;
        OnPropertyChanged(nameof(StepProgress));
        PrevStepCommand.ChangeCanExecute();
        NextStepCommand.ChangeCanExecute();
        _invalidate();
    }

    private void ApplyHighlight(GraphStep step)
    {
        _renderState.HighlightVertices = new HashSet<string>(step.HighlightVertices);
        _renderState.HighlightEdges = new HashSet<GraphEdgeKey>(step.HighlightEdges);
    }

    private void ApplyStep(GraphStep step)
    {
        ApplyHighlight(step);

        switch (step.Kind)
        {
            case GraphStepKind.BoruvkaAddEdge:
                if (step.A != null && step.B != null)
                {
                    _committedEdges.Add(GraphStepParsing.NormalizeEdge(step.A, step.B, _graph.IsDirected));
                }
                break;

            case GraphStepKind.DijkstraStart:
                if (step.A != null)
                {
                    _visitedVertices.Clear();
                    _vertexAnnotations.Clear();
                    foreach (var v in _graph.Vertices)
                    {
                        _vertexAnnotations[v] = "∞";
                    }
                    _vertexAnnotations[step.A] = "0";
                }
                break;

            case GraphStepKind.DijkstraExtract:
                if (step.A != null)
                {
                    _visitedVertices.Add(step.A);
                    if (step.Value.HasValue)
                    {
                        _vertexAnnotations[step.A] = step.Value.Value.ToString("0.###");
                    }
                }
                break;

            case GraphStepKind.DijkstraRelax:
                if (step.B != null && step.Value.HasValue)
                {
                    _vertexAnnotations[step.B] = step.Value.Value.ToString("0.###");
                }
                break;

            case GraphStepKind.BellmanUpdate:
                if (step.A != null && step.Value.HasValue)
                {
                    _vertexAnnotations[step.A] = step.Value.Value.ToString("0.###");
                }
                break;

            case GraphStepKind.TraversalVisit:
                if (step.A != null)
                {
                    _visitedVertices.Add(step.A);
                }
                break;
        }

        _renderState.VisitedVertices = new HashSet<string>(_visitedVertices);
        _renderState.CommittedEdges = new HashSet<GraphEdgeKey>(_committedEdges);
        _renderState.VertexAnnotations = new Dictionary<string, string>(_vertexAnnotations);
        UpdateHasAlgorithmState();
    }

    private void ResetAlgorithmVisualState()
    {
        _visitedVertices.Clear();
        _committedEdges.Clear();
        _vertexAnnotations.Clear();
        _renderState.VisitedVertices = new HashSet<string>();
        _renderState.CommittedEdges = new HashSet<GraphEdgeKey>();
        _renderState.VertexAnnotations = new Dictionary<string, string>();
        ClearHighlight();
        UpdateHasAlgorithmState();
    }

    private void UpdateHasAlgorithmState()
    {
        HasAlgorithmState =
            HasStepSession ||
            _visitedVertices.Count > 0 ||
            _committedEdges.Count > 0 ||
            _vertexAnnotations.Count > 0 ||
            _renderState.HighlightVertices.Count > 0 ||
            _renderState.HighlightEdges.Count > 0;
    }

    private void ExitAlgorithm()
    {
        ClearStepSession();
        ResetAlgorithmVisualState();
        StatusText = "";
        _invalidate();
    }

    private void ClearHighlight()
    {
        _renderState.HighlightVertices = new HashSet<string>();
        _renderState.HighlightEdges = new HashSet<GraphEdgeKey>();
        _invalidate();
    }

    private void AppendLog(string text)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusText = text;
            _invalidate();
        });
    }

    private void UpdateRender()
    {
        _renderState.IsDirected = _graph.IsDirected;
        _renderState.IsWeighted = _graph.IsWeighted;
        var vertices = _graph.Vertices.OrderBy(v => v).ToArray();
        _renderState.Vertices = vertices;
        _renderState.Edges = _graph.GetEdgeList().Select(e => (e.From, e.To, e.Weight)).ToArray();
        GraphSummary = $"Вершины: {_renderState.Vertices.Count}, рёбра: {_renderState.Edges.Count}, ориентированный: {(_graph.IsDirected ? "да" : "нет")}, взвешенный: {(_graph.IsWeighted ? "да" : "нет")}";

        SyncPositions(vertices);
        UpdateLayout();
        _renderState.Positions = new Dictionary<string, PointF>(_positions);
        _invalidate();
    }

    private void UpdateLayout()
    {
        if (_viewportWidth <= 0 || _viewportHeight <= 0)
        {
            return;
        }

        var vertices = _renderState.Vertices;
        if (vertices.Count == 0)
        {
            _positions.Clear();
            return;
        }

        if (_positions.Count == 0)
        {
            LayoutCircle(vertices);
            return;
        }

        var center = new PointF(_viewportWidth / 2f, _viewportHeight / 2f);
        foreach (var v in vertices)
        {
            if (_positions.ContainsKey(v)) continue;
            _positions[v] = SpawnPositionNear(center);
        }
    }

    private void SyncPositions(IReadOnlyList<string> vertices)
    {
        var set = vertices.ToHashSet(StringComparer.Ordinal);

        foreach (var key in _positions.Keys.ToArray())
        {
            if (!set.Contains(key))
            {
                _positions.Remove(key);
            }
        }

        if (_edgeFromVertex != null && !set.Contains(_edgeFromVertex))
        {
            _edgeFromVertex = null;
            _renderState.SelectedVertex = null;
        }
    }

    private void LayoutCircle(IReadOnlyList<string> vertices)
    {
        _positions.Clear();

        var center = new PointF(_viewportWidth / 2f, _viewportHeight / 2f);
        var r = Math.Max(80f, Math.Min(_viewportWidth, _viewportHeight) / 2f - 60f);

        for (int i = 0; i < vertices.Count; i++)
        {
            double angle = (2.0 * Math.PI * i) / Math.Max(1, vertices.Count);
            var x = center.X + (float)(Math.Cos(angle) * r);
            var y = center.Y + (float)(Math.Sin(angle) * r);
            _positions[vertices[i]] = new PointF(x, y);
        }
    }

    private PointF SpawnPositionNear(PointF center)
    {
        var n = _positions.Count;
        double angle = (n * 137.5) * (Math.PI / 180.0);
        float radius = 36f + (n % 10) * 10f;
        return new PointF(
            center.X + (float)(Math.Cos(angle) * radius),
            center.Y + (float)(Math.Sin(angle) * radius)
        );
    }

    private string? HitTestVertex(PointF point)
    {
        string? best = null;
        float bestDist = float.MaxValue;

        foreach (var kvp in _positions)
        {
            var dx = kvp.Value.X - point.X;
            var dy = kvp.Value.Y - point.Y;
            var dist = (float)Math.Sqrt(dx * dx + dy * dy);
            if (dist <= VertexHitRadius && dist < bestDist)
            {
                bestDist = dist;
                best = kvp.Key;
            }
        }

        return best;
    }

    private async Task HandleEmptyTap(PointF point)
    {
        if (HasAlgorithmState) return;

        var name = await _dialogs.Prompt("Добавить вершину", "Имя вершины:", "A");
        if (string.IsNullOrWhiteSpace(name)) return;

        if (!_graph.AddVertex(name))
        {
            await _dialogs.ShowMessage("Ошибка", $"Вершина '{name}' уже существует.");
            return;
        }

        _positions[name] = point;
        AppendLog($"Вершина '{name}' добавлена.");
        UpdateRender();
    }

    private async Task HandleVertexTap(string vertex)
    {
        if (HasAlgorithmState) return;

        if (_edgeFromVertex == null)
        {
            _edgeFromVertex = vertex;
            _renderState.SelectedVertex = vertex;
            StatusText = $"Выбрана вершина '{vertex}'. Выберите вторую вершину для ребра.";
            _invalidate();
            return;
        }

        if (_edgeFromVertex == vertex)
        {
            _edgeFromVertex = null;
            _renderState.SelectedVertex = null;
            StatusText = "";
            _invalidate();
            return;
        }

        var from = _edgeFromVertex;
        var to = vertex;

        if (_graph.IsDirected)
        {
            var choice = await _dialogs.Choose("Направление ребра", "Отмена", $"{from} -> {to}", $"{to} -> {from}");
            if (choice == null) return;
            if (choice.StartsWith(to, StringComparison.Ordinal))
            {
                (from, to) = (to, from);
            }
        }

        double weight = 1.0;
        if (_graph.IsWeighted)
        {
            var w = await _dialogs.Prompt("Вес ребра", $"{from} -> {to}", "1", "1");
            if (!string.IsNullOrWhiteSpace(w) && double.TryParse(w, out var parsed)) weight = parsed;
        }

        try
        {
            _graph.AddEdge(from, to, weight);
            AppendLog($"Ребро добавлено: {from} -> {to}");
            _edgeFromVertex = null;
            _renderState.SelectedVertex = null;
            UpdateRender();
        }
        catch (Exception ex)
        {
            await _dialogs.ShowMessage("Ошибка", ex.Message);
        }
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName == null) return;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
