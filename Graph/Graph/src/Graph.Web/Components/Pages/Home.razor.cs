using System.Globalization;
using Graph;
using Graph.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Graph.Web.Components.Pages;

public class HomeBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected IJSRuntime Js { get; set; } = default!;

    protected readonly record struct GraphPoint(double X, double Y);
    protected readonly record struct EdgeVisual(
        string From,
        string To,
        string X1,
        string Y1,
        string X2,
        string Y2,
        string LabelX,
        string LabelY,
        string LabelBoxX,
        string LabelBoxY,
        string WeightText,
        string CssClass);

    protected readonly record struct VertexVisual(
        string Name,
        string X,
        string Y,
        string Transform,
        string Radius,
        string Annotation,
        string CssClass);

    protected Graph<string> _graph = default!;
    protected Graph<string> Graph => _graph;

    protected ElementReference _svgRef;
    protected string _statusText = "";
    protected string _resultTitle = "";
    protected string _resultText = "";
    protected bool _stepMode = true;
    protected bool _newDirected;
    protected bool _newWeighted;
    protected bool _showConvertPanel;
    protected bool _convertDirected;
    protected bool _convertWeighted;
    protected string _convertDefaultWeightText = "1";
    protected string _newVertexName = "";
    protected string _edgeWeightInput = "1";
    protected string _kValueText = "3";
    protected List<string> _activityLog = new();

    private readonly Dictionary<string, GraphPoint> _positions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _visitedVertices = new(StringComparer.Ordinal);
    private readonly HashSet<GraphEdgeKey> _committedEdges = new();
    private readonly Dictionary<string, string> _vertexAnnotations = new(StringComparer.Ordinal);
    private readonly List<string> _selectedVertices = new();
    private HashSet<string> _highlightVertices = new(StringComparer.Ordinal);
    private HashSet<GraphEdgeKey> _highlightEdges = new();
    private List<GraphStep> _stepSessionSteps = new();
    private int _stepIndex = -1;
    private string? _dragVertex;
    private bool _dragMoved;
    private DotNetObjectReference<HomeBase>? _hotkeyReference;
    private const double CanvasWidth = 1100;
    private const double CanvasHeight = 760;
    private const double MinZoom = 0.4d;
    private const double MaxZoom = 2.2d;
    private double _zoom = 0.85d;

    protected bool HasStepSession => _stepSessionSteps.Count > 0;
    protected bool HasSelection => _selectedVertices.Count > 0;
    protected bool HasSingleSelection => _selectedVertices.Count >= 1;
    protected bool HasPairSelection => _selectedVertices.Count >= 2;
    protected IReadOnlyList<string> SelectedVertices => _selectedVertices;
    protected string? PrimarySelection => _selectedVertices.FirstOrDefault();
    protected string? SecondarySelection => _selectedVertices.Skip(1).FirstOrDefault();
    protected bool CanPrevStep => HasStepSession && _stepIndex >= 0;
    protected bool CanNextStep => HasStepSession && _stepIndex < _stepSessionSteps.Count - 1;
    protected int ZoomPercent => (int)Math.Round(_zoom * 100d);
    protected string SelectedVerticesLabel => _selectedVertices.Count == 0 ? "Ничего" : string.Join(" → ", _selectedVertices);
    protected string SelectedVerticesHelp =>
        _selectedVertices.Count switch
        {
            0 => "Выдели одну вершину для запросов или две вершины по порядку для ребра и алгоритмов.",
            1 => $"Выбрана вершина '{_selectedVertices[0]}'. Нажми ещё одну, чтобы собрать пару.",
            _ => $"Пара: {_selectedVertices[0]} → {_selectedVertices[1]}"
        };
    protected string StepStatusText =>
        HasStepSession
            ? (_stepIndex >= 0 && _stepIndex < _stepSessionSteps.Count
                ? _stepSessionSteps[_stepIndex].Text
                : "Алгоритм готов к пошаговому просмотру.")
            : "Подсветка алгоритма активна.";
    protected string StepHelpText =>
        HasStepSession
            ? "Используй кнопки навигации, чтобы идти по шагам и смотреть подсветку на графе."
            : "Можно сбросить текущую подсветку и вернуться к обычному редактированию.";
    protected string StepProgressCaption =>
        HasStepSession ? "текущий шаг" : "визуализация";
    protected string SvgViewBox
    {
        get
        {
            var width = CanvasWidth / _zoom;
            var height = CanvasHeight / _zoom;
            var x = (CanvasWidth - width) / 2d;
            var y = (CanvasHeight - height) / 2d;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{x:0.###} {y:0.###} {width:0.###} {height:0.###}");
        }
    }
    protected bool HasAlgorithmState =>
        HasStepSession ||
        _visitedVertices.Count > 0 ||
        _committedEdges.Count > 0 ||
        _vertexAnnotations.Count > 0 ||
        _highlightVertices.Count > 0 ||
        _highlightEdges.Count > 0;

    protected string StepProgress => HasStepSession ? $"{Math.Max(_stepIndex + 1, 0)}/{_stepSessionSteps.Count}" : "0/0";
    protected string GraphSummary =>
        $"Вершины: {_graph.Vertices.Count}, рёбра: {_graph.GetEdgeList().Count}, ориентированный: {(_graph.IsDirected ? "да" : "нет")}, взвешенный: {(_graph.IsWeighted ? "да" : "нет")}";

    protected IReadOnlyList<VertexVisual> VertexVisuals =>
        _graph.Vertices
            .OrderBy(v => v)
            .Select(vertex =>
            {
                var point = _positions.TryGetValue(vertex, out var position) ? position : new GraphPoint(CanvasWidth / 2, CanvasHeight / 2);
                var isHighlighted = _highlightVertices.Contains(vertex);
                var isSelected = _selectedVertices.Contains(vertex);
                var isVisited = _visitedVertices.Contains(vertex);
                var css = isHighlighted ? "vertex highlight" : isVisited ? "vertex visited" : isSelected ? "vertex selected" : "vertex";
                var radius = isHighlighted ? 25d : 21d;
                _vertexAnnotations.TryGetValue(vertex, out var annotation);
                return new VertexVisual(
                    vertex,
                    Svg(point.X),
                    Svg(point.Y),
                    $"translate({Svg(point.X)} {Svg(point.Y)})",
                    Svg(radius),
                    annotation ?? "",
                    css);
            })
            .ToArray();

    protected IReadOnlyList<EdgeVisual> EdgeVisuals =>
        _graph.GetEdgeList()
            .Select(edge =>
            {
                var from = _positions[edge.From];
                var to = _positions[edge.To];
                var dx = to.X - from.X;
                var dy = to.Y - from.Y;
                var length = Math.Sqrt(dx * dx + dy * dy);
                var safe = Math.Max(length, 0.001d);
                var ux = dx / safe;
                var uy = dy / safe;
                var startRadius = 26d;
                var endRadius = _graph.IsDirected ? 30d : 24d;
                var x1 = from.X + ux * startRadius;
                var y1 = from.Y + uy * startRadius;
                var x2 = to.X - ux * endRadius;
                var y2 = to.Y - uy * endRadius;
                var nx = -uy;
                var ny = ux;
                var offset = _graph.IsDirected ? 18d : 12d;
                var labelX = (x1 + x2) / 2d + nx * offset;
                var labelY = (y1 + y2) / 2d + ny * offset;
                var key = GraphStepParsing.NormalizeEdge(edge.From, edge.To, _graph.IsDirected);
                var css = _highlightEdges.Contains(key) ? "edge highlight" : _committedEdges.Contains(key) ? "edge committed" : "edge";
                return new EdgeVisual(
                    edge.From,
                    edge.To,
                    Svg(x1),
                    Svg(y1),
                    Svg(x2),
                    Svg(y2),
                    Svg(labelX),
                    Svg(labelY),
                    Svg(labelX - 24d),
                    Svg(labelY - 12d),
                    edge.Weight.ToString("0.###", CultureInfo.InvariantCulture),
                    css);
            })
            .ToArray();

    protected override void OnInitialized()
    {
        _graph = new Graph<string>(false, false);
        SeedDefaultGraph();
        SyncGraphForms();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _hotkeyReference = DotNetObjectReference.Create(this);
        await Js.InvokeVoidAsync("graphInterop.registerHotkeys", _hotkeyReference);
    }

    protected void CreateNewGraph()
    {
        _graph = new Graph<string>(_newDirected, _newWeighted);
        _positions.Clear();
        ResetAlgorithmVisualState();
        ClearStepSession();
        _selectedVertices.Clear();
        _zoom = 1d;
        _resultTitle = "Новый граф";
        _resultText = "Создан новый пустой граф.";
        SetStatus("Новый граф создан.");
    }

    protected void ToggleConvertPanel()
    {
        _showConvertPanel = !_showConvertPanel;
        if (_showConvertPanel)
        {
            _convertDirected = _graph.IsDirected;
            _convertWeighted = _graph.IsWeighted;
            _convertDefaultWeightText = "1";
        }

        StateHasChanged();
    }

    protected void CancelConvert()
    {
        _showConvertPanel = false;
        StateHasChanged();
    }

    protected void ApplyConvert()
    {
        try
        {
            double defaultWeight = 1d;
            if (_convertWeighted && !_graph.IsWeighted)
            {
                if (!double.TryParse(_convertDefaultWeightText.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out defaultWeight))
                {
                    ShowError("Некорректный вес по умолчанию.");
                    return;
                }
            }

            ExitAlgorithm();
            _graph = _graph.ConvertTo(_convertDirected, _convertWeighted, defaultWeight);
            SyncGraphForms();
            _showConvertPanel = false;

            _resultTitle = "Преобразование";
            _resultText = $"Граф преобразован: {(_graph.IsDirected ? "DIRECTED" : "UNDIRECTED")} {(_graph.IsWeighted ? "WEIGHTED" : "UNWEIGHTED")}";
            SetStatus("Граф преобразован.");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected async Task LoadGraphFromFile(InputFileChangeEventArgs args)
    {
        var file = args.File;
        if (file is null)
        {
            return;
        }

        try
        {
            await using var stream = file.OpenReadStream(2_000_000);
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            var path = Path.GetTempFileName();
            await File.WriteAllTextAsync(path, content);

            try
            {
                _graph = new Graph<string>(path, static s => s, AppendLog);
            }
            finally
            {
                File.Delete(path);
            }

            _positions.Clear();
            ResetAlgorithmVisualState();
            ClearStepSession();
            AutoLayout();
            _resultTitle = "Импорт";
            _resultText = $"Файл `{file.Name}` загружен.\n\n{_graph}";
            SetStatus($"Граф загружен из {file.Name}");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected async Task ExportEdgeList()
    {
        await ExportGraph("graph-edge-list.txt", GraphSaveFormat.EdgeList);
    }

    protected async Task ExportAdjacencyList()
    {
        await ExportGraph("graph-adjacency-list.txt", GraphSaveFormat.AdjacencyList);
    }

    protected void AddVertexFromPanel()
    {
        if (string.IsNullOrWhiteSpace(_newVertexName))
        {
            ShowError("Введите имя вершины.");
            return;
        }

        if (!_graph.AddVertex(_newVertexName))
        {
            ShowError($"Вершина '{_newVertexName}' уже существует.");
            return;
        }

        _positions[_newVertexName] = SpawnPositionNearCenter();
        NormalizePositionsToCanvas();
        SetStatus($"Вершина '{_newVertexName}' добавлена.");
        _newVertexName = "";
    }

    protected void RemoveVertexFromPanel()
    {
        if (!HasSelection)
        {
            ShowError("Сначала выбери вершину на графе.");
            return;
        }

        var removed = new List<string>();
        foreach (var vertex in _selectedVertices.ToArray())
        {
            if (_graph.RemoveVertex(vertex))
            {
                _positions.Remove(vertex);
                removed.Add(vertex);
            }
        }

        if (_positions.Count > 1)
        {
            NormalizePositionsToCanvas();
            FitGraphToView();
        }

        _selectedVertices.Clear();
        SetStatus(removed.Count == 0
            ? "Удалить выбранные вершины не удалось."
            : $"Удалены вершины: {string.Join(", ", removed)}.");
    }

    protected void AddEdgeFromPanel()
    {
        if (!HasPairSelection)
        {
            ShowError("Выбери две вершины по порядку на графе.");
            return;
        }

        try
        {
            var weight = ParseWeight(_edgeWeightInput);
            _graph.AddEdge(PrimarySelection!, SecondarySelection!, weight);
            SetStatus($"Ребро '{PrimarySelection} -> {SecondarySelection}' добавлено.");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected void RemoveEdgeFromPanel()
    {
        if (!HasPairSelection)
        {
            ShowError("Выбери две вершины по порядку на графе.");
            return;
        }

        if (!_graph.RemoveEdge(PrimarySelection!, SecondarySelection!))
        {
            ShowError("Ребро не найдено.");
            return;
        }

        SetStatus($"Ребро '{PrimarySelection} -> {SecondarySelection}' удалено.");
    }

    protected void ToggleEdgeDirection(string from, string to)
    {
        if (HasAlgorithmState)
        {
            return;
        }

        if (!_graph.IsDirected)
        {
            SetStatus("Направление можно менять только в ориентированном графе.");
            return;
        }

        try
        {
            var current = _graph.GetEdgeList().First(e => e.From == from && e.To == to);
            _graph.RemoveEdge(from, to);
            _graph.RemoveEdge(to, from);
            _graph.AddEdge(to, from, current.Weight);
            SetStatus($"Направление изменено: {to} -> {from}");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected async Task EditEdgeWeight(string from, string to)
    {
        if (HasAlgorithmState)
        {
            return;
        }

        if (!_graph.IsWeighted)
        {
            return;
        }

        try
        {
            var current = _graph.GetEdgeList().First(e => e.From == from && e.To == to);
            var input = await Js.InvokeAsync<string?>("graphInterop.promptText", "Новый вес ребра:", current.Weight.ToString("0.###", CultureInfo.InvariantCulture));
            if (string.IsNullOrWhiteSpace(input))
            {
                return;
            }

            if (!double.TryParse(input.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var newWeight))
            {
                ShowError("Некорректный вес.");
                return;
            }

            _graph.RemoveEdge(from, to);
            _graph.AddEdge(from, to, newWeight);
            SetStatus($"Вес обновлён: {from} -> {to} = {newWeight:0.###}");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected void ShowAdjacency()
    {
        _resultTitle = "Список смежности";
        _resultText = _graph.ToString();
        SetStatus("Показан список смежности.");
    }

    protected void ShowNonAdjacent()
    {
        if (!HasSingleSelection)
        {
            ShowError("Выбери вершину на графе.");
            return;
        }

        try
        {
            var result = _graph.GetNonAdjacentVertices(PrimarySelection!);
            _resultTitle = "Не смежные вершины";
            _resultText = result.Count == 0 ? "Нет не смежных вершин." : string.Join(", ", result);
            SetStatus($"Запрос для вершины '{PrimarySelection}' выполнен.");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected void ShowIsolated()
    {
        var result = _graph.GetIsolatedVertices();
        _resultTitle = "Изолированные вершины";
        _resultText = result.Count == 0 ? "Нет изолированных вершин." : string.Join(", ", result);
        SetStatus("Проверка изолированных вершин выполнена.");
    }

    protected void RemovePendantEdges()
    {
        _graph = _graph.RemoveEdgesToPendantVertices();
        ResetAlgorithmVisualState();
        if (_graph.Vertices.Count > 0)
        {
            AutoLayout();
        }
        SetStatus("Рёбра к висячим вершинам удалены.");
        _resultTitle = "Преобразование";
        _resultText = "Операция завершена. Граф обновлён.";
    }

    protected async Task RunCyclesDfs()
    {
        try
        {
            var cycles = await RunWithSteps(log => _graph.GetFundamentalCyclesDFS(log));
            _resultTitle = "Фундаментальные циклы DFS";
            _resultText = cycles.Count == 0 ? "Циклов не найдено." : string.Join("\n", cycles.Select(c => string.Join(" -> ", c)));
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected async Task RunCyclesBfs()
    {
        try
        {
            var cycles = await RunWithSteps(log => _graph.GetFundamentalCyclesBFS(log));
            _resultTitle = "Фундаментальные циклы BFS";
            _resultText = cycles.Count == 0 ? "Циклов не найдено." : string.Join("\n", cycles.Select(c => string.Join(" -> ", c)));
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected void RunEquidistant()
    {
        if (!HasPairSelection)
        {
            ShowError("Выбери две вершины на графе для поиска равных путей.");
            return;
        }

        try
        {
            var bfs = _graph.FindEquidistantVertexBFS(PrimarySelection!, SecondarySelection!);
            var dfs = _graph.FindEquidistantVertexDFS(PrimarySelection!, SecondarySelection!);
            _resultTitle = "Равные пути";
            _resultText = $"{FormatVertexResult("BFS", bfs)}\n{FormatVertexResult("DFS", dfs)}";
            SetStatus("Поиск вершин с равными путями завершён.");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected async Task RunBoruvka()
    {
        if (_graph.IsDirected)
        {
            ShowError("Алгоритм Борувки работает только для неориентированных графов.");
            return;
        }

        try
        {
            var mst = await RunWithSteps(log => _graph.GetMinimumSpanningTreeBoruvka(log));
            var edges = mst.GetEdgeList();
            _resultTitle = "Минимальное остовное дерево";
            _resultText = $"{string.Join("\n", edges.Select(e => e.ToString()))}\n\nОбщий вес: {edges.Sum(e => e.Weight):0.###}";
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected async Task RunDijkstra()
    {
        if (!HasPairSelection)
        {
            ShowError("Выбери две вершины на графе: старт, затем финиш.");
            return;
        }

        if (!_graph.IsWeighted)
        {
            ShowError("Dijkstra требует взвешенный граф.");
            return;
        }

        if (_graph.GetEdgeList().Any(e => e.Weight < 0))
        {
            ShowError("Dijkstra неприменим для отрицательных весов.");
            return;
        }

        try
        {
            var (distance, paths) = await RunWithSteps(log => _graph.GetShortestPathsDijkstra(PrimarySelection!, SecondarySelection!, log));
            _resultTitle = "Dijkstra";
            _resultText = double.IsPositiveInfinity(distance) || paths.Count == 0
                ? "Путь не существует."
                : $"Дистанция: {distance:0.###}\n\n{string.Join("\n", paths.Select(path => string.Join(" -> ", path)))}";
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected async Task RunKShortestPaths()
    {
        if (!HasPairSelection)
        {
            ShowError("Выбери две вершины на графе: старт, затем финиш.");
            return;
        }

        if (!int.TryParse(_kValueText, out var k) || k <= 0)
        {
            ShowError("K должно быть положительным числом.");
            return;
        }

        try
        {
            var paths = await RunWithSteps(log => _graph.GetKShortestPathsBellmanFord(PrimarySelection!, SecondarySelection!, k, log));
            _resultTitle = "K кратчайших путей";
            _resultText = paths.Count == 0
                ? "Пути не найдены."
                : string.Join("\n", paths.Select((path, index) => $"#{index + 1} ({path.distance:0.###}): {string.Join(" -> ", path.path)}"));
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected async Task RunNegativeCycles()
    {
        try
        {
            var cycles = await RunWithSteps(log => _graph.GetNegativeCyclesFloydWarshall(log));
            _resultTitle = "Floyd-Warshall";
            _resultText = cycles.Count == 0 ? "Циклов отрицательного веса не найдено." : string.Join("\n", cycles.Select(c => string.Join(" -> ", c)));
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected async Task RunMaxFlow()
    {
        if (!HasPairSelection)
        {
            ShowError("Выбери две вершины на графе: source, затем sink.");
            return;
        }

        if (!_graph.IsDirected || !_graph.IsWeighted)
        {
            ShowError("Edmonds-Karp требует ориентированный взвешенный граф.");
            return;
        }

        if (_graph.GetEdgeList().Any(e => e.Weight < 0))
        {
            ShowError("Пропускные способности должны быть неотрицательными.");
            return;
        }

        try
        {
            var maxFlow = await RunWithSteps(log => _graph.GetMaxFlowEdmondsKarp(PrimarySelection!, SecondarySelection!, log));
            _resultTitle = "Edmonds-Karp";
            _resultText = $"Максимальный поток: {maxFlow:0.###}";
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    protected async Task OnCanvasPointerDown(PointerEventArgs args)
    {
        if (_dragVertex is not null)
        {
            return;
        }

        if (HasAlgorithmState)
        {
            return;
        }

        if (!args.ShiftKey)
        {
            return;
        }

        var point = await GetSvgPoint(args);
        if (HitTestVertex(point) is null)
        {
            await AddVertexAtPoint(point);
        }
    }

    protected async Task OnVertexPointerDown(string vertex, PointerEventArgs args)
    {
        _dragVertex = vertex;
        _dragMoved = false;
        await InvokeAsync(StateHasChanged);
    }

    protected async Task OnPointerMove(PointerEventArgs args)
    {
        if (_dragVertex is null)
        {
            return;
        }

        var point = await GetSvgPoint(args);
        _positions[_dragVertex] = ClampPoint(point);
        _dragMoved = true;
        await InvokeAsync(StateHasChanged);
    }

    protected async Task OnPointerUp(PointerEventArgs args)
    {
        if (_dragVertex is null)
        {
            return;
        }

        var vertex = _dragVertex;
        _dragVertex = null;

        if (_dragMoved)
        {
            _dragMoved = false;
            await InvokeAsync(StateHasChanged);
            return;
        }

        if (HasAlgorithmState)
        {
            return;
        }

        await HandleVertexTap(vertex);
    }

    protected void PrevStep()
    {
        if (!HasStepSession)
        {
            return;
        }

        var newIndex = _stepIndex - 1;
        ResetAlgorithmVisualState();

        if (newIndex >= 0)
        {
            for (var i = 0; i <= newIndex; i++)
            {
                ApplyStep(_stepSessionSteps[i]);
            }
        }

        _stepIndex = newIndex;
        StateHasChanged();
    }

    protected void NextStep()
    {
        if (!CanNextStep)
        {
            return;
        }

        _stepIndex++;
        ApplyStep(_stepSessionSteps[_stepIndex]);
        _statusText = _stepSessionSteps[_stepIndex].Text;
        StateHasChanged();
    }

    protected void ExitAlgorithm()
    {
        ClearStepSession();
        ResetAlgorithmVisualState();
        _statusText = "";
        StateHasChanged();
    }

    [JSInvokable]
    public Task HandleHotkey(string key)
    {
        switch (key)
        {
            case "ArrowLeft":
                if (CanPrevStep)
                {
                    PrevStep();
                }
                break;
            case "ArrowRight":
                if (CanNextStep)
                {
                    NextStep();
                }
                break;
            case "Escape":
                if (HasStepSession || HasAlgorithmState)
                {
                    ExitAlgorithm();
                }
                break;
        }

        return Task.CompletedTask;
    }

    protected void AutoLayout()
    {
        var vertices = _graph.Vertices.OrderBy(v => v).ToArray();
        _positions.Clear();

        if (vertices.Length == 0)
        {
            _zoom = 1d;
            StateHasChanged();
            return;
        }

        InitializeCircularLayout(vertices);
        RunForceLayout(vertices);
        NormalizePositionsToCanvas();
        FitGraphToView();
        StateHasChanged();
    }

    protected void FitGraphToView()
    {
        if (_positions.Count == 0)
        {
            _zoom = 1d;
            StateHasChanged();
            return;
        }

        var bounds = GetGraphBounds();
        var contentWidth = Math.Max(bounds.maxX - bounds.minX, 80d);
        var contentHeight = Math.Max(bounds.maxY - bounds.minY, 80d);
        var availableWidth = CanvasWidth - 140d;
        var availableHeight = CanvasHeight - 140d;
        var scaleX = availableWidth / contentWidth;
        var scaleY = availableHeight / contentHeight;
        SetZoom(Math.Clamp(Math.Min(scaleX, scaleY), 0.55d, 1.45d));
    }

    protected void ClearSelection()
    {
        _selectedVertices.Clear();
        _statusText = "";
    }

    protected void ReverseSelectionOrder()
    {
        if (!HasPairSelection)
        {
            ShowError("Нужно выбрать две вершины, чтобы поменять их порядок.");
            return;
        }

        (_selectedVertices[0], _selectedVertices[1]) = (_selectedVertices[1], _selectedVertices[0]);
        SetStatus($"Порядок выбора изменён: {SelectedVerticesLabel}.");
    }

    protected void ZoomIn()
    {
        SetZoom(_zoom + 0.1d);
    }

    protected void ZoomOut()
    {
        SetZoom(_zoom - 0.1d);
    }

    protected void ResetZoom()
    {
        SetZoom(1d);
    }

    protected void OnZoomSliderChanged(ChangeEventArgs args)
    {
        if (args.Value is null)
        {
            return;
        }

        if (int.TryParse(args.Value.ToString(), out var percent))
        {
            SetZoom(percent / 100d);
        }
    }

    protected void OnCanvasWheel(WheelEventArgs args)
    {
        var step = args.DeltaY < 0 ? 0.1d : -0.1d;
        SetZoom(_zoom + step);
    }

    private async Task ExportGraph(string fileName, GraphSaveFormat format)
    {
        var content = BuildGraphText(format);
        await Js.InvokeVoidAsync("graphInterop.downloadText", fileName, content);
        _resultTitle = "Экспорт";
        _resultText = $"Подготовлен файл `{fileName}`.\n\n{content}";
        SetStatus($"Экспортирован {fileName}");
    }

    private string BuildGraphText(GraphSaveFormat format)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine($"{(_graph.IsDirected ? "DIRECTED" : "UNDIRECTED")} {(_graph.IsWeighted ? "WEIGHTED" : "UNWEIGHTED")}");
        builder.AppendLine(string.Join(" ", _graph.Vertices.OrderBy(v => v)));

        if (format == GraphSaveFormat.EdgeList)
        {
            foreach (var edge in _graph.GetEdgeList())
            {
                builder.Append(edge.From).Append(' ').Append(edge.To);
                if (_graph.IsWeighted)
                {
                    builder.Append(' ').Append(edge.Weight.ToString(CultureInfo.InvariantCulture));
                }
                builder.AppendLine();
            }
        }
        else
        {
            foreach (var vertex in _graph.Vertices.OrderBy(v => v))
            {
                var snapshot = _graph.GetAdjacencyListSnapshot();
                builder.Append(vertex);
                if (snapshot.TryGetValue(vertex, out var neighbors) && neighbors.Count > 0)
                {
                    builder.Append(':');
                    foreach (var neighbor in neighbors)
                    {
                        builder.Append(' ').Append(neighbor.Key);
                        if (_graph.IsWeighted)
                        {
                            builder.Append('(').Append(neighbor.Value.ToString("0.###", CultureInfo.InvariantCulture)).Append(')');
                        }
                    }
                }
                builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    private async Task AddVertexAtPoint(GraphPoint point)
    {
        var baseName = string.IsNullOrWhiteSpace(_newVertexName) ? "V" : _newVertexName.Trim();
        var name = baseName;
        var index = 1;

        while (_graph.ContainsVertex(name))
        {
            name = $"{baseName}{index++}";
        }

        _graph.AddVertex(name);
        _positions[name] = ClampPoint(point);
        _newVertexName = "";
        SetStatus($"Вершина '{name}' добавлена кликом.");
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleVertexTap(string vertex)
    {
        if (_selectedVertices.Contains(vertex))
        {
            _selectedVertices.Remove(vertex);
            _statusText = _selectedVertices.Count == 0
                ? "Выделение очищено."
                : $"Осталось выбрано: {SelectedVerticesLabel}.";
            return;
        }

        if (_selectedVertices.Count == 2)
        {
            _selectedVertices.RemoveAt(0);
        }

        _selectedVertices.Add(vertex);
        _statusText = SelectedVerticesHelp;
        await InvokeAsync(StateHasChanged);
    }

    private async Task<GraphPoint> GetSvgPoint(PointerEventArgs args)
    {
        var point = await Js.InvokeAsync<JsPoint>("graphInterop.getSvgPoint", _svgRef, args.ClientX, args.ClientY);
        return ClampPoint(new GraphPoint(point.X, point.Y));
    }

    private GraphPoint ClampPoint(GraphPoint point)
    {
        const double padding = 56d;
        return new GraphPoint(
            Math.Clamp(point.X, padding, CanvasWidth - padding),
            Math.Clamp(point.Y, padding, CanvasHeight - padding));
    }

    private string? HitTestVertex(GraphPoint point)
    {
        string? best = null;
        var bestDistance = double.MaxValue;

        foreach (var (name, position) in _positions)
        {
            var dx = position.X - point.X;
            var dy = position.Y - point.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= 28d && distance < bestDistance)
            {
                bestDistance = distance;
                best = name;
            }
        }

        return best;
    }

    private async Task<T> RunWithSteps<T>(Func<Action<string>, T> func)
    {
        ClearStepSession();
        ResetAlgorithmVisualState();
        SetStatus("Выполнение...");

        var steps = new List<GraphStep>();
        var result = await Task.Run(() => func(text =>
        {
            lock (steps)
            {
                steps.Add(GraphStepParsing.Parse(text, _graph.IsDirected));
            }
        }));

        if (_stepMode)
        {
            _stepSessionSteps = steps;
            _stepIndex = -1;
            SetStatus($"Шагов: {steps.Count}. Используйте навигацию.");
        }
        else
        {
            foreach (var step in steps)
            {
                ApplyStep(step);
                _statusText = step.Text;
                await InvokeAsync(StateHasChanged);
                await Task.Delay(180);
            }

            ClearHighlight();
            SetStatus("Готово.");
        }

        return result;
    }

    private void ApplyStep(GraphStep step)
    {
        _highlightVertices = new HashSet<string>(step.HighlightVertices, StringComparer.Ordinal);
        _highlightEdges = new HashSet<GraphEdgeKey>(step.HighlightEdges);

        switch (step.Kind)
        {
            case GraphStepKind.BoruvkaAddEdge:
                if (step.A is not null && step.B is not null)
                {
                    _committedEdges.Add(GraphStepParsing.NormalizeEdge(step.A, step.B, _graph.IsDirected));
                }
                break;
            case GraphStepKind.DijkstraStart:
                if (step.A is not null)
                {
                    _visitedVertices.Clear();
                    _vertexAnnotations.Clear();
                    foreach (var vertex in _graph.Vertices)
                    {
                        _vertexAnnotations[vertex] = "∞";
                    }
                    _vertexAnnotations[step.A] = "0";
                }
                break;
            case GraphStepKind.DijkstraExtract:
                if (step.A is not null)
                {
                    _visitedVertices.Add(step.A);
                    if (step.Value.HasValue)
                    {
                        _vertexAnnotations[step.A] = step.Value.Value.ToString("0.###", CultureInfo.InvariantCulture);
                    }
                }
                break;
            case GraphStepKind.DijkstraRelax:
                if (step.B is not null && step.Value.HasValue)
                {
                    _vertexAnnotations[step.B] = step.Value.Value.ToString("0.###", CultureInfo.InvariantCulture);
                }
                break;
            case GraphStepKind.BellmanUpdate:
                if (step.A is not null && step.Value.HasValue)
                {
                    _vertexAnnotations[step.A] = step.Value.Value.ToString("0.###", CultureInfo.InvariantCulture);
                }
                break;
            case GraphStepKind.TraversalVisit:
                if (step.A is not null)
                {
                    _visitedVertices.Add(step.A);
                }
                break;
        }

        AppendLog(step.Text);
    }

    private void ResetAlgorithmVisualState()
    {
        _visitedVertices.Clear();
        _committedEdges.Clear();
        _vertexAnnotations.Clear();
        ClearHighlight();
    }

    private void ClearHighlight()
    {
        _highlightVertices = new HashSet<string>(StringComparer.Ordinal);
        _highlightEdges = new HashSet<GraphEdgeKey>();
    }

    private void ClearStepSession()
    {
        _stepSessionSteps = new List<GraphStep>();
        _stepIndex = -1;
    }

    private void SeedDefaultGraph()
    {
        foreach (var vertex in new[] { "A", "B", "C", "D", "E", "F" })
        {
            _graph.AddVertex(vertex);
        }

        _graph.AddEdge("A", "B");
        _graph.AddEdge("A", "C");
        _graph.AddEdge("B", "D");
        _graph.AddEdge("C", "D");
        _graph.AddEdge("C", "E");
        _graph.AddEdge("D", "F");
        _graph.AddEdge("E", "F");
        AutoLayout();
        SetStatus("Демо-граф готов.");
    }

    private GraphPoint SpawnPositionNearCenter()
    {
        var count = _positions.Count;
        var angle = count * 137.5d * Math.PI / 180d;
        var radius = 40d + (count % 10) * 14d;
        return new GraphPoint(
            CanvasWidth / 2d + Math.Cos(angle) * radius,
            CanvasHeight / 2d + Math.Sin(angle) * radius);
    }

    private double ParseWeight(string raw)
    {
        if (!_graph.IsWeighted)
        {
            return 1d;
        }

        if (double.TryParse(raw.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }

        throw new InvalidOperationException("Вес ребра задан некорректно.");
    }

    private static string FormatVertexResult(string label, string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? $"{label}: не найдено" : $"{label}: {value}";
    }

    private void SetStatus(string message)
    {
        _statusText = message;
        AppendLog(message);
    }

    private void ShowError(string message)
    {
        _resultTitle = "Ошибка";
        _resultText = message;
        SetStatus(message);
    }

    private void AppendLog(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        _activityLog.Insert(0, $"{DateTime.Now:HH:mm:ss}  {text}");
        if (_activityLog.Count > 12)
        {
            _activityLog.RemoveRange(12, _activityLog.Count - 12);
        }
    }

    private void SyncGraphForms()
    {
        _newDirected = _graph.IsDirected;
        _newWeighted = _graph.IsWeighted;
    }

    private void SetZoom(double value)
    {
        _zoom = Math.Clamp(value, MinZoom, MaxZoom);
        StateHasChanged();
    }

    protected static string Svg(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private void InitializeCircularLayout(IReadOnlyList<string> vertices)
    {
        var centerX = CanvasWidth / 2d;
        var centerY = CanvasHeight / 2d;
        var radius = Math.Max(120d, Math.Min(CanvasWidth, CanvasHeight) / 2d - 180d);

        for (var index = 0; index < vertices.Count; index++)
        {
            var angle = 2d * Math.PI * index / Math.Max(vertices.Count, 1);
            _positions[vertices[index]] = new GraphPoint(
                centerX + Math.Cos(angle) * radius,
                centerY + Math.Sin(angle) * radius);
        }
    }

    private void RunForceLayout(IReadOnlyList<string> vertices)
    {
        var edgeList = _graph.GetEdgeList();
        if (vertices.Count <= 1)
        {
            return;
        }

        var area = CanvasWidth * CanvasHeight;
        var naturalLength = Math.Sqrt(area / vertices.Count) * 0.72d;
        var repulsion = naturalLength * naturalLength * 1.15d;

        for (var iteration = 0; iteration < 180; iteration++)
        {
            var forces = vertices.ToDictionary(v => v, _ => new GraphPoint(0d, 0d), StringComparer.Ordinal);

            for (var i = 0; i < vertices.Count; i++)
            {
                for (var j = i + 1; j < vertices.Count; j++)
                {
                    var a = vertices[i];
                    var b = vertices[j];
                    var deltaX = _positions[b].X - _positions[a].X;
                    var deltaY = _positions[b].Y - _positions[a].Y;
                    var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    var safeDistance = Math.Max(distance, 0.01d);
                    var directionX = deltaX / safeDistance;
                    var directionY = deltaY / safeDistance;
                    var force = repulsion / (safeDistance * safeDistance);

                    forces[a] = new GraphPoint(forces[a].X - directionX * force, forces[a].Y - directionY * force);
                    forces[b] = new GraphPoint(forces[b].X + directionX * force, forces[b].Y + directionY * force);
                }
            }

            foreach (var edge in edgeList)
            {
                var deltaX = _positions[edge.To].X - _positions[edge.From].X;
                var deltaY = _positions[edge.To].Y - _positions[edge.From].Y;
                var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                var safeDistance = Math.Max(distance, 0.01d);
                var directionX = deltaX / safeDistance;
                var directionY = deltaY / safeDistance;
                var force = (safeDistance - naturalLength) * 0.085d;

                forces[edge.From] = new GraphPoint(forces[edge.From].X + directionX * force, forces[edge.From].Y + directionY * force);
                forces[edge.To] = new GraphPoint(forces[edge.To].X - directionX * force, forces[edge.To].Y - directionY * force);
            }

            foreach (var vertex in vertices)
            {
                var point = _positions[vertex];
                var force = forces[vertex];
                var centerPullX = (CanvasWidth / 2d - point.X) * 0.003d;
                var centerPullY = (CanvasHeight / 2d - point.Y) * 0.003d;
                var next = new GraphPoint(
                    point.X + force.X + centerPullX,
                    point.Y + force.Y + centerPullY);
                _positions[vertex] = ClampPoint(next);
            }
        }
    }

    private void NormalizePositionsToCanvas()
    {
        if (_positions.Count == 0)
        {
            return;
        }

        var bounds = GetGraphBounds();
        var contentWidth = Math.Max(bounds.maxX - bounds.minX, 1d);
        var contentHeight = Math.Max(bounds.maxY - bounds.minY, 1d);
        var targetWidth = Math.Min(CanvasWidth - 180d, Math.Max(220d, contentWidth));
        var targetHeight = Math.Min(CanvasHeight - 180d, Math.Max(220d, contentHeight));
        var scale = Math.Min(targetWidth / contentWidth, targetHeight / contentHeight);
        var centerX = (bounds.minX + bounds.maxX) / 2d;
        var centerY = (bounds.minY + bounds.maxY) / 2d;

        foreach (var key in _positions.Keys.ToArray())
        {
            var point = _positions[key];
            var translated = new GraphPoint(
                (point.X - centerX) * scale + CanvasWidth / 2d,
                (point.Y - centerY) * scale + CanvasHeight / 2d);
            _positions[key] = ClampPoint(translated);
        }
    }

    private (double minX, double minY, double maxX, double maxY) GetGraphBounds()
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;

        foreach (var point in _positions.Values)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        return (minX, minY, maxX, maxY);
    }

    private sealed class JsPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Js.InvokeVoidAsync("graphInterop.unregisterHotkeys");
        }
        catch
        {
        }

        _hotkeyReference?.Dispose();
    }
}
