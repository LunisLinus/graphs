using Graph;

namespace Graph.Tests;

public sealed class GraphConversionTests
{
    [Fact]
    public void ConvertTo_UnweightedToWeighted_AssignsDefaultWeight()
    {
        var g = new Graph<string>(isDirected: false, isWeighted: false);
        g.AddVertex("A");
        g.AddVertex("B");
        g.AddEdge("A", "B");

        var converted = g.ConvertTo(isDirected: false, isWeighted: true, defaultWeightForNewWeighted: 5);
        var edges = converted.GetEdgeList();

        Assert.True(converted.IsWeighted);
        Assert.False(converted.IsDirected);
        Assert.Single(edges);
        Assert.Equal(5, edges[0].Weight);
    }

    [Fact]
    public void ConvertTo_UndirectedToDirected_UsesSingleDirection()
    {
        var g = new Graph<string>(isDirected: false, isWeighted: true);
        g.AddVertex("A");
        g.AddVertex("B");
        g.AddEdge("A", "B", 2);

        var converted = g.ConvertTo(isDirected: true, isWeighted: true);
        var edges = converted.GetEdgeList();

        Assert.True(converted.IsDirected);
        Assert.True(converted.IsWeighted);
        Assert.Single(edges);
        Assert.Contains(edges, e => e.From == "A" && e.To == "B" && e.Weight == 2);
    }

    [Fact]
    public void ConvertTo_DirectedToUndirected_PicksMinimumWeightIfBothDirectionsExist()
    {
        var g = new Graph<string>(isDirected: true, isWeighted: true);
        g.AddVertex("A");
        g.AddVertex("B");
        g.AddEdge("A", "B", 7);
        g.AddEdge("B", "A", 3);

        var converted = g.ConvertTo(isDirected: false, isWeighted: true);
        var edges = converted.GetEdgeList();

        Assert.False(converted.IsDirected);
        Assert.True(converted.IsWeighted);
        Assert.Single(edges);
        Assert.Equal(3, edges[0].Weight);
    }
}
