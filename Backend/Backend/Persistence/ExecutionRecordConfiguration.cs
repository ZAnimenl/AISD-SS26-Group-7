using Backend.Domain;
using Microsoft.EntityFrameworkCore;

namespace Backend.Persistence;

public static class ExecutionRecordConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ExecutionRecord>(entity =>
        {
            entity.ToTable("execution_records");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Status).HasMaxLength(64).IsRequired();
            entity.Property(record => record.TestResultsJson).HasColumnType("jsonb").IsRequired();
            entity.Property(record => record.MetricsJson).HasColumnType("jsonb").IsRequired();
        });
    }
}
