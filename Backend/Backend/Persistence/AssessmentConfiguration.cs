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
            entity.Property(assessment => assessment.StartsAt);
            entity.Property(assessment => assessment.SharedPrototypeReference).HasMaxLength(200);
            entity.Property(assessment => assessment.SharedPrototypeVersion).HasMaxLength(80);
            entity.Property(assessment => assessment.SharedPrototypeMetadataJson).HasColumnType("jsonb").IsRequired();
        });
    }
}
