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
            entity.HasOne(testCase => testCase.Question)
                .WithMany(question => question.TestCases)
                .HasForeignKey(testCase => testCase.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
