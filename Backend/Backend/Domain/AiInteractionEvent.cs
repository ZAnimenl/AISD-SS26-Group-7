namespace Backend.Domain;

public sealed class AiInteractionEvent
{
    public Guid Id { get; set; }

    public Guid InteractionId { get; set; }

    public AiInteraction? Interaction { get; set; }

    public Guid SessionId { get; set; }

    public string EventType { get; set; } = string.Empty;

    public int? ElapsedMilliseconds { get; set; }

    public bool AppliedUnchanged { get; set; }

    public string MetadataJson { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
}
