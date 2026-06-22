namespace Backend.Services;

public sealed class CanonicalPrototypeSource
{
    private static readonly IReadOnlyDictionary<string, string[]> FilesByLanguage =
        new Dictionary<string, string[]>
        {
            ["html"] = ["frontend/index.html", "frontend/styles.css", "frontend/app.js"],
            ["python"] =
            [
                "backend/main.py",
                "backend/models.py",
                "backend/repositories.py",
                "backend/services.py",
                "backend/controllers.py",
                "backend/schemas.py",
                "backend/config/environment.py"
            ],
            ["sql"] = ["database/schema.sql", "database/seed.sql", "database/solution.sql"]
        };

    private readonly string prototypeRoot;

    public CanonicalPrototypeSource()
        : this(Path.Combine(AppContext.BaseDirectory, "assessmentPrototype"))
    {
    }

    internal CanonicalPrototypeSource(string prototypeRoot)
    {
        this.prototypeRoot = prototypeRoot;
    }

    public Dictionary<string, Dictionary<string, string>> ApplyCanonicalFiles(
        IReadOnlyDictionary<string, Dictionary<string, string>> generatedStarterCode,
        IReadOnlyCollection<string> languages)
    {
        var merged = generatedStarterCode.ToDictionary(
            language => language.Key,
            language => new Dictionary<string, string>(language.Value));

        foreach (var language in languages)
        {
            var canonicalFiles = LoadLanguage(language);
            if (canonicalFiles.Count == 0)
            {
                continue;
            }

            if (!merged.TryGetValue(language, out var files))
            {
                files = new Dictionary<string, string>();
                merged[language] = files;
            }

            foreach (var canonicalFile in canonicalFiles)
            {
                files[canonicalFile.Key] = canonicalFile.Value;
            }
        }

        return merged;
    }

    internal Dictionary<string, string> LoadLanguage(string language)
    {
        if (!FilesByLanguage.TryGetValue(language, out var relativePaths))
        {
            return [];
        }

        if (!Directory.Exists(prototypeRoot))
        {
            throw new AiDraftGenerationException(
                "The canonical Todo starter prototype is unavailable. Assessment generation cannot continue safely.");
        }

        return relativePaths.ToDictionary(
            relativePath => Path.GetFileName(relativePath),
            relativePath =>
            {
                var fullPath = Path.Combine(prototypeRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath))
                {
                    throw new AiDraftGenerationException(
                        $"The canonical Todo starter file '{relativePath}' is unavailable.");
                }

                return File.ReadAllText(fullPath);
            });
    }
}
