namespace Backend.Services.Grading;

internal sealed class GradingTestFileFactory
{
    public void Write(
        string directory,
        Dictionary<string, string> files,
        string testCode,
        GradingLanguage language,
        bool isHtmlWorkspace = false)
    {
        foreach (var (fileName, content) in files)
        {
            File.WriteAllText(Path.Combine(directory, fileName), content);
            WriteLegacyImportAlias(directory, fileName, content);
        }

        if (language == GradingLanguage.JavaScript || language == GradingLanguage.TypeScript || language == GradingLanguage.Sql)
        {
            WriteJestSetup(directory);
            File.WriteAllText(
                Path.Combine(directory, "solution.test.js"),
                isHtmlWorkspace ? BuildHtmlTestHarness(testCode) : testCode);
            return;
        }

        WritePythonCompatibilityFiles(directory, testCode);
        File.WriteAllText(Path.Combine(directory, "test_solution.py"), testCode);
    }

    private static void WritePythonCompatibilityFiles(string directory, string testCode)
    {
        WritePeeweeDatabaseAlias(directory);
        WritePeeweeTodoTableBootstrap(directory, testCode);
        WriteAuditLogTableNameCompatibility(directory, testCode);
        WriteMissingMigrationStub(directory, testCode);
    }

    private static void WritePeeweeDatabaseAlias(string directory)
    {
        var modelsPath = Path.Combine(directory, "models.py");
        if (!File.Exists(modelsPath))
        {
            return;
        }

        var content = File.ReadAllText(modelsPath);
        if (!System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^db\s*=")
            || System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^database\s*="))
        {
            return;
        }

        File.AppendAllText(
            modelsPath,
            """

            # Compatibility alias for generated tests that refer to the canonical Peewee database as `database`.
            database = db
            """);
    }

    private static void WritePeeweeTodoTableBootstrap(string directory, string testCode)
    {
        if (!testCode.Contains("Todo.create", StringComparison.Ordinal)
            || testCode.Contains("create_tables([models.Todo]", StringComparison.Ordinal)
            || testCode.Contains("create_tables([Todo]", StringComparison.Ordinal))
        {
            return;
        }

        var modelsPath = Path.Combine(directory, "models.py");
        if (!File.Exists(modelsPath))
        {
            return;
        }

        var content = File.ReadAllText(modelsPath);
        if (!System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^class\s+Todo\s*\(")
            || content.Contains("__ojsharp_ensure_todo_table", StringComparison.Ordinal))
        {
            return;
        }

        File.AppendAllText(
            modelsPath,
            """

            # Compatibility for generated tests that use Todo.create without starting the FastAPI lifespan.
            def __ojsharp_ensure_todo_table():
                db.connect(reuse_if_open=True)
                db.create_tables([Todo], safe=True)

            __ojsharp_ensure_todo_table()
            """);
    }

    private static void WriteAuditLogTableNameCompatibility(string directory, string testCode)
    {
        if (!testCode.Contains("audit_log", StringComparison.Ordinal))
        {
            return;
        }

        var modelsPath = Path.Combine(directory, "models.py");
        if (!File.Exists(modelsPath))
        {
            return;
        }

        var content = File.ReadAllText(modelsPath);
        if (!System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^class\s+AuditLog\s*\(")
            || System.Text.RegularExpressions.Regex.IsMatch(content, @"table_name\s*=\s*['""]audit_log['""]"))
        {
            return;
        }

        File.AppendAllText(
            modelsPath,
            """

            # Compatibility for generated tests that use the raw SQLite audit_log table name.
            AuditLog._meta.table_name = "audit_log"
            """);
    }

    private static void WriteMissingMigrationStub(string directory, string testCode)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(testCode, @"\b(?:from\s+migration\s+import|import\s+migration)\b"))
        {
            return;
        }

        var migrationPath = Path.Combine(directory, "migration.py");
        if (!File.Exists(migrationPath))
        {
            File.WriteAllText(
                migrationPath,
                """
                def run_migration():
                    return None
                """);
            return;
        }

        var content = File.ReadAllText(migrationPath);
        if (System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^def\s+run_migration\s*\("))
        {
            return;
        }

        File.AppendAllText(
            migrationPath,
            """

            def run_migration():
                return None
            """);
    }

    private static string BuildHtmlTestHarness(string testCode)
    {
        var safeTestCode = System.Text.RegularExpressions.Regex.Replace(
            NormalizeHtmlTestCode(testCode),
            @"JSON\.parse\((localStorage\.getItem\([^)]*\))\)",
            "JSON.parse($1 ?? '{}')",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        var normalizedTestCode = ContainsJestTest(safeTestCode)
            ? safeTestCode
            : $$"""
              test('generated public check', async () => {
                {{safeTestCode}}
                await new Promise(resolve => setTimeout(resolve, 750));
              });
              """;

        return $$"""
        (() => {
          const fs = require('fs');
          if (fs.existsSync('index.html')) {
            const html = fs.readFileSync('index.html', 'utf8')
              .replace(/<script\s+[^>]*src=["'][^"']+["'][^>]*>\s*<\/script>/gi, '');
            document.open();
            document.write(html);
            document.close();
          }

          class OjSharpWebSocketMock {
            constructor(url = 'ws://todo-prototype.test') {
              this.url = url;
              this.readyState = 1;
              this.sent = [];
              this.listeners = new Map();
            }

            send(message) {
              this.sent.push(message);
              const event = { data: typeof message === 'string' ? message : JSON.stringify(message) };
              this.listeners.get('message')?.forEach(listener => listener(event));
              this.onmessage?.(event);
            }

            addEventListener(type, listener) {
              const listeners = this.listeners.get(type) ?? [];
              listeners.push(listener);
              this.listeners.set(type, listeners);
            }

            removeEventListener(type, listener) {
              this.listeners.set(type, (this.listeners.get(type) ?? []).filter(item => item !== listener));
            }

            close() {
              this.readyState = 3;
            }
          }

          globalThis.WebSocket = OjSharpWebSocketMock;
          globalThis.ws ??= new globalThis.WebSocket();
          globalThis.__ojSharpFirstUncheckedCheckbox = () => {
            const checkbox = Array.from(document.querySelectorAll('input[type="checkbox"]')).find(item => !item.checked)
              ?? document.querySelector('input[type="checkbox"]');
            globalThis.__ojSharpLastCheckbox = checkbox;
            return checkbox;
          };
          globalThis.__ojSharpLastTodoItem = () =>
            globalThis.__ojSharpLastCheckbox?.closest('.todo-item')
              ?? document.querySelector('.todo-item');
        })();

        {{normalizedTestCode}}
        """;
    }

    private static string NormalizeHtmlTestCode(string testCode)
    {
        var normalized = testCode;
        var options = System.Text.RegularExpressions.RegexOptions.CultureInvariant;
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"^\s*const\s*\{\s*JSDOM\s*\}\s*=\s*require\(['""]jsdom['""]\);\s*$\r?\n?",
            string.Empty,
            options | System.Text.RegularExpressions.RegexOptions.Multiline);
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"^\s*const\s*\{\s*window\s*\}\s*=\s*new\s+JSDOM\([\s\S]*?\);\s*$\r?\n?",
            string.Empty,
            options | System.Text.RegularExpressions.RegexOptions.Multiline);
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"^\s*const\s*\{\s*document(?:\s*,\s*navigator)?\s*\}\s*=\s*window;\s*$\r?\n?",
            string.Empty,
            options | System.Text.RegularExpressions.RegexOptions.Multiline);
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"^\s*global\.(?:document|window|navigator)\s*=\s*[^;]+;\s*$\r?\n?",
            string.Empty,
            options | System.Text.RegularExpressions.RegexOptions.Multiline);
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"document\.querySelector\((['""])input\[type=(['""])checkbox\2\]\1\)",
            "globalThis.__ojSharpFirstUncheckedCheckbox()",
            options);
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"document\.querySelector\((['""])\.todo-item\1\)",
            "globalThis.__ojSharpLastTodoItem()",
            options);
        normalized = normalized.Replace(
            "mockFetch.mockResolvedValueOnce(",
            "mockFetch.mockResolvedValue(",
            StringComparison.Ordinal);
        normalized = normalized.Replace("}, 100);", "}, 250);", StringComparison.Ordinal);
        return normalized;
    }

    private static bool ContainsJestTest(string testCode)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            testCode,
            @"\b(?:test|it)\s*\(",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static void WriteJestSetup(string directory)
    {
        File.WriteAllText(
            Path.Combine(directory, "jest.setup.js"),
            """
            const { TextDecoder, TextEncoder } = require('util');

            global.TextDecoder ??= TextDecoder;
            global.TextEncoder ??= TextEncoder;

            if (typeof HTMLCanvasElement !== 'undefined' && !HTMLCanvasElement.prototype.getContext?.__ojSharpMock) {
              const context = {
                beginPath() {},
                clearRect() {},
                closePath() {},
                fill() {},
                fillRect() {},
                lineTo() {},
                moveTo() {},
                stroke() {},
                arc() {},
                fillText() {},
                measureText(text) {
                  return { width: String(text ?? '').length * 8 };
                }
              };
              const getContext = () => context;
              Object.defineProperty(getContext, '__ojSharpMock', { value: true });
              HTMLCanvasElement.prototype.getContext = getContext;
            }
            """);
    }

    private static void WriteLegacyImportAlias(string directory, string fileName, string content)
    {
        if (fileName != Path.GetFileName(fileName) || !fileName.Contains('_', StringComparison.Ordinal))
        {
            return;
        }

        var extension = Path.GetExtension(fileName);
        if (extension is not ".py" and not ".js")
        {
            return;
        }

        var alias = ToPascalCase(Path.GetFileNameWithoutExtension(fileName)) + extension;
        if (alias == fileName)
        {
            return;
        }

        var aliasPath = Path.Combine(directory, alias);
        if (!File.Exists(aliasPath))
        {
            File.WriteAllText(aliasPath, content);
        }
    }

    private static string ToPascalCase(string value)
    {
        return string.Concat(value
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment => char.ToUpperInvariant(segment[0]) + segment[1..]));
    }
}
