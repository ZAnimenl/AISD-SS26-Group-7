using Backend.Api;
using Backend.Domain;
using Backend.Services;

namespace OjSharp.Tests.ApiContractTests;

public sealed class SubmissionFinalizeTests
{
    [Fact]
    public void Finalize_backfills_missing_workspace_states_before_evaluation()
    {
        var session = new AssessmentSession
        {
            Id = Guid.NewGuid(),
            Assessment = new Assessment
            {
                Questions =
                [
                    new Question
                    {
                        Id = Guid.NewGuid(),
                        StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                        {
                            ["python"] = "def solve(value):\n    return value\n"
                        })
                    },
                    new Question
                    {
                        Id = Guid.NewGuid(),
                        StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                        {
                            ["javascript"] = "function solve(value) {\n  return value;\n}\n"
                        })
                    }
                ]
            }
        };

        var addedStates = SubmissionEndpoints.EnsureWorkspaceStates(session, DateTimeOffset.UtcNow);

        Assert.Equal(2, addedStates.Count);
        Assert.Equal(2, session.WorkspaceStates.Count);
        Assert.All(session.Assessment.Questions, question =>
            Assert.Contains(session.WorkspaceStates, state => state.QuestionId == question.Id));
        Assert.Contains(session.WorkspaceStates, state => state.SelectedLanguage == "python" && state.ActiveFile == "main.py");
        Assert.Contains(session.WorkspaceStates, state => state.SelectedLanguage == "javascript" && state.ActiveFile == "main.js");
    }

    [Fact]
    public void Finalize_does_not_replace_existing_workspace_state()
    {
        var questionId = Guid.NewGuid();
        var existingState = new WorkspaceQuestionState
        {
            Id = Guid.NewGuid(),
            QuestionId = questionId,
            SelectedLanguage = "python",
            ActiveFile = "main.py",
            FilesJson = JsonDocumentSerializer.Serialize(new Dictionary<string, WorkspaceFileDto>
            {
                ["main.py"] = new("python", "def solve(value):\n    return value + 1\n")
            })
        };
        var session = new AssessmentSession
        {
            Id = Guid.NewGuid(),
            Assessment = new Assessment
            {
                Questions =
                [
                    new Question
                    {
                        Id = questionId,
                        StarterCodeJson = JsonDocumentSerializer.Serialize(new Dictionary<string, string>
                        {
                            ["python"] = "def solve(value):\n    pass\n"
                        })
                    }
                ]
            },
            WorkspaceStates = [existingState]
        };

        var addedStates = SubmissionEndpoints.EnsureWorkspaceStates(session, DateTimeOffset.UtcNow);

        Assert.Empty(addedStates);
        Assert.Single(session.WorkspaceStates);
        Assert.Same(existingState, session.WorkspaceStates[0]);
    }
}
