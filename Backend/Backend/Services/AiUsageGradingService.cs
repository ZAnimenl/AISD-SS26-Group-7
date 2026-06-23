using System.Text.Json;
using Backend.Domain;
using Backend.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services;

public sealed record AiUsageCriteria(
    int PromptQualityAndContext,
    int BehavioralEfficiency,
    int ObjectiveRepetition,
    int CriticalEvaluationAndAdaptation,
    int ReflectionQualityAndConsistency);

public sealed record AiUsageGrade(
    int Score,
    AiUsageCriteria Criteria,
    string Summary,
    string Confidence,
    string ReflectionConsistency,
    IReadOnlyList<object> Evidence);

public sealed record ProblemStatementCopyEvidence(Guid InteractionId, Guid QuestionId, string Reason);

public sealed record TaskAiUsageBenchmarkEvidence(
    Guid QuestionId,
    TaskAiUsageBenchmark Benchmark,
    int TotalTokens,
    int InteractionCount,
    string[] ProvidedContextSignals);

public sealed class AiUsageGradingService(
    OjSharpDbContext dbContext,
    AiCompletionService completionService,
    ILogger<AiUsageGradingService> logger)
{
    public const string RubricVersion = "ai-usage-v1";

    public async Task GradeAsync(AssessmentSession session, CancellationToken cancellationToken)
    {
        session.AiGradingStatus = AiGradingStatuses.Pending;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var interactions = await dbContext.AiInteractions
                .Where(item => item.SessionId == session.Id)
                .ToListAsync(cancellationToken);
            var events = await dbContext.AiInteractionEvents
                .Where(item => item.SessionId == session.Id)
                .ToListAsync(cancellationToken);
            var executions = await dbContext.ExecutionRecords
                .Where(item => item.SessionId == session.Id)
                .ToListAsync(cancellationToken);
            interactions = interactions.OrderBy(item => item.CreatedAt).ToList();
            events = events.OrderBy(item => item.CreatedAt).ToList();
            executions = executions.OrderBy(item => item.CreatedAt).ToList();
            var taskDefinitions = await dbContext.Questions
                .Where(item => item.AssessmentId == session.AssessmentId)
                .Select(item => new
                {
                    item.Id,
                    item.TaskType,
                    item.Difficulty,
                    item.GradingConfigurationJson,
                    item.ProblemDescriptionMarkdown
                })
                .ToListAsync(cancellationToken);

            var objectiveRepetition = CalculateObjectiveRepetition(interactions, events);
            var rapidAcceptDeduction = CalculateRapidAcceptDeduction(events);
            var problemCopy = DetectProblemStatementCopy(
                interactions,
                taskDefinitions.ToDictionary(item => item.Id, item => item.ProblemDescriptionMarkdown));
            var taskBenchmarks = taskDefinitions.ToDictionary(
                item => item.Id,
                item => TaskAiUsageBenchmarkFactory.Read(
                    item.GradingConfigurationJson,
                    item.TaskType,
                    item.Difficulty));
            var benchmarkEvidence = BuildBenchmarkEvidence(interactions, taskBenchmarks);
            var completion = await completionService.GenerateAsync(
                BuildSystemPrompt(),
                BuildUserPrompt(session, interactions, events, executions, benchmarkEvidence, objectiveRepetition, rapidAcceptDeduction),
                AiResponseFormat.Json,
                cancellationToken,
                maxTokens: 1800);
            var grade = ApplyProblemStatementCopyCap(
                ParseGrade(completion.Content, objectiveRepetition, rapidAcceptDeduction),
                problemCopy);

            session.AiUsageScore = grade.Score;
            session.AiGradingStatus = AiGradingStatuses.Completed;
            session.AiGradingModel = "configured-ai-grading-provider";
            session.AiRubricVersion = RubricVersion;
            session.AiGradingSummary = grade.Summary;
            session.AiGradingConfidence = grade.Confidence;
            session.AiGradingDetailsJson = JsonDocumentSerializer.Serialize(new
            {
                rubric_version = RubricVersion,
                grade.Score,
                criteria = grade.Criteria,
                reflection_consistency = grade.ReflectionConsistency,
                grade.Confidence,
                grade.Summary,
                grade.Evidence,
                objective_metrics = new
                {
                    objective_repetition_score = objectiveRepetition,
                    rapid_accept_deduction = rapidAcceptDeduction,
                    problem_statement_copy_detected = problemCopy is not null,
                    prompt_quality_cap_reason = problemCopy?.Reason,
                    task_benchmarks = benchmarkEvidence
                },
            });
            session.AiGradedAt = DateTimeOffset.UtcNow;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Automatic AI usage grading failed for session {SessionId}.", session.Id);
            session.AiGradingStatus = AiGradingStatuses.Failed;
            session.AiGradingSummary = exception.Message;
            session.AiRubricVersion = RubricVersion;
            session.AiUsageScore = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public static int CountWords(string value)
    {
        return value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    internal static int CalculateRapidAcceptDeduction(IEnumerable<AiInteractionEvent> events)
    {
        var eventList = events.ToList();
        return Math.Min(8, eventList.Count(item =>
            item.EventType == AiInteractionEventTypes.Apply
            && item.AppliedUnchanged
            && item.ElapsedMilliseconds is >= 0 and <= 3000
            && !eventList.Any(later =>
                later.InteractionId == item.InteractionId
                && later.CreatedAt >= item.CreatedAt
                && later.CreatedAt <= item.CreatedAt.AddMinutes(1)
                && later.EventType is AiInteractionEventTypes.Edit or AiInteractionEventTypes.Undo)));
    }

    internal static int CalculateObjectiveRepetition(
        IReadOnlyList<AiInteraction> interactions,
        IReadOnlyList<AiInteractionEvent> events)
    {
        if (interactions.Count < 2)
        {
            return 10;
        }

        var deductions = 0;
        for (var index = 1; index < interactions.Count; index += 1)
        {
            var previous = interactions[index - 1];
            var current = interactions[index];
            var interveningAction = events.Any(item =>
                item.CreatedAt > previous.CreatedAt
                && item.CreatedAt < current.CreatedAt
                && item.EventType is AiInteractionEventTypes.Apply
                    or AiInteractionEventTypes.Edit
                    or AiInteractionEventTypes.Reject
                    or AiInteractionEventTypes.Dismiss
                    or AiInteractionEventTypes.Undo);

            if (!interveningAction && Similarity(previous.Message, current.Message) >= 0.8)
            {
                deductions = Math.Min(4, deductions + 2);
            }
            else if (!interveningAction)
            {
                deductions = Math.Min(7, deductions + 1);
            }
        }

        return Math.Max(0, 10 - deductions);
    }

    internal static IReadOnlyList<TaskAiUsageBenchmarkEvidence> BuildBenchmarkEvidence(
        IEnumerable<AiInteraction> interactions,
        IReadOnlyDictionary<Guid, TaskAiUsageBenchmark> benchmarks)
    {
        var byQuestion = interactions.GroupBy(interaction => interaction.QuestionId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        return benchmarks
            .OrderBy(item => item.Key)
            .Select(item =>
            {
                var taskInteractions = byQuestion.GetValueOrDefault(item.Key, []);
                return new TaskAiUsageBenchmarkEvidence(
                    item.Key,
                    item.Value,
                    taskInteractions.Sum(interaction => interaction.TotalTokens),
                    taskInteractions.Length,
                    DetectProvidedContextSignals(taskInteractions));
            })
            .ToArray();
    }

    private static string[] DetectProvidedContextSignals(IEnumerable<AiInteraction> interactions)
    {
        var interactionList = interactions.ToArray();
        var prompts = string.Join("\n", interactionList.Select(interaction => interaction.Message)).ToLowerInvariant();
        var signals = new List<string>();
        if (interactionList.Any(interaction => CountWords(interaction.Message) >= 5))
            signals.Add("task_goal");
        if (interactionList.Any(interaction => !string.IsNullOrWhiteSpace(interaction.ActiveFileContent)))
            signals.Add("active_file_or_code_context");
        if (ContainsAny(prompts, "error", "fail", "test", "expected", "actual", "stdout", "stderr"))
            signals.Add("observed_behavior_or_test_output");
        if (ContainsAny(prompts, "must", "should", "preserve", "require", "acceptance", "constraint", "edge case", "validation"))
            signals.Add("desired_constraint_or_acceptance_condition");
        return signals.ToArray();
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        return terms.Any(term => value.Contains(term, StringComparison.Ordinal));
    }

    private static double Similarity(string first, string second)
    {
        var firstTerms = Terms(first);
        var secondTerms = Terms(second);
        if (firstTerms.Count == 0 || secondTerms.Count == 0)
        {
            return 0;
        }

        var intersection = firstTerms.Intersect(secondTerms).Count();
        var union = firstTerms.Union(secondTerms).Count();
        return union == 0 ? 0 : intersection / (double)union;
    }

    private static HashSet<string> Terms(string value)
    {
        return value
            .ToLowerInvariant()
            .Split([' ', '\r', '\n', '\t', '.', ',', ':', ';', '(', ')', '[', ']', '{', '}'], StringSplitOptions.RemoveEmptyEntries)
            .Where(term => term.Length > 2)
            .ToHashSet();
    }

    private static string BuildSystemPrompt()
    {
        return """
        You grade how a student used an embedded AI assistant during a coding assessment.
        Return one valid JSON object only.
        Use only the supplied attempt evidence. Do not reward low token counts by themselves and do not use a fixed token threshold or cohort comparison.
        Score these semantic criteria:
        - prompt_quality_and_context: integer 0-30
        - behavioral_efficiency: integer 0-30
        - critical_evaluation_before_deduction: integer 0-20
        - reflection_quality_and_consistency: integer 0-10
        The platform supplies objective_repetition_score (0-10) and rapid_accept_deduction (0-8). Do not alter them.
        Task benchmarks contain a reference token budget, interaction count, and required context signals. Use them to assess whether each task received sufficient useful context and proportionate token use. Do not reward low token counts by themselves or apply a hard cap.
        Evaluate productive progress, use of context, refinement, response-to-action-to-test sequences, critical adaptation, and whether the reflection agrees with the logs.
        Include concise criterion-level evidence referencing interaction_id or execution_id.
        Required JSON fields: prompt_quality_and_context, behavioral_efficiency, critical_evaluation_before_deduction, reflection_quality_and_consistency, reflection_consistency, confidence, summary, evidence.
        """;
    }

    private static string BuildUserPrompt(
        AssessmentSession session,
        IReadOnlyList<AiInteraction> interactions,
        IReadOnlyList<AiInteractionEvent> events,
        IReadOnlyList<ExecutionRecord> executions,
        IReadOnlyList<TaskAiUsageBenchmarkEvidence> benchmarkEvidence,
        int objectiveRepetition,
        int rapidAcceptDeduction)
    {
        var payload = new
        {
            reflection = session.ReflectionText,
            objective_repetition_score = objectiveRepetition,
            rapid_accept_deduction = rapidAcceptDeduction,
            task_benchmarks = benchmarkEvidence,
            interactions = interactions.Select(item => new
            {
                interaction_id = item.Id,
                item.QuestionId,
                item.InteractionType,
                prompt = Truncate(item.Message, 1800),
                response = Truncate(item.ResponseMarkdown, 2400),
                semantic_tags = JsonDocumentSerializer.Deserialize(item.SemanticTagsJson, Array.Empty<string>()),
                item.InputTokens,
                item.OutputTokens,
                item.TotalTokens,
                item.CreatedAt
            }),
            events = events.Select(item => new
            {
                event_id = item.Id,
                interaction_id = item.InteractionId,
                item.EventType,
                item.ElapsedMilliseconds,
                item.AppliedUnchanged,
                item.CreatedAt
            }),
            executions = executions.Select(item => new
            {
                execution_id = item.Id,
                item.QuestionId,
                item.Status,
                stdout = Truncate(item.Stdout ?? string.Empty, 800),
                stderr = Truncate(item.Stderr ?? string.Empty, 800),
                item.CreatedAt
            })
        };

        return JsonDocumentSerializer.Serialize(payload);
    }

    internal static AiUsageGrade ParseGrade(string content, int objectiveRepetition, int rapidAcceptDeduction)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        var prompt = ReadScore(root, "prompt_quality_and_context", 30);
        var efficiency = ReadScore(root, "behavioral_efficiency", 30);
        var criticalBeforeDeduction = ReadScore(root, "critical_evaluation_before_deduction", 20);
        var reflection = ReadScore(root, "reflection_quality_and_consistency", 10);
        var critical = Math.Max(0, criticalBeforeDeduction - rapidAcceptDeduction);
        var criteria = new AiUsageCriteria(prompt, efficiency, objectiveRepetition, critical, reflection);
        var score = prompt + efficiency + objectiveRepetition + critical + reflection;
        var evidence = root.TryGetProperty("evidence", out var evidenceElement)
            ? NormalizeEvidence(evidenceElement)
            : [];

        return new AiUsageGrade(
            score,
            criteria,
            ReadString(root, "summary", "Automatic AI usage grading completed."),
            ReadString(root, "confidence", "medium"),
            ReadString(root, "reflection_consistency", "not_assessed"),
            evidence);
    }

    internal static IReadOnlyList<object> NormalizeEvidence(JsonElement evidence)
    {
        try
        {
            return evidence.ValueKind switch
            {
                JsonValueKind.Array => evidence.EnumerateArray().Select(item => (object)item.Clone()).ToArray(),
                JsonValueKind.Object => [evidence.Clone()],
                JsonValueKind.String when !string.IsNullOrWhiteSpace(evidence.GetString()) => [evidence.GetString()!],
                _ => []
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal static ProblemStatementCopyEvidence? DetectProblemStatementCopy(
        IReadOnlyList<AiInteraction> interactions,
        IReadOnlyDictionary<Guid, string> problemStatements)
    {
        foreach (var interaction in interactions)
        {
            if (!problemStatements.TryGetValue(interaction.QuestionId, out var statement))
            {
                continue;
            }

            var normalizedStatement = NormalizeComparableText(statement);
            var normalizedPrompt = NormalizeComparableText(interaction.Message);
            if (normalizedStatement.Length >= 60 && normalizedPrompt.Contains(normalizedStatement, StringComparison.Ordinal))
            {
                return new ProblemStatementCopyEvidence(
                    interaction.Id,
                    interaction.QuestionId,
                    "The problem statement was copied into an AI prompt with only formatting or whitespace changes; Prompt quality and context is capped at 15/30.");
            }
        }

        return null;
    }

    private static AiUsageGrade ApplyProblemStatementCopyCap(
        AiUsageGrade grade,
        ProblemStatementCopyEvidence? copyEvidence)
    {
        if (copyEvidence is null)
        {
            return grade;
        }

        var criteria = grade.Criteria with
        {
            PromptQualityAndContext = Math.Min(15, grade.Criteria.PromptQualityAndContext)
        };
        var evidence = grade.Evidence.Concat([
            (object)new Dictionary<string, object>
            {
                ["criterion"] = "prompt_quality_and_context",
                ["reason"] = copyEvidence.Reason,
                ["interaction_id"] = copyEvidence.InteractionId,
                ["question_id"] = copyEvidence.QuestionId
            }
        ]).ToArray();
        return grade with
        {
            Criteria = criteria,
            Score = criteria.PromptQualityAndContext
                + criteria.BehavioralEfficiency
                + criteria.ObjectiveRepetition
                + criteria.CriticalEvaluationAndAdaptation
                + criteria.ReflectionQualityAndConsistency,
            Evidence = evidence
        };
    }

    private static string NormalizeComparableText(string value)
    {
        var characters = value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .Aggregate(new List<char>(), (characters, character) =>
            {
                if (character != ' ' || characters.Count == 0 || characters[^1] != ' ')
                {
                    characters.Add(character);
                }
                return characters;
            });
        return new string(characters.ToArray()).Trim();
    }

    private static int ReadScore(JsonElement root, string property, int maximum)
    {
        if (!root.TryGetProperty(property, out var value) || !value.TryGetInt32(out var score))
        {
            throw new JsonException($"AI grading response is missing '{property}'.");
        }

        return Math.Clamp(score, 0, maximum);
    }

    private static string ReadString(JsonElement root, string property, string fallback)
    {
        return root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static string Truncate(string value, int maximum)
    {
        return value.Length <= maximum ? value : value[..maximum] + "...";
    }
}
