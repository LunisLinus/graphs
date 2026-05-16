namespace Graph.Gui;

public partial class MainPage : ContentPage, IUiDialogService
{
    private readonly GraphViewModel _vm;

    public MainPage()
    {
        InitializeComponent();
        _vm = new GraphViewModel(this, () => GraphView.Invalidate());
        BindingContext = _vm;

        GraphView.SizeChanged += (_, _) =>
        {
            _vm.SetViewport((float)GraphView.Width, (float)GraphView.Height);
            GraphView.Invalidate();
        };

        GraphView.StartInteraction += (_, e) =>
        {
            if (!e.Touches.Any()) return;
            var p = e.Touches.First();
            _vm.PointerDown(new PointF((float)p.X, (float)p.Y));
        };

        GraphView.DragInteraction += (_, e) =>
        {
            if (!e.Touches.Any()) return;
            var p = e.Touches.First();
            _vm.PointerMove(new PointF((float)p.X, (float)p.Y));
        };

        GraphView.EndInteraction += async (_, e) =>
        {
            if (!e.Touches.Any()) return;
            var p = e.Touches.First();
            await _vm.PointerUp(new PointF((float)p.X, (float)p.Y));
        };
    }

    public Task ShowMessage(string title, string message)
    {
        return DisplayAlert(title, message, "OK");
    }

    public Task<bool> Confirm(string title, string message, string accept, string cancel)
    {
        return DisplayAlert(title, message, accept, cancel);
    }

    public Task<string?> Prompt(string title, string message, string? placeholder = null, string? initialValue = null)
    {
        return DisplayPromptAsync(title, message, "OK", "Отмена", placeholder, initialValue: initialValue);
    }

    public Task<string?> Choose(string title, string cancel, params string[] options)
    {
        return DisplayActionSheet(title, cancel, null, options);
    }
}
