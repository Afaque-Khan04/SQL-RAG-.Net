using AdhocSystem.Api.Services.Semantic;
using Xunit;

namespace AdhocSystem.Tests;

public class QueryDecomposerTests
{
    private readonly QueryDecomposer _decomposer = new();

    [Fact]
    public void Decompose_SingleQuery_ReturnsSingle()
    {
        var result = _decomposer.Decompose("long distance touring bikes");
        Assert.Single(result);
        Assert.Equal("long distance touring bikes", result[0]);
    }

    [Fact]
    public void Decompose_Conjunction_ReturnsMultipleSubQueries()
    {
        var result = _decomposer.Decompose("best bike and best helmet");
        Assert.Equal(2, result.Count);
        Assert.Contains("best bike", result);
        Assert.Contains("best helmet", result);
    }

    [Fact]
    public void Decompose_InheritsAdjective_WhenMissingOnSubsequentParts()
    {
        var result = _decomposer.Decompose("best bike and helmet");
        Assert.Equal(2, result.Count);
        Assert.Contains("best bike", result);
        Assert.Contains("best helmet", result);
    }

    [Fact]
    public void Decompose_ComparePattern_SplitsCorrectly()
    {
        var result = _decomposer.Decompose("compare mountain bikes and road bikes");
        Assert.Equal(2, result.Count);
        Assert.Equal("mountain bikes", result[0]);
        Assert.Equal("road bikes", result[1]);
    }
}
