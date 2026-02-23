namespace ConferenceManagement.Api.Models;

/// <summary>
/// Core conference entity — persisted via the ConferenceCreated event.
/// </summary>
public class Conference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Materialized view that tracks seat availability for a conference.
/// Updated by the event processor whenever a SeatBooked or BookingCancelled event arrives.
/// </summary>
public class SeatAvailability
{
    public Guid ConferenceId { get; set; }
    public required string ConferenceName { get; set; }
    public int TotalSeats { get; set; }
    public int BookedSeats { get; set; }
    public int AvailableSeats => TotalSeats - BookedSeats;
    public long LastEventSequence { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a single booking record (created by the SeatBooked event).
/// </summary>
public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConferenceId { get; set; }
    public required string AttendeeName { get; set; }
    public required string AttendeeEmail { get; set; }
    public DateTime BookedAt { get; set; } = DateTime.UtcNow;
    public bool IsCancelled { get; set; }
    public DateTime? CancelledAt { get; set; }
}
