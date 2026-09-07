using AdhocSystem.Api.Models.Common;

namespace AdhocSystem.Api.Services.Routing;

public interface IIntentRouter
{
    Task<IntentType> RouteAsync(string query, CancellationToken cancellationToken = default);
}
