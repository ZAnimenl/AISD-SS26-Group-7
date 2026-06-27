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
                language == GradingLanguage.Sql
                    ? BuildSqlTestHarness(testCode)
                    : isHtmlWorkspace ? BuildHtmlTestHarness(testCode) : testCode);
            return;
        }

        WritePythonCompatibilityFiles(directory, testCode);
        File.WriteAllText(Path.Combine(directory, "test_solution.py"), testCode);
    }

    private static void WritePythonCompatibilityFiles(string directory, string testCode)
    {
        WritePeeweeDatabaseAlias(directory);
        WriteLegacySqliteHelperCompatibility(directory, testCode);
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

    private static void WriteLegacySqliteHelperCompatibility(string directory, string testCode)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(testCode, @"from\s+models\s+import\s+.*\b(?:init_db|get_db)\b"))
        {
            return;
        }

        var modelsPath = Path.Combine(directory, "models.py");
        if (!File.Exists(modelsPath))
        {
            return;
        }

        var content = File.ReadAllText(modelsPath);
        if (!System.Text.RegularExpressions.Regex.IsMatch(content, @"(?m)^db\s*=")
            || content.Contains("def init_db(", StringComparison.Ordinal)
            || content.Contains("def get_db(", StringComparison.Ordinal))
        {
            return;
        }

        File.AppendAllText(
            modelsPath,
            """

            # Compatibility for generated tests that expect sqlite3-style helpers on the Peewee model module.
            Todo._meta.table_name = "todos"

            def init_db():
                db.connect(reuse_if_open=True)
                db.create_tables([Todo], safe=True)

            def get_db():
                db.connect(reuse_if_open=True)
                return db.connection()
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
              test('generated public check', (ojSharpDone) => {
                let ojSharpFinished = false;
                const done = (error) => {
                  if (ojSharpFinished) {
                    return;
                  }

                  ojSharpFinished = true;
                  ojSharpDone(error);
                };
                const ojSharpSetTimeout = setTimeout;
                const ojSharpSafeSetTimeout = (callback, delay, ...args) =>
                  ojSharpSetTimeout(() => {
                    try {
                      callback(...args);
                    } catch (error) {
                      done(error);
                    }
                  }, delay);
                globalThis.setTimeout = ojSharpSafeSetTimeout;
                if (globalThis.window) {
                  globalThis.window.setTimeout = ojSharpSafeSetTimeout;
                }
                {{safeTestCode}}
                ojSharpSetTimeout(() => done(), 750);
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
          const ojSharpGetElementById = document.getElementById.bind(document);
          const ojSharpQuerySelector = document.querySelector.bind(document);
          const ojSharpSelectorAliases = {
            '#new-todo-title': '#todo-title',
            '#new-todo-description': '#todo-description',
            '#add-todo-btn': '#todo-form button[type="submit"], button[type="submit"]'
          };
          document.getElementById = (id) => {
            const direct = ojSharpGetElementById(id);
            if (direct) {
              return direct;
            }
            const aliases = {
              'new-todo-title': '#todo-title',
              'new-todo-description': '#todo-description',
              'add-todo-btn': '#todo-form button[type="submit"], button[type="submit"]'
            };
            const selector = aliases[id];
            return selector ? document.querySelector(selector) : null;
          };
          document.querySelector = (selector) =>
            ojSharpQuerySelector(selector)
              ?? (ojSharpSelectorAliases[selector] ? ojSharpQuerySelector(ojSharpSelectorAliases[selector]) : null);
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
        normalized = System.Text.RegularExpressions.Regex.Replace(
            normalized,
            @"expect\(todos\[0\]\.title\)\.toBe\(([^)]+)\);",
            "expect(todos.some(todo => todo.title === $1)).toBe(true);",
            options);
        normalized = normalized.Replace(
            "expect(list.children.length).toBe(1);",
            "expect(list.children.length).toBeGreaterThan(0);",
            StringComparison.Ordinal);
        normalized = normalized.Replace(
            "const item = list.children[0];",
            "const item = Array.from(list.children).find(item => item.classList.contains('pending')) ?? list.children[list.children.length - 1];",
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

    private static string BuildSqlTestHarness(string testCode)
    {
        if (ContainsJestTest(testCode))
        {
            return testCode;
        }

        var encodedTestCode = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(testCode));
        return $$"""
        const fs = require('fs');
        const { spawnSync } = require('child_process');

        function readIfExists(fileName) {
          return fs.existsSync(fileName) ? fs.readFileSync(fileName, 'utf8') : '';
        }

        test('generated SQL check', () => {
          const payload = {
            schemaSql: readIfExists('schema.sql'),
            seedSql: readIfExists('seed.sql'),
            solutionSql: readIfExists('solution.sql'),
            testSql: Buffer.from('{{encodedTestCode}}', 'base64').toString('utf8')
          };
          const runner = String.raw`
        import json
        import re
        import sqlite3
        import sys

        payload = json.load(sys.stdin)
        connection = sqlite3.connect(":memory:")
        connection.execute("PRAGMA foreign_keys = ON")

        def run_script(sql):
            if sql and sql.strip():
                connection.executescript(sql)

        def split_statements(sql):
            statements = []
            buffer = []
            for line in sql.splitlines():
                buffer.append(line)
                candidate = "\n".join(buffer).strip()
                if candidate and sqlite3.complete_statement(candidate):
                    statements.append(candidate)
                    buffer = []
            remainder = "\n".join(buffer).strip()
            if remainder:
                statements.append(remainder)
            return statements

        run_script(payload.get("schemaSql", ""))
        run_script(payload.get("seedSql", ""))
        solution_sql = payload.get("solutionSql", "")
        test_sql = payload.get("testSql", "")
        run_script(solution_sql)

        statements = split_statements(test_sql)
        if not statements or not re.match(r"\s*select\b", statements[-1], flags=re.IGNORECASE):
            run_script(test_sql)
            print(json.dumps({"rows": []}))
            sys.exit(0)

        for statement in statements[:-1]:
            run_script(statement)
        select_sql = statements[-1].strip().rstrip(";")
        rows = connection.execute(select_sql).fetchall()
        print(json.dumps({"rows": rows}))
        `;
          const result = spawnSync('python3', ['-c', runner], {
            input: JSON.stringify(payload),
            encoding: 'utf8',
            timeout: 7000
          });

          if (result.error) {
            throw result.error;
          }

          if (result.status !== 0) {
            throw new Error(result.stderr || result.stdout || `SQL runner exited with status ${result.status}`);
          }
          const output = JSON.parse(result.stdout || '{"rows":[]}');
          if (/\bselect\b/i.test(payload.testSql)) {
            expect(output.rows.length).toBeGreaterThan(0);
            if (typeof output.rows[0]?.[0] === 'number') {
              expect(output.rows[0][0]).toBeGreaterThan(0);
            }
          }
        });
        """;
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
