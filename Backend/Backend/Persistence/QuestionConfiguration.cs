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
            entity.Property(question => question.ProblemDescriptionMarkdown).IsRequired();
            entity.Property(question => question.LanguageConstraintsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(question => question.StarterCodeJson).HasColumnType("jsonb").IsRequired();
            entity.Property(question => question.AdminNotes).HasMaxLength(2000);
            entity.HasOne(question => question.Assessment)
                .WithMany(assessment => assessment.Questions)
                .HasForeignKey(question => question.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
