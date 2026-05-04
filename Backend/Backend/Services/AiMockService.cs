using Backend.Domain;

namespace Backend.Services;

// Mock AI service that generates context-aware responses without calling external LLM providers.
// This is the MVP implementation. Replace with a real provider adapter when an API key is available.
public sealed class AiMockService
{
    public (string ResponseMarkdown, string[] SemanticTags) GenerateResponse(
        string interactionType,
        string message,
        string selectedLanguage,
        string activeFileContent)
    {
        var tags = DeriveSemanticTags(interactionType, message, activeFileContent);
        var response = BuildResponse(interactionType, message, selectedLanguage, activeFileContent);
        return (response, tags);
    }

    private static string[] DeriveSemanticTags(string interactionType, string message, string activeFileContent)
    {
        var tags = new List<string>();

        tags.Add(interactionType switch
        {
            "debug" => "debugging",
            "code_review" => "code_review",
            "explain" => "explanation",
            "hint" => "conceptual_hint",
            _ => "general_help"
        });

        var combined = (message + " " + activeFileContent).ToLowerInvariant();

        if (combined.Contains("loop") || combined.Contains("for ") || combined.Contains("while "))
            tags.Add("loops");
        if (combined.Contains("recur"))
            tags.Add("recursion");
        if (combined.Contains("sort"))
            tags.Add("sorting");
        if (combined.Contains("exception") || combined.Contains("error") || combined.Contains("try"))
            tags.Add("error_handling");
        if (combined.Contains("list") || combined.Contains("array") || combined.Contains("dict"))
            tags.Add("data_structures");
        if (combined.Contains("time") || combined.Contains("complex") || combined.Contains("o(n)"))
            tags.Add("complexity");

        return tags.Distinct().ToArray();
    }

    private static string BuildResponse(
        string interactionType,
        string message,
        string selectedLanguage,
        string activeFileContent)
    {
        var hasCode = !string.IsNullOrWhiteSpace(activeFileContent);
        var lang = selectedLanguage.ToLowerInvariant();

        return interactionType switch
        {
            "debug" => BuildDebugResponse(message, lang, hasCode),
            "code_review" => BuildCodeReviewResponse(message, lang, hasCode),
            "explain" => BuildExplainResponse(message, lang),
            "hint" => BuildHintResponse(message, lang),
            _ => BuildGeneralResponse(message, lang)
        };
    }

    private static string BuildDebugResponse(string message, string lang, bool hasCode)
    {
        var steps = new List<string>
        {
            "## Debugging Guide",
            "",
            "Here are some steps to help you find the issue:",
            "",
            "1. **Check your inputs** — Make sure your function receives the expected types and values.",
            "2. **Trace through manually** — Walk through the sample input step by step and compare with your output.",
            "3. **Check edge cases** — Empty input, single element, negative numbers, or zero are common failure points.",
        };

        if (hasCode)
        {
            steps.Add("4. **Add print statements** — Insert temporary print/console.log calls to inspect variable values at each step.");
            steps.Add("5. **Check return values** — Ensure every branch of your code returns a value.");
        }

        if (lang == "python")
        {
            steps.Add("");
            steps.Add("**Python tip:** Use `print(type(x))` to verify the type of a variable if you suspect a type mismatch.");
        }
        else if (lang == "javascript" || lang == "typescript")
        {
            steps.Add("");
            steps.Add("**JS tip:** Use `console.log(JSON.stringify(x))` to inspect objects and arrays clearly.");
        }

        steps.Add("");
        steps.Add("> Start with the failing sample case and work backwards from the wrong output.");

        return string.Join("\n", steps);
    }

    private static string BuildCodeReviewResponse(string message, string lang, bool hasCode)
    {
        var lines = new List<string>
        {
            "## Code Review Checklist",
            "",
            "Consider the following when reviewing your solution:",
            "",
            "- **Correctness** — Does your solution produce the right output for all sample cases?",
            "- **Edge cases** — Have you handled empty input, single elements, and boundary values?",
            "- **Readability** — Are variable names clear and descriptive?",
            "- **Function signature** — Does it match the required interface in the problem statement?",
        };

        if (lang == "python")
        {
            lines.Add("- **Python style** — Follow PEP 8: use `snake_case` for variables and functions.");
        }
        else if (lang is "javascript" or "typescript")
        {
            lines.Add("- **JS style** — Prefer `const` over `let` where values do not change.");
        }
        else if (lang == "csharp")
        {
            lines.Add("- **C# style** — Use `var` for local variables and `PascalCase` for methods.");
        }

        lines.Add("");
        lines.Add("> A clean, readable solution is easier to debug and maintain.");

        return string.Join("\n", lines);
    }

    private static string BuildExplainResponse(string message, string lang)
    {
        return string.Join("\n",
        [
            "## Understanding the Problem",
            "",
            "Break the problem into three parts:",
            "",
            "1. **Input** — What data does your function receive? What are the types and constraints?",
            "2. **Transformation** — What logic or calculation must you apply to produce the result?",
            "3. **Output** — What format does the answer need to be in?",
            "",
            "Once you have identified these three parts, start by solving the simplest possible version of the problem.",
            "Then extend your solution to handle additional cases.",
            "",
            "> Tip: Re-read the problem statement carefully and highlight the key requirements before writing any code."
        ]);
    }

    private static string BuildHintResponse(string message, string lang)
    {
        return string.Join("\n",
        [
            "## Hint",
            "",
            "Here is a nudge in the right direction without giving away the full solution:",
            "",
            "- Start with the public sample case. Write a function that passes only that one case first.",
            "- Once the sample passes, think about what other inputs might break your solution.",
            "- Consider whether a loop, recursion, or a built-in function is the most natural fit for this problem.",
            "",
            "> Remember: a working simple solution is better than a broken complex one."
        ]);
    }

    private static string BuildGeneralResponse(string message, string lang)
    {
        return string.Join("\n",
        [
            "## AI Assistant",
            "",
            "I am here to help you work through this problem. Here are some general tips:",
            "",
            "- **Read the problem carefully** — Identify the input, the required output, and any constraints.",
            "- **Start small** — Solve the simplest case first, then build up.",
            "- **Test often** — Run your code against the sample cases frequently as you develop.",
            "- **Ask specific questions** — If you are stuck, describe exactly what you tried and what went wrong.",
            "",
            "> Use the interaction types (hint, explain, debug, code_review) for more targeted assistance."
        ]);
    }
}
