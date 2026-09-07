using AdhocSystem.Api.Models.Common;
using AdhocSystem.Api.Services.Audio;
using AdhocSystem.Api.Services.Database;
using AdhocSystem.Api.Services.Llm;
using AdhocSystem.Api.Services.NL2SQL;
using AdhocSystem.Api.Services.Routing;
using AdhocSystem.Api.Services.Security;
using AdhocSystem.Api.Services.Semantic;
using AdhocSystem.Api.Services.Session;

var builder = WebApplication.CreateBuilder(args);

// Strongly-typed Configurations
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<LlmOptions>(builder.Configuration.GetSection(LlmOptions.SectionName));
builder.Services.Configure<VectorStoreOptions>(builder.Configuration.GetSection(VectorStoreOptions.SectionName));

// Add HttpClient factory for LLM clients, Semantic search, and STT
builder.Services.AddHttpClient<GroqChatClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<OpenRouterChatClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<NvidiaNimChatClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<GeminiChatClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddHttpClient<SpeechToTextService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
});

// Register Multi-Provider Chat Clients into Circuit Breaker
builder.Services.AddTransient<IChatClient, GroqChatClient>();
builder.Services.AddTransient<IChatClient, GeminiChatClient>();
builder.Services.AddTransient<IChatClient, OpenRouterChatClient>();
builder.Services.AddTransient<IChatClient, NvidiaNimChatClient>();
builder.Services.AddSingleton<ICircuitBreakerLlm, CircuitBreakerLlm>();

// Register Domain & Infrastructure Services
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<ISchemaIntrospector, SchemaIntrospector>();
builder.Services.AddSingleton<ISqlGuardService, SqlGuardService>();
builder.Services.AddSingleton<ISessionManager, SessionManager>();
builder.Services.AddSingleton<ILocalEmbeddingGenerator, LocalEmbeddingGenerator>();
builder.Services.AddSingleton<ISqliteSemanticStore, SqliteSemanticStore>();
builder.Services.AddHostedService<SemanticSyncBackgroundService>();
builder.Services.AddSingleton<QueryDecomposer>();
builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();
builder.Services.AddScoped<ISpeechToTextService, SpeechToTextService>();
builder.Services.AddScoped<IIntentRouter, IntentRouter>();
builder.Services.AddScoped<INl2SqlService, Nl2SqlService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
    });

// CORS for local frontend clients
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Advanced RAG System (SQL-based) .NET API",
        Version = "v1",
        Description = "Production-grade NL2SQL & Semantic Retrieval Engine in ASP.NET Core 7.0 with ScriptDom AST Security & Polly Circuit Breaker"
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// Serve Swagger in both Development and Production for interactive testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AdvRAG .NET API v1");
    c.RoutePrefix = "swagger";
});

// Serve static frontend files if present
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program { }
