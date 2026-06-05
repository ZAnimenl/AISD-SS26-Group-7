using Backend.Api;
using Backend.Domain;
using Backend.Persistence;
using Backend.Services;

namespace OjSharp.Tests.ApiContractTests;

public sealed class SessionStartConcurrencyTests
{
    [Fact]
    public void Assessment_attempt_advisory_lock_key_is_stable_and_scoped()
    {
        var assessmentId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var key = DatabaseAdvisoryLocks.GetAssessmentAttemptKey(assessmentId, userId);

        Assert.Equal(key, DatabaseAdvisoryLocks.GetAssessmentAttemptKey(assessmentId, userId));
        Assert.NotEqual(DatabaseAdvisoryLocks.SchemaCompatibility, key);
        Assert.NotEqual(key, DatabaseAdvisoryLocks.GetAssessmentAttemptKey(
            Guid.Parse("11111111-1111-1111-1111-111111111112"),
            userId));
        Assert.NotEqual(key, DatabaseAdvisoryLocks.GetAssessmentAttemptKey(
            assessmentId,
            Guid.Parse("22222222-2222-2222-2222-222222222223")));
    }

    [Fact]
    public void Missing_workspace_states_are_created_in_deterministic_question_order()
    {
        var sessionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var now = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
        var firstQuestionId = Guid.Parse("44444444-4444-4444-4444-444444444441");
        var secondQuestionId = Guid.Parse("44444444-4444-4444-4444-444444444442");
        var existingQuestionId = Guid.Parse("44444444-4444-4444-4444-444444444443");
        var questions = new[]
        {
            Question(secondQuestionId, 20, "javascript", "main.js", "console.log('second');"),
            Question(existingQuestionId, 5, "python", "main.py", "print('existing')"),
            Question(firstQuestionId, 10, "python", "app.py", "print('first')")
        };

        var states = SessionEndpoints.CreateMissingWorkspaceStates(
            sessionId,
            questions,
            new HashSet<Guid> { existingQuestionId },
            now);

        Assert.Equal(new[] { firstQuestionId, secondQuestionId }, states.Select(state => state.QuestionId).ToArray());
        Assert.All(states, state =>
        {
            Assert.Equal(sessionId, state.SessionId);
            Assert.Equal(now, state.LastSavedAt);
            Assert.Equal(1, state.Version);
        });

        Assert.Equal("python", states[0].SelectedLanguage);
        Assert.Equal("app.py", states[0].ActiveFile);
        var firstFiles = JsonDocumentSerializer.Deserialize(states[0].FilesJson, new Dictionary<string, WorkspaceFileDto>());
        Assert.Equal("print('first')", firstFiles["app.py"].Content);

        Assert.Equal("javascript", states[1].SelectedLanguage);
        Assert.Equal("main.js", states[1].ActiveFile);
        var secondFiles = JsonDocumentSerializer.Deserialize(states[1].FilesJson, new Dictionary<string, WorkspaceFileDto>());
        Assert.Equal("console.log('second');", secondFiles["main.js"].Content);
    }

    private static Question Question(Guid id, int sortOrder, string language, string fileName, string content)
    {
        return new Question
        {
            Id = id,
            SortOrder = sortOrder,
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
            {
                [language] = new Dictionary<string, string>
                {
                    [fileName] = content
                }
            })
        };
    }
}
