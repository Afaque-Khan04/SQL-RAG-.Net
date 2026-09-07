using System.Numerics.Tensors;
using AdhocSystem.Api.Services.Semantic;
using Xunit;

namespace AdhocSystem.Tests;

public class LocalEmbeddingGeneratorTests
{
    private readonly LocalEmbeddingGenerator _generator = new();

    [Fact]
    public void GenerateEmbedding_ProducesNormalizedVector()
    {
        var text = "Lightweight titanium road bicycle frame with carbon fork";
        var vec = _generator.GenerateEmbedding(text, 384);

        Assert.NotNull(vec);
        Assert.Equal(384, vec.Length);

        // Check L2 norm equals 1.0 (within epsilon)
        var sumSquares = vec.Sum(v => v * v);
        Assert.True(Math.Abs(sumSquares - 1.0f) < 1e-4, $"Vector L2 norm should be 1.0, got {sumSquares}");
    }

    [Fact]
    public void GenerateEmbedding_RelatedTermsHavePositiveCosineSimilarity()
    {
        var textA = "water resistant rain jacket for mountain biking";
        var textB = "waterproof cycling jacket for wet weather ride";
        var textUnrelated = "titanium crankset bottom bracket tool";

        var vecA = _generator.GenerateEmbedding(textA, 384);
        var vecB = _generator.GenerateEmbedding(textB, 384);
        var vecUnrelated = _generator.GenerateEmbedding(textUnrelated, 384);

        var simAB = TensorPrimitives.CosineSimilarity(vecA, vecB);
        var simAUnrelated = TensorPrimitives.CosineSimilarity(vecA, vecUnrelated);

        Assert.True(simAB > 0.2f, $"Related texts should have positive similarity, got {simAB}");
        Assert.True(simAB > simAUnrelated, $"Related texts ({simAB}) should score higher than unrelated ({simAUnrelated})");
    }

    [Fact]
    public void Serialization_RoundTripPreservesVector()
    {
        var text = "Ultra light carbon fiber handlebar";
        var original = _generator.GenerateEmbedding(text, 384);

        var bytes = _generator.SerializeVector(original);
        Assert.Equal(384 * sizeof(float), bytes.Length);

        var restored = _generator.DeserializeVector(bytes);
        Assert.Equal(original.Length, restored.Length);

        for (int i = 0; i < original.Length; i++)
        {
            Assert.Equal(original[i], restored[i]);
        }
    }
}
