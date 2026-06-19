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

            var objectiveRepetition = CalculateObjectiveRepetition(interactions, events);
            var rapidAcceptDeduction = CalculateRapidAcceptDeduction(events);
            var completion = await completionService.GenerateAsync(
                BuildSystemPrompt(),
                BuildUserPrompt(session, interactions, events, executions, objectiveRepetition, rapidAcceptDeduction),
                AiResponseFormat.Json,
                cancellationToken,
                maxTokens: 1800);
            var grade = ParseGrade(completion.Content, objectiveRepetition, rapidAcceptDeduction);

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
                    rapid_accept_deduction = rapidAcceptDeduction
                }
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
        int objectiveRepetition,
        int rapidAcceptDeduction)
    {
        var payload = new
        {
            reflection = session.ReflectionText,
            objective_repetition_score = objectiveRepetition,
            rapid_accept_deduction = rapidAcceptDeduction,
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

    private static AiUsageGrade ParseGrade(string content, int objectiveRepetition, int rapidAcceptDeduction)
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
            ? JsonSerializer.Deserialize<object[]>(evidenceElement.GetRawText()) ?? []
            : [];

        return new AiUsageGrade(
            score,
            criteria,
            ReadString(root, "summary", "Automatic AI usage grading completed."),
            ReadString(root, "confidence", "medium"),
            ReadString(root, "reflection_consistency", "not_assessed"),
            evidence);
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
