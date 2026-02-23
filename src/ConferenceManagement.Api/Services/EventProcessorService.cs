using System.Text.Json;
using ConferenceManagement.Api.Data;
using ConferenceManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ConferenceManagement.Api.Services;

/// <summary>
/// Background service that polls the message queue and processes events:
///   1. Persists each event to the append-only event store (PostgreSQL).
///   2. Updates the materialized view (seat_availability / bookings tables).
/// </summary>
public class EventProcessorService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    ILogger<EventProcessorService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string Topic = "conference-events";
    private const int PollIntervalMs = 500;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EventProcessorService started");
        var httpClient = httpClientFactory.CreateClient("MessageQueue");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var response = await httpClient.GetAsync($"/queue/{Topic}", stoppingToken);

                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    // Queue is empty — back off briefly
                    await Task.Delay(PollIntervalMs, stoppingToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync(stoppingToken);
                if (string.IsNullOrWhiteSpace(body))
                {
                    await Task.Delay(PollIntervalMs, stoppingToken);
                    continue;
                }

                await ProcessMessageAsync(body, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error polling message queue");
                await Task.Delay(2000, stoppingToken);
            }
        }

        logger.LogInformation("EventProcessorService stopped");
    }

    private async Task ProcessMessageAsync(string messageJson, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();

        // Messages are envelopes: { "eventType": "...", "payload": { ... } }
        using var doc = JsonDocument.Parse(messageJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("eventType", out var typeProp))
        {
            logger.LogWarning("Received message without eventType: {Message}", messageJson);
            return;
        }

        var eventType = typeProp.GetString() ?? string.Empty;
        var payloadElement = root.GetProperty("payload");
        var payloadJson = payloadElement.GetRawText();

        switch (eventType)
        {
            case EventTypes.ConferenceCreated:
                await HandleConferenceCreatedAsync(db, payloadJson, ct);
                break;

            case EventTypes.SeatBooked:
                await HandleSeatBookedAsync(db, payloadJson, ct);
                break;

            case EventTypes.BookingCancelled:
                await HandleBookingCancelledAsync(db, payloadJson, ct);
                break;

            default:
                logger.LogWarning("Unknown event type: {EventType}", eventType);
                break;
        }
    }

    private static async Task HandleConferenceCreatedAsync(EventStoreDbContext db, string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<ConferenceCreatedPayload>(payloadJson, JsonOptions)!;

        // Persist event
        var evt = new ConferenceEvent
        {
            ConferenceId = payload.ConferenceId,
            EventType = EventTypes.ConferenceCreated,
            Payload = payloadJson
        };
        db.Events.Add(evt);

        // Persist conference entity
        if (!await db.Conferences.AnyAsync(c => c.Id == payload.ConferenceId, ct))
        {
            db.Conferences.Add(new Conference
            {
                Id = payload.ConferenceId,
                Name = payload.Name,
                TotalSeats = payload.TotalSeats
            });
        }

        // Initialise materialized view
        if (!await db.SeatAvailabilities.AnyAsync(s => s.ConferenceId == payload.ConferenceId, ct))
        {
            db.SeatAvailabilities.Add(new SeatAvailability
            {
                ConferenceId = payload.ConferenceId,
                ConferenceName = payload.Name,
                TotalSeats = payload.TotalSeats,
                BookedSeats = 0
            });
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task HandleSeatBookedAsync(EventStoreDbContext db, string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<SeatBookedPayload>(payloadJson, JsonOptions)!;

        // Persist event
        db.Events.Add(new ConferenceEvent
        {
            ConferenceId = payload.ConferenceId,
            EventType = EventTypes.SeatBooked,
            Payload = payloadJson
        });

        // Persist booking
        db.Bookings.Add(new Booking
        {
            Id = payload.BookingId,
            ConferenceId = payload.ConferenceId,
            AttendeeName = payload.AttendeeName,
            AttendeeEmail = payload.AttendeeEmail
        });

        // Update materialized view
        var availability = await db.SeatAvailabilities.FindAsync([payload.ConferenceId], ct);
        if (availability is not null)
        {
            availability.BookedSeats++;
            availability.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task HandleBookingCancelledAsync(EventStoreDbContext db, string payloadJson, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<BookingCancelledPayload>(payloadJson, JsonOptions)!;

        // Persist event
        db.Events.Add(new ConferenceEvent
        {
            ConferenceId = payload.ConferenceId,
            EventType = EventTypes.BookingCancelled,
            Payload = payloadJson
        });

        // Update booking record
        var booking = await db.Bookings.FindAsync([payload.BookingId], ct);
        if (booking is not null)
        {
            booking.IsCancelled = true;
            booking.CancelledAt = DateTime.UtcNow;
        }

        // Update materialized view
        var availability = await db.SeatAvailabilities.FindAsync([payload.ConferenceId], ct);
        if (availability is not null && availability.BookedSeats > 0)
        {
            availability.BookedSeats--;
            availability.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }
}
