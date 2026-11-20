namespace SportClub.Maui.Models;

// Client-side copies of the API DTOs (the MAUI app does not reference the backend projects).

public record UserDto(int Id, string FullName, string Email, string Role, int? TrainerId);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public record TrainingTypeDto(int Id, string Name, string? Description, string Intensity);

public record SessionDto(
    int Id, int TrainingTypeId, string TrainingTypeName, string Intensity, int TrainerId, string TrainerName,
    DateTime StartsAt, int DurationMinutes, string Room, int MaxParticipants, int BookedCount, int FreeSpots, int WaitlistCount);

public record BookingDto(int Id, int SessionId, string TrainingTypeName, string TrainerName, string Room,
    DateTime StartsAt, string Status, bool? Attended, DateTime CreatedAt);

public record WaitlistEntryDto(int Id, int SessionId, string TrainingTypeName, DateTime StartsAt, int Position, DateTime CreatedAt);

public record MonthlyPassDto(int Id, int UserId, string UserName, DateTime ValidFrom, DateTime ValidUntil,
    int VisitsTotal, int VisitsUsed, int VisitsLeft, bool IsActive, string? PlanName, decimal? PricePaid, DateTime? PurchasedAt, bool IsUpcoming);

public record PassPlanDto(int Id, string Name, string? Description, int Visits, int DurationDays, decimal Price, bool IsActive);

public record ParticipantDto(int BookingId, int UserId, string FullName, string Email, bool? Attended);

public record PopularTrainingDto(int TrainingTypeId, string Name, int Sessions, int Bookings, int Attended, double AverageFillPercent);

public record NotificationDto(int Id, int? SessionId, string Type, string Message, DateTime CreatedAt, bool IsRead);

public record ProblemDetailsDto(string? Title, string? Detail, int? Status, Dictionary<string, string[]>? Errors);

public record FreeSpotsChangedMessage(string Type, int SessionId, int Free);
