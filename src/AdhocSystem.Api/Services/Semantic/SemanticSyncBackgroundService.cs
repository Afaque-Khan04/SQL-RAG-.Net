namespace AdhocSystem.Api.Services.Semantic;

public class SemanticSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SemanticSyncBackgroundService> _logger;

    public SemanticSyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SemanticSyncBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SemanticSyncBackgroundService starting up. Initializing SQLite Semantic Store...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ISqliteSemanticStore>();
            await store.InitializeAsync(stoppingToken);
            _logger.LogInformation("SemanticSyncBackgroundService completed store initialization.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SemanticSyncBackgroundService encountered an error while initializing semantic store.");
        }
    }
}
