namespace AdhocSystem.Api.Services.Database;

public interface ISchemaIntrospector
{
    Task<string> GetDynamicSchemaContextAsync(CancellationToken cancellationToken = default);
    void InvalidateCache();
}
