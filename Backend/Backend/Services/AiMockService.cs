using Backend.Domain;

namespace Backend.Services;

public sealed class AiMockService
{
    private readonly IEnumerable<IAiResponseProvider> _responseProviders;

    public AiMockService(IEnumerable<IAiResponseProvider> responseProviders)
    {
        _responseProviders = responseProviders;
    }

    public async Task<(string ResponseMarkdown, string[] SemanticTags, int InputTokens, int OutputTokens)> GenerateResponseAsync(
        string interactionType,
        string message,
        string selectedLanguage,
        string activeFileContent,
        CancellationToken cancellationToken)
    {
        var tags = DeriveSemanticTags(interactionType, message, activeFileContent);
        var context = new AiGenerationContext(interactionType, message, selectedLanguage, activeFileContent);

        // 1. Attempt using registered providers with token tracking (e.g. Deepseek)
        foreach (var responseProvider in _responseProviders)
        {
            var result = await responseProvider.TryGenerateWithUsageAsync(context, tags, cancellationToken);
            if (result is not null)
            {
                return (result.ResponseMarkdown, tags, result.InputTokens, result.OutputTokens);
            }

            var providerResponse = await responseProvider.TryGenerateAsync(context, tags, cancellationToken);
            if (!string.IsNullOrWhiteSpace(providerResponse))
            {
                // Estimate tokens for providers that do not report usage
                var estInput = (message.Length + activeFileContent.Length) / 4;
                var estOutput = providerResponse.Length / 4;
                return (providerResponse, tags, estInput, estOutput);
            }
        }

        // 2. Fallback to mock responses if no real AI provider is configured
        var response = BuildResponse(interactionType, message, selectedLanguage, activeFileContent);
        var mockInputTokens = (message.Length + activeFileContent.Length) / 4;
        var mockOutputTokens = response.Length / 4;
        return (response, tags, mockInputTokens, mockOutputTokens);
    }

