using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class QuestionConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Question>(entity =>
        {
            entity.ToTable("questions");
            entity.HasKey(question => question.Id);
            entity.Property(question => question.Title).HasMaxLength(300).IsRequired();
            entity.Property(question => question.TaskType).HasMaxLength(80).IsRequired();
            entity.Property(question => question.Difficulty).HasMaxLength(40).IsRequired();
            entity.Property(question => question.VerificationMode).HasMaxLength(80).IsRequired();
            entity.Property(question => question.StarterPrototypeReference).HasMaxLength(200);
            entity.Property(question => question.ProblemDescriptionMarkdown).IsRequired();
            entity.Property(question => question.LanguageConstraintsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(question => question.StarterCodeJson).HasColumnType("jsonb").IsRequired();
            entity.Property(question => question.StarterFilesMetadataJson).HasColumnType("jsonb").IsRequired();
            entity.Property(question => question.VerificationMetadataJson).HasColumnType("jsonb").IsRequired();
            entity.Property(question => question.GradingConfigurationJson).HasColumnType("jsonb").IsRequired();
            entity.Property(question => question.AuthoringSource).HasMaxLength(80).IsRequired();
            entity.Property(question => question.TraceabilityMetadataJson).HasColumnType("jsonb").IsRequired();
            entity.Property(question => question.AdminNotes).HasMaxLength(2000);
            entity.HasOne(question => question.Assessment)
                .WithMany(assessment => assessment.Questions)
                .HasForeignKey(question => question.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
