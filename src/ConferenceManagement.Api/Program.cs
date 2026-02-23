using System.Text.Json;
using ConferenceManagement.Api.Data;
using ConferenceManagement.Api.Models;
using ConferenceManagement.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// Database — PostgreSQL event store
var connectionString = builder.Configuration.GetConnectionString("EventStore")
    ?? "Host=postgres;Port=5432;Database=conference_events;Username=postgres;Password=postgres";

builder.Services.AddDbContext<EventStoreDbContext>(opt =>
    opt.UseNpgsql(connectionString));

// HTTP client for the message queue service
var queueUrl = builder.Configuration["MessageQueue:BaseUrl"] ?? "http://queue:8080";
builder.Services.AddHttpClient("MessageQueue", client =>
    client.BaseAddress = new Uri(queueUrl));

builder.Services.AddHttpClient<HttpQueuePublisher>(client =>
    client.BaseAddress = new Uri(queueUrl));

builder.Services.AddScoped<IEventPublisher, HttpQueuePublisher>();
builder.Services.AddHostedService<EventProcessorService>();

var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EventStoreDbContext>();
    db.Database.Migrate();
}

// ── Endpoints ─────────────────────────────────────────────────────────────

var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web);

// POST /api/conferences  — create a new conference
app.MapPost("/api/conferences", async (
    CreateConferenceRequest req,
    IEventPublisher publisher,
    EventStoreDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.Name))
        return Results.BadRequest("Conference name is required.");
    if (req.TotalSeats <= 0)
        return Results.BadRequest("TotalSeats must be greater than zero.");

    var conferenceId = Guid.NewGuid();
    var payload = new ConferenceCreatedPayload(conferenceId, req.Name, req.TotalSeats);

    await publisher.PublishAsync("conference-events", new
    {
        eventType = EventTypes.ConferenceCreated,
        payload
    });

    return Results.Accepted($"/api/conferences/{conferenceId}", new { conferenceId, req.Name, req.TotalSeats });
});

// GET /api/conferences/{id}  — get conference details
app.MapGet("/api/conferences/{id:guid}", async (Guid id, EventStoreDbContext db) =>
{
    var conf = await db.Conferences.FindAsync(id);
    return conf is null ? Results.NotFound() : Results.Ok(conf);
});

// GET /api/conferences  — list all conferences
app.MapGet("/api/conferences", async (EventStoreDbContext db) =>
    Results.Ok(await db.Conferences.ToListAsync()));

// POST /api/conferences/{id}/bookings  — book a seat
app.MapPost("/api/conferences/{id:guid}/bookings", async (
    Guid id,
    BookSeatRequest req,
    IEventPublisher publisher,
    EventStoreDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.AttendeeName))
        return Results.BadRequest("AttendeeName is required.");
    if (string.IsNullOrWhiteSpace(req.AttendeeEmail))
        return Results.BadRequest("AttendeeEmail is required.");

    // Check availability using the materialized view
    var availability = await db.SeatAvailabilities.FindAsync(id);
    if (availability is null)
        return Results.NotFound("Conference not found.");
    if (availability.AvailableSeats <= 0)
        return Results.Conflict("No seats available.");

    var bookingId = Guid.NewGuid();
    var payload = new SeatBookedPayload(bookingId, id, req.AttendeeName, req.AttendeeEmail);

    await publisher.PublishAsync("conference-events", new
    {
        eventType = EventTypes.SeatBooked,
        payload
    });

    return Results.Accepted($"/api/conferences/{id}/bookings/{bookingId}", new { bookingId, conferenceId = id });
});

// DELETE /api/conferences/{id}/bookings/{bookingId}  — cancel a booking
app.MapDelete("/api/conferences/{id:guid}/bookings/{bookingId:guid}", async (
    Guid id,
    Guid bookingId,
    IEventPublisher publisher,
    EventStoreDbContext db) =>
{
    var booking = await db.Bookings.FindAsync(bookingId);
    if (booking is null || booking.ConferenceId != id)
        return Results.NotFound("Booking not found.");
    if (booking.IsCancelled)
        return Results.Conflict("Booking is already cancelled.");

    var payload = new BookingCancelledPayload(bookingId, id);

    await publisher.PublishAsync("conference-events", new
    {
        eventType = EventTypes.BookingCancelled,
        payload
    });

    return Results.Accepted();
});

// GET /api/conferences/{id}/availability  — seat availability (from materialized view)
app.MapGet("/api/conferences/{id:guid}/availability", async (Guid id, EventStoreDbContext db) =>
{
    var availability = await db.SeatAvailabilities.FindAsync(id);
    return availability is null
        ? Results.NotFound()
        : Results.Ok(new
        {
            availability.ConferenceId,
            availability.ConferenceName,
            availability.TotalSeats,
            availability.BookedSeats,
            availability.AvailableSeats,
            availability.UpdatedAt
        });
});

// GET /api/conferences/{id}/bookings  — list bookings for a conference
app.MapGet("/api/conferences/{id:guid}/bookings", async (Guid id, EventStoreDbContext db) =>
    Results.Ok(await db.Bookings.Where(b => b.ConferenceId == id).ToListAsync()));

// GET /api/conferences/{id}/events  — full event history (event replay)
app.MapGet("/api/conferences/{id:guid}/events", async (Guid id, EventStoreDbContext db) =>
    Results.Ok(await db.Events
        .Where(e => e.ConferenceId == id)
        .OrderBy(e => e.SequenceNumber)
        .ToListAsync()));

app.Run();
