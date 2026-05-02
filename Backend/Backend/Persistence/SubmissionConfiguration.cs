using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class SubmissionConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Submission>(entity =>
        {
            entity.ToTable("submissions");
            entity.HasKey(submission => submission.Id);
            entity.Property(submission => submission.EvaluationStatus).HasMaxLength(64).IsRequired();
            entity.Property(submission => submission.FilesJson).HasColumnType("jsonb").IsRequired();
            entity.HasOne(submission => submission.Session)
                .WithMany(session => session.Submissions)
                .HasForeignKey(submission => submission.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
