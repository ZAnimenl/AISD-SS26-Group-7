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
    public void Expired_attempt_blocks_starting_another_attempt()
    {
        Assert.True(SessionEndpoints.HasExpiredAttempt([
            SessionStatuses.Submitted,
            SessionStatuses.Expired
        ]));

        Assert.False(SessionEndpoints.HasExpiredAttempt([
            SessionStatuses.Submitted,
            SessionStatuses.Closed
        ]));
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

    [Fact]
    public void Missing_workspace_states_prefer_question_language_constraints_over_python_starter()
    {
        var sessionId = Guid.Parse("33333333-3333-3333-3333-333333333334");
        var now = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
        var questionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var question = new Question
        {
            Id = questionId,
            SortOrder = 1,
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
            {
                ["python"] = new()
                {
                    ["main.py"] = "def solve():\n    pass\n"
                },
                ["javascript"] = new()
                {
                    ["main.js"] = "function solve() {\n  return true;\n}\n"
                }
            })
        };

        var states = SessionEndpoints.CreateMissingWorkspaceStates(
            sessionId,
            [question],
            new HashSet<Guid>(),
            now);

        var state = Assert.Single(states);
        Assert.Equal(questionId, state.QuestionId);
        Assert.Equal("javascript", state.SelectedLanguage);
        Assert.Equal("main.js", state.ActiveFile);
        var files = JsonDocumentSerializer.Deserialize(state.FilesJson, new Dictionary<string, WorkspaceFileDto>());
        Assert.Single(files);
        Assert.Equal("javascript", files["main.js"].Language);
        Assert.Contains("function solve", files["main.js"].Content);
    }

    [Fact]
    public void Missing_workspace_states_create_empty_allowed_file_when_starter_language_is_not_allowed()
    {
        var sessionId = Guid.Parse("33333333-3333-3333-3333-333333333335");
        var now = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
        var question = new Question
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444445"),
            SortOrder = 1,
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
            {
                ["python"] = new()
                {
                    ["main.py"] = "def solve():\n    pass\n"
                }
            })
        };

        var state = Assert.Single(SessionEndpoints.CreateMissingWorkspaceStates(
            sessionId,
            [question],
            new HashSet<Guid>(),
            now));

        Assert.Equal("javascript", state.SelectedLanguage);
        Assert.Equal("main.js", state.ActiveFile);
        var files = JsonDocumentSerializer.Deserialize(state.FilesJson, new Dictionary<string, WorkspaceFileDto>());
        Assert.Single(files);
        Assert.Equal("javascript", files["main.js"].Language);
        Assert.Equal(string.Empty, files["main.js"].Content);
    }

    [Fact]
    public void Frontend_workspace_uses_html_language_with_legacy_javascript_starter_files()
    {
        var sessionId = Guid.Parse("33333333-3333-3333-3333-333333333336");
        var now = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
        var question = new Question
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444446"),
            SortOrder = 1,
            TaskType = TaskTypes.FrontendUiExtension,
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
            {
                ["javascript"] = new()
                {
                    ["index.html"] = "<main id=\"app\"></main>",
                    ["app.js"] = "document.querySelector('#app');"
                }
            })
        };

        var state = Assert.Single(SessionEndpoints.CreateMissingWorkspaceStates(
            sessionId,
            [question],
            new HashSet<Guid>(),
            now));

        Assert.Equal("html", state.SelectedLanguage);
        Assert.Equal("index.html", state.ActiveFile);
        var files = JsonDocumentSerializer.Deserialize(state.FilesJson, new Dictionary<string, WorkspaceFileDto>());
        Assert.Equal("html", files["index.html"].Language);
        Assert.Equal("html", files["app.js"].Language);
        Assert.Contains("app", files["index.html"].Content);
    }

    [Fact]
    public void Database_workspace_defaults_to_sql_language()
    {
        var sessionId = Guid.Parse("33333333-3333-3333-3333-333333333337");
        var now = new DateTimeOffset(2026, 6, 5, 12, 0, 0, TimeSpan.Zero);
        var question = new Question
        {
            Id = Guid.Parse("44444444-4444-4444-4444-444444444447"),
            SortOrder = 1,
            TaskType = TaskTypes.DatabaseQuerySchema,
            LanguageConstraintsJson = JsonDocumentSerializer.Serialize(new[] { "python", "javascript" }),
            StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>())
        };

        var state = Assert.Single(SessionEndpoints.CreateMissingWorkspaceStates(
            sessionId,
            [question],
            new HashSet<Guid>(),
            now));

        Assert.Equal("sql", state.SelectedLanguage);
        Assert.Equal("solution.sql", state.ActiveFile);
        var files = JsonDocumentSerializer.Deserialize(state.FilesJson, new Dictionary<string, WorkspaceFileDto>());
        Assert.Single(files);
        Assert.Equal("sql", files["solution.sql"].Language);
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
