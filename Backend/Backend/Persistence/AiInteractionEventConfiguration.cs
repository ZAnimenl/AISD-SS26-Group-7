using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class AiInteractionEventConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiInteractionEvent>(entity =>
        {
            entity.ToTable("ai_interaction_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EventType).HasMaxLength(80).IsRequired();
            entity.Property(item => item.MetadataJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => new { item.SessionId, item.CreatedAt });
            entity.HasOne(item => item.Interaction)
                .WithMany()
                .HasForeignKey(item => item.InteractionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
