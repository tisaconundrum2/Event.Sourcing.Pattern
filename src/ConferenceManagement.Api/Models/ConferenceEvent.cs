namespace ConferenceManagement.Api.Models;

/// <summary>
/// Represents an immutable event stored in the append-only event store.
/// </summary>
public class ConferenceEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConferenceId { get; set; }
    public required string EventType { get; set; }
    public required string Payload { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public long SequenceNumber { get; set; }
}

public static class EventTypes
{
    public const string ConferenceCreated = "ConferenceCreated";
    public const string SeatBooked = "SeatBooked";
    public const string BookingCancelled = "BookingCancelled";
}
