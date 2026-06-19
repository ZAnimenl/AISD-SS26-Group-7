import type { Language, TaskType } from "@/lib/types";

export const STUDENT_LANGUAGE_OPTIONS: Array<{ value: Language; label: string }> = [
  { value: "python", label: "Python" },
  { value: "javascript", label: "JavaScript" },
  { value: "typescript", label: "TypeScript" },
  { value: "html", label: "HTML" },
  { value: "sql", label: "SQL" }
];

export const DEFAULT_STUDENT_LANGUAGES: Language[] = ["python", "javascript"];

const SUPPORTED_STUDENT_LANGUAGE_VALUES = new Set<Language>(STUDENT_LANGUAGE_OPTIONS.map((option) => option.value));

export const defaultStarterCode: Record<Language, Record<string, string>> = {
  python: {
    "solution.py": "from services import TodoService\n\n\ndef solve(command=None):\n    return TodoService().execute(command)\n",
    "models.py": "from dataclasses import dataclass\n\n\n@dataclass\nclass Todo:\n    title: str\n    completed: bool = False\n    priority: str = \"medium\"\n",
    "services.py": "class TodoService:\n    def execute(self, command=None):\n        raise NotImplementedError(\"Implement the Todo behavior\")\n"
  },
  javascript: {
    "solution.js": "const { TodoService } = require(\"./services.js\");\n\nfunction solve(command) {\n  return new TodoService().execute(command);\n}\n\nmodule.exports = { solve };\n",
    "models.js": "function createTodo(title, overrides = {}) {\n  return { title, completed: false, priority: \"medium\", ...overrides };\n}\n\nmodule.exports = { createTodo };\n",
    "services.js": "class TodoService {\n  execute(_command) {\n    throw new Error(\"Implement the Todo behavior\");\n  }\n}\n\nmodule.exports = { TodoService };\n"
  },
  typescript: {
    "solution.ts": "import { TodoService } from \"./services\";\n\nexport function solve(command: unknown): unknown {\n  return new TodoService().execute(command);\n}\n",
    "types.ts": "export interface Todo {\n  title: string;\n  completed: boolean;\n  priority: \"low\" | \"medium\" | \"high\";\n}\n",
    "services.ts": "export class TodoService {\n  execute(_command: unknown): unknown {\n    throw new Error(\"Implement the Todo behavior\");\n  }\n}\n"
  },
  html: {
    "index.html": "<!DOCTYPE html>\n<html>\n  <head>\n    <title>Todo List</title>\n    <link rel=\"stylesheet\" href=\"styles.css\" />\n  </head>\n  <body>\n    <main id=\"app\" aria-label=\"Todo application\"></main>\n    <script src=\"app.js\"></script>\n  </body>\n</html>\n",
    "styles.css": "/* Extend the responsive Todo application interface. */\n",
    "app.js": "export function initializeTodoApp() {\n  // Implement the Todo prototype behavior.\n}\n\ninitializeTodoApp();\n"
  },
  sql: {
    "schema.sql": "-- Define the supplied relational schema and constraints.\n",
    "seed.sql": "-- Insert representative data, including edge cases.\n",
    "solution.sql": "-- Implement the required queries or migration here.\n"
  }
};

export const defaultTestCode: Record<Language, string> = {
  python: "from solution import solve\n\n\ndef test_solution_exists():\n    assert callable(solve)\n",
  javascript: "const { solve } = require(\"./solution.js\");\n\ntest(\"solution exists\", () => {\n  expect(typeof solve).toBe(\"function\");\n});\n",
  typescript: "const solve = globalThis.__ojsharpSolve;\n\ntest(\"solution exists\", () => {\n  expect(typeof solve).toBe(\"function\");\n});\n",
  html: "const fs = require(\"fs\");\n\ntest(\"index.html exists\", () => {\n  expect(fs.readFileSync(\"index.html\", \"utf8\")).toMatch(/<html|<body|<main|<div/i);\n});\n",
  sql: "const fs = require(\"fs\");\n\ntest(\"solution.sql exists\", () => {\n  expect(fs.readFileSync(\"solution.sql\", \"utf8\").trim().length).toBeGreaterThan(0);\n});\n"
};

export function normalizeTestCode(testCode?: Partial<Record<Language, string>> | null): Record<Language, string> {
  return {
    python: testCode?.python ?? defaultTestCode.python,
    javascript: testCode?.javascript ?? defaultTestCode.javascript,
    typescript: testCode?.typescript ?? defaultTestCode.typescript,
    html: testCode?.html ?? testCode?.javascript ?? defaultTestCode.html,
    sql: testCode?.sql ?? defaultTestCode.sql
  };
}

export function isSupportedStudentLanguage(value: unknown): value is Language {
  return typeof value === "string" && SUPPORTED_STUDENT_LANGUAGE_VALUES.has(value as Language);
}

export function normalizeLanguageValue(value: unknown, fallback: Language = "python"): Language {
  return tryNormalizeLanguageValue(value) ?? fallback;
}

function tryNormalizeLanguageValue(value: unknown): Language | null {
  if (typeof value !== "string") {
    return null;
  }

  const normalized = value.trim().toLowerCase();
  if (normalized === "js") return "javascript";
  if (normalized === "ts") return "typescript";
  if (normalized === "py") return "python";
  if (isSupportedStudentLanguage(normalized)) return normalized;
  return null;
}

export function getLanguageLabel(language: Language) {
  return STUDENT_LANGUAGE_OPTIONS.find((option) => option.value === language)?.label ?? language;
}

export function getDefaultFileNameForLanguage(language: Language) {
  switch (language) {
    case "javascript":
      return "main.js";
    case "typescript":
      return "main.ts";
    case "html":
      return "index.html";
    case "sql":
      return "solution.sql";
    case "python":
    default:
      return "main.py";
  }
}

export function getDefaultLanguagesForTaskType(taskType?: TaskType): Language[] {
  switch (taskType) {
    case "frontend_ui_extension":
      return ["html"];
    case "database_query_schema":
      return ["sql"];
    case "rest_api_development":
    case "bug_fix":
    default:
      return DEFAULT_STUDENT_LANGUAGES;
  }
}

export function normalizeStudentLanguageConstraints(value: unknown, taskType?: TaskType): Language[] {
  if (!Array.isArray(value)) {
    return getDefaultLanguagesForTaskType(taskType);
  }

  const languages = value
    .map(tryNormalizeLanguageValue)
    .filter((item): item is Language => item !== null);
  const uniqueLanguages = languages.length ? Array.from(new Set(languages)) : getDefaultLanguagesForTaskType(taskType);

  if (taskType === "frontend_ui_extension" && !uniqueLanguages.includes("html")) {
    return ["html"];
  }

  if (taskType === "database_query_schema" && !uniqueLanguages.includes("sql")) {
    return ["sql"];
  }

  return uniqueLanguages;
}
