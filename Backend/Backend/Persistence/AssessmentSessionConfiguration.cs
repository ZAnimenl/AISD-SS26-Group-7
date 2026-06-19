using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class AssessmentSessionConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssessmentSession>(entity =>
        {
            entity.ToTable("assessment_sessions");
            entity.HasKey(session => session.Id);
            entity.Property(session => session.Status).HasMaxLength(64).IsRequired();
            entity.Property(session => session.ReflectionText).IsRequired();
            entity.Property(session => session.ReflectionSubmissionReason).HasMaxLength(40);
            entity.Property(session => session.AiGradingStatus).HasMaxLength(40).IsRequired();
            entity.Property(session => session.AiGradingDetailsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(session => session.AiGradingModel).HasMaxLength(120);
            entity.Property(session => session.AiRubricVersion).HasMaxLength(80);
            entity.Property(session => session.AiGradingConfidence).HasMaxLength(40);
            entity.HasIndex(session => new { session.AssessmentId, session.UserId, session.Status });
            entity.HasOne(session => session.Assessment)
                .WithMany(assessment => assessment.Sessions)
                .HasForeignKey(session => session.AssessmentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(session => session.User)
                .WithMany(user => user.Sessions)
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
