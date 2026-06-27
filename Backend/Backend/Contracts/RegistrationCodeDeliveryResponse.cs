namespace Backend.Contracts;

public sealed record RegistrationCodeDeliveryResponse(
    bool Sent,
    DateTimeOffset ExpiresAt,
    string? VerificationCode);
