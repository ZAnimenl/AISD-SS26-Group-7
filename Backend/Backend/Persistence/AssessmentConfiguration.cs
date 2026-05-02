using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class AssessmentConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Assessment>(entity =>
        {
            entity.ToTable("assessments");
            entity.HasKey(assessment => assessment.Id);
            entity.Property(assessment => assessment.Title).HasMaxLength(300).IsRequired();
            entity.Property(assessment => assessment.Description).IsRequired();
            entity.Property(assessment => assessment.Status).HasMaxLength(64).IsRequired();
        });
    }
}
