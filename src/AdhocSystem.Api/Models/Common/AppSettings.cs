namespace AdhocSystem.Api.Models.Common;

public class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; set; } = string.Empty;
    public string ReadOnlyConnectionString { get; set; } = string.Empty;
    public List<string> AllowedSchemas { get; set; } = new() { "Sales", "Purchasing", "Production" };
    public int MaxResultLimit { get; set; } = 150000;
}

public class LlmOptions
{
    public const string SectionName = "Llm";
    public string Provider { get; set; } = "groq";
    public string Model { get; set; } = "openai/gpt-oss-120b";
    public string ReasoningEffort { get; set; } = "low";
    public string GroqApiKey { get; set; } = string.Empty;
    public string GroqModel { get; set; } = "openai/gpt-oss-120b";
    public string NvidiaApiKey { get; set; } = string.Empty;
    public string NvidiaModel { get; set; } = "minimaxai/minimax-m3";
    public string NvidiaUrl { get; set; } = "https://integrate.api.nvidia.com/v1";
    public string OpenRouterApiKey { get; set; } = string.Empty;
    public string OpenRouterModel { get; set; } = "nvidia/nemotron-3-ultra-550b-a55b:free";
    public string OpenRouterUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string GoogleApiKey { get; set; } = string.Empty;
    public string GeminiModel { get; set; } = "gemini-3.7-flash";
}

public class VectorStoreOptions
{
    public const string SectionName = "VectorStore";
    public string ChromaBaseUrl { get; set; } = "http://localhost:8000";
    public string PersistDirectory { get; set; } = string.Empty;
    public string CollectionName { get; set; } = "product_descriptions";
    public string SqliteDbPath { get; set; } = "data/semantic_store.db";
    public bool AutoSyncOnStartup { get; set; } = true;
    public float FtsWeight { get; set; } = 0.5f;
    public float VectorWeight { get; set; } = 0.5f;
    public int VectorDimension { get; set; } = 384;
    public int DefaultTopK { get; set; } = 25;
    public int MaxTopK { get; set; } = 1000;
}
