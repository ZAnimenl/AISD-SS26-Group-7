using Backend.Domain;
using Backend.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OjSharp.Tests.ApiContractTests;

public sealed class DateTimeOffsetOrderingTests
{
    [Fact]
    public async Task Sqlite_ordering_handles_DateTimeOffset_keys()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<OjSharpDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new OjSharpDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var oldest = Assessment("Oldest", now.AddDays(-2));
        var middle = Assessment("Middle", now.AddDays(-1));
        var newest = Assessment("Newest", now);

        dbContext.Assessments.AddRange(middle, newest, oldest);
        await dbContext.SaveChangesAsync();

        var descending = await DateTimeOffsetOrdering.ToDescendingListAsync(
            dbContext.Assessments,
            dbContext,
            assessment => assessment.CreatedAt,
            CancellationToken.None);
        var ascending = await DateTimeOffsetOrdering.ToAscendingListAsync(
            dbContext.Assessments,
            dbContext,
            assessment => assessment.CreatedAt,
            CancellationToken.None);

        Assert.Equal([newest.Id, middle.Id, oldest.Id], descending.Select(assessment => assessment.Id));
        Assert.Equal([oldest.Id, middle.Id, newest.Id], ascending.Select(assessment => assessment.Id));
    }

    private static Assessment Assessment(string title, DateTimeOffset createdAt)
    {
        return new Assessment
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = $"{title} description",
            DurationMinutes = 60,
            Status = AssessmentStatuses.Draft,
            SharedPrototypeMetadataJson = "{}",
            CreatedAt = createdAt
        };
    }
}
