namespace Graph.Gui;

public sealed class GraphDrawable : IDrawable
{
    private readonly GraphRenderState _state;

    public GraphDrawable(GraphRenderState state)
    {
        _state = state;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();
        canvas.Antialias = true;

        var bg = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#1F1F1F") : Colors.White;
        canvas.FillColor = bg;
        canvas.FillRectangle(dirtyRect);

        DrawEdges(canvas);
        DrawVertices(canvas);

        canvas.RestoreState();
    }

    private void DrawEdges(ICanvas canvas)
    {
        var stroke = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#3C3C3C") : Color.FromArgb("#DADADA");
        var committed = Color.FromArgb("#4F8CFF");
        var labelFill = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#E92A2A2A") : Color.FromArgb("#F2FFFFFF");
        var labelText = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black;

        foreach (var (from, to, weight) in _state.Edges)
        {
            if (!_state.Positions.TryGetValue(from, out var p1)) continue;
            if (!_state.Positions.TryGetValue(to, out var p2)) continue;

            var key = GraphStepParsing.NormalizeEdge(from, to, _state.IsDirected);
            bool isHighlighted = _state.HighlightEdges.Contains(key);
            bool isCommitted = _state.CommittedEdges.Contains(key);

            var edgeColor = isHighlighted ? Color.FromArgb("#FF6B35") : (isCommitted ? committed : stroke);
            canvas.StrokeColor = edgeColor;
            canvas.StrokeSize = isHighlighted ? 5 : (isCommitted ? 4 : 2);

            canvas.DrawLine(p1, p2);

            if (_state.IsDirected)
            {
                DrawArrowHead(canvas, p1, p2, edgeColor);
            }

            if (_state.IsWeighted)
            {
                var label = weight.ToString("0.###");
                var mid = new PointF((p1.X + p2.X) / 2f, (p1.Y + p2.Y) / 2f);

                var dx = p2.X - p1.X;
                var dy = p2.Y - p1.Y;
                var len = (float)Math.Sqrt(dx * dx + dy * dy);
                if (len < 0.001f) continue;

                var nx = -dy / len;
                var ny = dx / len;
                var offset = _state.IsDirected ? 14f : 10f;
                var anchor = new PointF(mid.X + nx * offset, mid.Y + ny * offset);

                canvas.FontSize = 12;

                float w = 12 + label.Length * 7;
                const float h = 18;

                var rect = new RectF(anchor.X - w / 2f, anchor.Y - h / 2f, w, h);

                canvas.FillColor = labelFill;
                canvas.FillRoundedRectangle(rect, 8);

                canvas.StrokeColor = edgeColor;
                canvas.StrokeSize = 1;
                canvas.DrawRoundedRectangle(rect, 8);

                canvas.FontColor = labelText;
                canvas.DrawString(label, rect, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
    }

    private void DrawArrowHead(ICanvas canvas, PointF from, PointF to, Color color)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var len = (float)Math.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f) return;

        var ux = dx / len;
        var uy = dy / len;

        const float size = 10f;
        var tip = new PointF(to.X - ux * 18, to.Y - uy * 18);
        var left = new PointF(
            tip.X - ux * size - uy * (size / 2),
            tip.Y - uy * size + ux * (size / 2)
        );
        var right = new PointF(
            tip.X - ux * size + uy * (size / 2),
            tip.Y - uy * size - ux * (size / 2)
        );

        var path = new PathF();
        path.MoveTo(tip);
        path.LineTo(left);
        path.LineTo(right);
        path.Close();

        canvas.FillColor = color;
        canvas.FillPath(path);
    }

    private void DrawVertices(ICanvas canvas)
    {
        var fill = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#2A2A2A") : Color.FromArgb("#F3F3F3");
        var stroke = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#5A5A5A") : Color.FromArgb("#C8C8C8");
        var text = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black;
        var selection = Color.FromArgb("#4F8CFF");
        var visited = Color.FromArgb("#4F8CFF");
        var annotationText = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#BBBBBB") : Color.FromArgb("#666666");

        foreach (var v in _state.Vertices)
        {
            if (!_state.Positions.TryGetValue(v, out var p)) continue;

            bool isHighlighted = _state.HighlightVertices.Contains(v);
            bool isSelected = _state.SelectedVertex == v;
            bool isVisited = _state.VisitedVertices.Contains(v);

            float r = isHighlighted ? 20 : 16;
            canvas.FillColor = isHighlighted ? Color.FromArgb("#FF6B35") : (isVisited ? visited : fill);
            canvas.StrokeColor = isHighlighted ? Color.FromArgb("#FF6B35") : (isVisited ? visited : stroke);
            canvas.StrokeSize = 2;

            canvas.FillCircle(p, r);
            canvas.DrawCircle(p, r);

            canvas.FontColor = (isHighlighted || isVisited) ? Colors.White : text;
            canvas.FontSize = 13;
            canvas.DrawString(v, p.X - r, p.Y - 8, r * 2, 16, HorizontalAlignment.Center, VerticalAlignment.Center);

            if (_state.VertexAnnotations.TryGetValue(v, out var annotation) && !string.IsNullOrWhiteSpace(annotation))
            {
                canvas.FontColor = annotationText;
                canvas.FontSize = 11;
                canvas.DrawString(annotation, p.X - 40, p.Y + r + 2, 80, 14, HorizontalAlignment.Center, VerticalAlignment.Center);
            }

            if (isSelected)
            {
                canvas.StrokeColor = selection;
                canvas.StrokeSize = 3;
                canvas.DrawCircle(p, r + 6);
            }
        }
    }
}