    private static string[] DeriveSemanticTags(string interactionType, string message, string activeFileContent)
    {
        var tags = new List<string>();

        tags.Add(interactionType switch
        {
            AiInteractionTypes.CodeSuggestion => "code_suggestion",
            AiInteractionTypes.Explanation => "explanation",
            AiInteractionTypes.Debugging => "debugging",
            _ => "general_help"
        });

        var combined = (message + " " + activeFileContent).ToLowerInvariant();

        if (combined.Contains("loop") || combined.Contains("for ") || combined.Contains("while "))
            tags.Add("loops");
        if (combined.Contains("recur"))
            tags.Add("recursion");
        if (combined.Contains("api") || combined.Contains("endpoint") || combined.Contains("route"))
            tags.Add("api_design");
        if (combined.Contains("bug") || combined.Contains("fix") || combined.Contains("wrong"))
            tags.Add("bug_fix");
        if (combined.Contains("exception") || combined.Contains("error") || combined.Contains("try"))
            tags.Add("error_handling");
        if (combined.Contains("list") || combined.Contains("array") || combined.Contains("dict"))
            tags.Add("data_structures");
        if (combined.Contains("database") || combined.Contains("query") || combined.Contains("sql"))
            tags.Add("database");
        if (combined.Contains("test") || combined.Contains("assert"))
            tags.Add("testing");

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
            AiInteractionTypes.CodeSuggestion => BuildCodeSuggestionResponse(message, lang, hasCode, activeFileContent),
            AiInteractionTypes.Explanation => BuildExplanationResponse(message, lang, activeFileContent),
            AiInteractionTypes.Debugging => BuildDebuggingResponse(message, lang, hasCode),
            _ => BuildGeneralResponse(message, lang)
        };
    }

    private static string BuildCodeSuggestionResponse(string message, string lang, bool hasCode, string code)
    {
        var lines = new List<string>
        {
            "## Code Suggestion",
            "",
            "Based on your current code and task, here are some suggestions:",
            ""
        };

        if (code.Contains("TODO"))
        {
            lines.Add("I see you have `TODO` comments in your code. Here is how to approach them:");
            lines.Add("");
            lines.Add("1. **Start with the data structure** - Decide how your data will be stored and accessed.");
            lines.Add("2. **Implement one method at a time** - Get the simplest method working first, then build up.");
            lines.Add("3. **Test as you go** - Run the code after each method to verify it works.");
        }
        else
        {
            lines.Add("Looking at your implementation:");
            lines.Add("");
            lines.Add("1. **Check the logic** - Make sure each operation does exactly what the requirements ask.");
            lines.Add("2. **Handle edge cases** - Empty inputs, missing items, and boundary values.");
            lines.Add("3. **Return the correct format** - Match the expected output structure from the task description.");
        }

        if (lang == "python")
        {
            lines.Add("");
            lines.Add("**Python tip:** Use list comprehensions for filtering and dictionary comprehensions for transforming data.");
        }
        else if (lang == "javascript")
        {
            lines.Add("");
            lines.Add("**JS tip:** Use `Array.filter()`, `Array.map()`, and `Array.reduce()` for clean data transformations.");
        }

        lines.Add("");
        lines.Add("> I can help guide your approach, but try implementing it yourself first for the best learning experience.");

        return string.Join("\n", lines);
    }

    private static string BuildExplanationResponse(string message, string lang, string code)
    {
        var lines = new List<string>
        {
            "## Explanation",
            "",
            "Let me break down what this task is asking:",
            "",
            "1. **Read the requirements carefully** - Each numbered requirement maps to a specific piece of logic you need to implement.",
            "2. **Identify inputs and outputs** - What data comes in, and what format should the result be in?",
            "3. **Think about the data flow** - How does the input get transformed step by step into the output?",
            ""
        };

        if (!string.IsNullOrWhiteSpace(code) && code.Contains("class"))
        {
            lines.Add("Your code defines a **class**. Each method in the class should handle one specific responsibility:");
            lines.Add("");
            lines.Add("- Methods that **add** data should create new entries and update internal state.");
            lines.Add("- Methods that **query** data should filter and return without modifying state.");
            lines.Add("- Methods that **modify** data should find the target item and update it.");
        }
        else if (!string.IsNullOrWhiteSpace(code) && (code.Contains("app.") || code.Contains("Flask") || code.Contains("express")))
        {
            lines.Add("Your code is a **web server**. The key concepts are:");
            lines.Add("");
            lines.Add("- **Routes** map URL paths to handler functions.");
            lines.Add("- **Handlers** process the request and return a response.");
            lines.Add("- **JSON responses** should use `jsonify()` in Flask or `res.json()` in Express.");
        }

        lines.Add("");
        lines.Add("> Try to explain the problem back to yourself in plain language before writing code. If you can describe the steps clearly, the code will follow naturally.");

        return string.Join("\n", lines);
    }

    private static string BuildDebuggingResponse(string message, string lang, bool hasCode)
    {
        var steps = new List<string>
        {
            "## Debugging Guide",
            "",
            "Here are steps to find and fix the issue:",
            "",
            "1. **Read the error message** - If there is one, it usually points to the exact line and type of problem.",
            "2. **Check the operator** - A common bug is using `+` instead of `*`, or comparing with `=` instead of `==`.",
            "3. **Check the conditions** - Are your `if` statements filtering correctly? Off-by-one errors are very common.",
            "4. **Check return values** - Make sure every code path returns something, and the return type matches what the caller expects."
        };

        if (hasCode)
        {
            steps.Add("5. **Add print/log statements** - Print intermediate values to see where the actual output diverges from expected.");
            steps.Add("6. **Test with the simplest input** - Use a single-item or empty input to isolate the issue.");
        }

        if (lang == "python")
        {
            steps.Add("");
            steps.Add("**Python tip:** Use `print(f'{variable=}')` to quickly inspect variable names and values.");
        }
        else if (lang is "javascript")
        {
            steps.Add("");
            steps.Add("**JS tip:** Use `console.log(JSON.stringify(data, null, 2))` to pretty-print objects for inspection.");
        }

        steps.Add("");
        steps.Add("> Focus on the first failing test case. Fix that one first, then move to the next.");

        return string.Join("\n", steps);
    }

    private static string BuildGeneralResponse(string message, string lang)
    {
        return string.Join("\n",
        [
            "## AI Agent",
            "",
            "I am your embedded AI assistant for this task. Here is how I can help:",
            "",
            "- **Code suggestion** - I can guide you on how to structure your implementation.",
            "- **Explanation** - I can break down the task requirements and concepts.",
            "- **Debugging** - I can help you find and fix issues in your code.",
            "",
            "Try to describe what you are stuck on, and I will point you in the right direction.",
            "",
            "> Remember: I will guide and explain, but I will not write the complete solution for you. The goal is for you to learn by implementing it yourself."
        ]);
    }
}
