using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class TestCaseConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestCase>(entity =>
        {
            entity.ToTable("test_cases");
            entity.HasKey(testCase => testCase.Id);
            entity.Property(testCase => testCase.Name).HasMaxLength(200).IsRequired();
            entity.Property(testCase => testCase.Visibility).HasMaxLength(64).IsRequired();
            entity.Property(testCase => testCase.TestCodeJson).HasColumnName("test_code_json").IsRequired();
            entity.Property(testCase => testCase.AuthoringSource).HasMaxLength(80).IsRequired();
            entity.Property(testCase => testCase.TraceabilityMetadataJson).HasColumnType("jsonb").IsRequired();
            entity.Property(testCase => testCase.PublicMetadataJson).HasColumnType("jsonb").IsRequired();
            entity.Property(testCase => testCase.AdminMetadataJson).HasColumnType("jsonb").IsRequired();
            entity.HasOne(testCase => testCase.Question)
                .WithMany(question => question.TestCases)
                .HasForeignKey(testCase => testCase.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
