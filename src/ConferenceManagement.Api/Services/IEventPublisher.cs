using System.Text.Json;
using ConferenceManagement.Api.Models;

namespace ConferenceManagement.Api.Services;

/// <summary>
/// Publishes events to the external message queue service via HTTP.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(string topic, object payload, CancellationToken ct = default);
}

public class HttpQueuePublisher(HttpClient httpClient, ILogger<HttpQueuePublisher> logger) : IEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PublishAsync(string topic, object payload, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        try
        {
            var response = await httpClient.PostAsync($"/queue/{topic}", content, ct);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Published event to topic '{Topic}'", topic);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish event to topic '{Topic}'", topic);
            throw;
        }
    }
}
