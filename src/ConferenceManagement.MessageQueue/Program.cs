using System.Collections.Concurrent;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddSingleton<QueueStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// POST /queue/{topic} - enqueue a message
app.MapPost("/queue/{topic}", async (string topic, HttpRequest request, QueueStore store) =>
{
    using var reader = new StreamReader(request.Body);
    var message = await reader.ReadToEndAsync();
    if (string.IsNullOrEmpty(message))
        return Results.BadRequest("Message body cannot be empty.");

    store.Enqueue(topic, message);
    return Results.Accepted();
});

// GET /queue/{topic} - dequeue a message (returns 204 if empty)
app.MapGet("/queue/{topic}", (string topic, QueueStore store) =>
{
    if (store.TryDequeue(topic, out var message))
        return Results.Ok(message);
    return Results.NoContent();
});

// GET /queue/{topic}/length - get queue depth
app.MapGet("/queue/{topic}/length", (string topic, QueueStore store) =>
    Results.Ok(new { topic, length = store.Count(topic) }));

app.Run();

/// <summary>
/// Thread-safe in-memory queue store keyed by topic name.
/// </summary>
public class QueueStore
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _queues = new();

    public void Enqueue(string topic, string message)
    {
        var queue = _queues.GetOrAdd(topic, _ => new ConcurrentQueue<string>());
        queue.Enqueue(message);
    }

    public bool TryDequeue(string topic, out string? message)
    {
        if (_queues.TryGetValue(topic, out var queue))
            return queue.TryDequeue(out message);
        message = null;
        return false;
    }

    public int Count(string topic) =>
        _queues.TryGetValue(topic, out var queue) ? queue.Count : 0;
}
