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
