namespace ConferenceManagement.Api.Models;

// ── Request DTOs ────────────────────────────────────────────────────────────

public record CreateConferenceRequest(string Name, int TotalSeats);

public record BookSeatRequest(string AttendeeName, string AttendeeEmail);

// ── Event payloads (serialised to JSON and stored in event store) ──────────

public record ConferenceCreatedPayload(Guid ConferenceId, string Name, int TotalSeats);

public record SeatBookedPayload(Guid BookingId, Guid ConferenceId, string AttendeeName, string AttendeeEmail);

public record BookingCancelledPayload(Guid BookingId, Guid ConferenceId);
