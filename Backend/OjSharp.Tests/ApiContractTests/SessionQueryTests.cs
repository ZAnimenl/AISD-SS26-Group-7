using Backend.Domain;
using Backend.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace OjSharp.Tests.ApiContractTests;

public sealed class SessionQueryTests
{
    [Fact]
    public async Task Sqlite_filters_session_expiry_in_memory()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<OjSharpDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new OjSharpDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var now = new DateTimeOffset(2026, 6, 7, 12, 0, 0, TimeSpan.Zero);
        var assessmentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var expired = Session(assessmentId, userId, now.AddMinutes(-1));
        var open = Session(assessmentId, userId, now.AddMinutes(30));

        dbContext.Users.Add(new User
        {
            Id = userId,
            Email = "student@example.com",
            FullName = "Student",
            PasswordHash = "hash",
            Role = UserRoles.Student,
            Status = "active",
            CreatedAt = now
        });
        dbContext.Assessments.Add(new Assessment
        {
            Id = assessmentId,
            Title = "Assessment",
            Description = "Description",
            DurationMinutes = 60,
            Status = AssessmentStatuses.Active,
            SharedPrototypeMetadataJson = "{}",
            CreatedAt = now
        });
        dbContext.AssessmentSessions.AddRange(expired, open);
        await dbContext.SaveChangesAsync();

        var source = dbContext.AssessmentSessions.Where(session =>
            session.AssessmentId == assessmentId
            && session.UserId == userId
            && session.Status == SessionStatuses.Active);

        var expiredSessions = await SessionQueries.ToExpiredListAsync(source, dbContext, now, CancellationToken.None);
        var openSession = await SessionQueries.FirstUnexpiredAsync(source, dbContext, now, CancellationToken.None);

        Assert.Equal(expired.Id, Assert.Single(expiredSessions).Id);
        Assert.Equal(open.Id, openSession?.Id);
    }

    private static AssessmentSession Session(Guid assessmentId, Guid userId, DateTimeOffset expiresAt)
    {
        return new AssessmentSession
        {
            Id = Guid.NewGuid(),
            AssessmentId = assessmentId,
            UserId = userId,
            Status = SessionStatuses.Active,
            StartedAt = expiresAt.AddMinutes(-60),
            ExpiresAt = expiresAt
        };
    }
}
