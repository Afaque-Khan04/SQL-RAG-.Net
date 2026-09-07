namespace AdhocSystem.Api.Services.Semantic;

public interface ISemanticSearchService
{
    Task<List<Dictionary<string, object?>>> SearchAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
}
