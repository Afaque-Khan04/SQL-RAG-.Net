namespace AdhocSystem.Api.Services.Semantic;

public interface ILocalEmbeddingGenerator
{
    float[] GenerateEmbedding(string text, int dimension = 384);
    byte[] SerializeVector(float[] vector);
    float[] DeserializeVector(byte[] bytes);
}
