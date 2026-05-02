using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class AiInteractionConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiInteraction>(entity =>
        {
            entity.ToTable("ai_interactions");
            entity.HasKey(interaction => interaction.Id);
            entity.Property(interaction => interaction.InteractionType).HasMaxLength(64).IsRequired();
            entity.Property(interaction => interaction.Message).IsRequired();
            entity.Property(interaction => interaction.SelectedLanguage).HasMaxLength(64).IsRequired();
            entity.Property(interaction => interaction.ActiveFileContent).IsRequired();
            entity.Property(interaction => interaction.ResponseMarkdown).IsRequired();
            entity.Property(interaction => interaction.SemanticTagsJson).HasColumnType("jsonb").IsRequired();
        });
    }
}
