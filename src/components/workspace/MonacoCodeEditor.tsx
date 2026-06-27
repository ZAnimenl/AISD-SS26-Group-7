"use client";

import dynamic from "next/dynamic";
import { useEffect, useRef } from "react";
import type { BeforeMount, OnChange, OnMount } from "@monaco-editor/react";
import type { Language } from "@/lib/types";

const Editor = dynamic(() => import("@monaco-editor/react").then((module) => module.default), {
  ssr: false,
  loading: () => (
    <div className="grid h-full min-h-0 place-items-center rounded-xl border border-white/10 bg-[#080b14] font-mono text-sm text-white/45">
      Loading editor...
    </div>
  )
});

interface MonacoCodeEditorProps {
  assessmentId: string;
  questionId: string;
  fileName: string;
  language: Language;
  value: string;
  onChange: (value: string, eventTimestamp: number) => void;
  readOnly?: boolean;
}

const MONACO_LANGUAGE_BY_WORKSPACE_LANGUAGE: Record<Language, string> = {
  python: "python",
  javascript: "javascript",
  typescript: "typescript",
  html: "html",
  sql: "sqlite"
};

function getMonacoLanguage(fileName: string, language: Language) {
  const extension = fileName.split(".").pop()?.toLowerCase();
  switch (extension) {
    case "html":
      return "html";
    case "css":
      return "css";
    case "js":
    case "jsx":
      return "javascript";
    case "ts":
    case "tsx":
      return "typescript";
    case "sql":
      return "sqlite";
    case "py":
      return "python";
    case "json":
      return "json";
    default:
      return MONACO_LANGUAGE_BY_WORKSPACE_LANGUAGE[language];
  }
}

const SQLITE_LANGUAGE_ID = "sqlite";
let sqliteLanguageRegistered = false;

const configureMonaco: BeforeMount = (monaco) => {
  if (sqliteLanguageRegistered) {
    return;
  }

  monaco.languages.register({ id: SQLITE_LANGUAGE_ID });
  monaco.languages.setLanguageConfiguration(SQLITE_LANGUAGE_ID, {
    comments: { lineComment: "--", blockComment: ["/*", "*/"] },
    brackets: [["(", ")"], ["BEGIN", "END"]],
    autoClosingPairs: [
      { open: "'", close: "'" },
      { open: "\"", close: "\"" },
      { open: "(", close: ")" }
    ],
    surroundingPairs: [
      { open: "'", close: "'" },
      { open: "\"", close: "\"" },
      { open: "(", close: ")" }
    ]
  });
  monaco.languages.setMonarchTokensProvider(SQLITE_LANGUAGE_ID, {
    defaultToken: "",
    tokenPostfix: ".sqlite",
    ignoreCase: true,
    keywords: [
      "abort", "action", "add", "after", "all", "alter", "and", "as", "asc", "autoincrement",
      "before", "begin", "between", "by", "cascade", "case", "check", "collate", "column",
      "commit", "conflict", "constraint", "create", "cross", "current_date", "current_time",
      "current_timestamp", "database", "default", "deferrable", "deferred", "delete", "desc",
      "distinct", "drop", "each", "else", "end", "escape", "except", "exists", "explain",
      "fail", "for", "foreign", "from", "full", "glob", "group", "having", "if", "ignore",
      "immediate", "in", "index", "indexed", "initially", "inner", "insert", "instead",
      "intersect", "into", "is", "isnull", "join", "key", "left", "like", "limit", "match",
      "natural", "no", "not", "notnull", "null", "of", "offset", "on", "or", "order",
      "outer", "plan", "pragma", "primary", "query", "raise", "recursive", "references",
      "regexp", "reindex", "release", "rename", "replace", "restrict", "right", "rollback",
      "row", "savepoint", "select", "set", "table", "temp", "temporary", "then", "to",
      "transaction", "trigger", "union", "unique", "update", "using", "vacuum", "values",
      "view", "virtual", "when", "where", "with", "without"
    ],
    builtinFunctions: [
      "abs", "changes", "char", "coalesce", "count", "date", "datetime", "hex", "ifnull",
      "instr", "json", "json_extract", "last_insert_rowid", "length", "like", "lower",
      "max", "min", "nullif", "printf", "quote", "random", "replace", "round", "strftime",
      "substr", "sum", "time", "total", "trim", "typeof", "upper"
    ],
    tokenizer: {
      root: [
        [/--.*$/, "comment"],
        [/\/\*/, "comment", "@comment"],
        [/'([^']|'')*'/, "string"],
        [/"([^"]|"")*"/, "string"],
        [/\b\d+(\.\d+)?\b/, "number"],
        [/[;,.]/, "delimiter"],
        [/[()]/, "@brackets"],
        [/[a-zA-Z_][\w$]*/, {
          cases: {
            "@keywords": "keyword",
            "@builtinFunctions": "predefined",
            "@default": "identifier"
          }
        }],
        [/[<>=!]+/, "operator"]
      ],
      comment: [
        [/[^\/*]+/, "comment"],
        [/\*\//, "comment", "@pop"],
        [/[\/*]/, "comment"]
      ]
    }
  });
  sqliteLanguageRegistered = true;
};

function encodeUriPathSegment(value: string) {
  return encodeURIComponent(value).replace(/%2F/gi, "/");
}

function getModelPath(assessmentId: string, questionId: string, fileName: string) {
  return [
    "ojsharp://workspace",
    encodeUriPathSegment(assessmentId),
    encodeUriPathSegment(questionId),
    encodeUriPathSegment(fileName)
  ].join("/");
}

export function MonacoCodeEditor({ assessmentId, questionId, fileName, language, value, onChange, readOnly = false }: MonacoCodeEditorProps) {
  const modelPath = getModelPath(assessmentId, questionId, fileName);
  const editorRef = useRef<Parameters<OnMount>[0] | null>(null);

  const handleEditorChange: OnChange = (nextValue) => {
    onChange(nextValue ?? "", performance.now());
  };

  const handleEditorMount: OnMount = (editor) => {
    editorRef.current = editor;
    editor.focus();
  };

  useEffect(() => {
    const model = editorRef.current?.getModel();
    if (!model) {
      return;
    }

    if (model.getValue() !== value) {
      model.setValue(value);
    }
  }, [modelPath, value]);

  return (
    <div className="h-full min-h-0 overflow-hidden rounded-xl border border-white/10 bg-[#080b14]">
      <Editor
        beforeMount={configureMonaco}
        height="100%"
        path={modelPath}
        language={getMonacoLanguage(fileName, language)}
        theme="vs-dark"
        value={value}
        onChange={handleEditorChange}
        onMount={handleEditorMount}
        saveViewState
        options={{
          automaticLayout: true,
          fontFamily: "'JetBrains Mono', Consolas, 'Courier New', monospace",
          fontLigatures: true,
          fontSize: 14,
          lineHeight: 22,
          minimap: { enabled: false },
          padding: { top: 16, bottom: 16 },
          renderLineHighlight: "gutter",
          scrollBeyondLastLine: false,
          smoothScrolling: true,
          tabSize: 2,
          wordWrap: "on",
          readOnly
        }}
      />
    </div>
  );
}

// Inline completion is intentionally omitted until the backend supports /api/v1/ai/inline-completion.
