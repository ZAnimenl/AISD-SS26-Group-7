"use client";

import dynamic from "next/dynamic";
import { useEffect, useRef } from "react";
import type { OnChange, OnMount } from "@monaco-editor/react";
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
}

const MONACO_LANGUAGE_BY_WORKSPACE_LANGUAGE: Record<Language, string> = {
  python: "python",
  javascript: "javascript",
  typescript: "typescript",
  html: "html",
  sql: "sql"
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
      return "sql";
    case "py":
      return "python";
    case "json":
      return "json";
    default:
      return MONACO_LANGUAGE_BY_WORKSPACE_LANGUAGE[language];
  }
}

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

export function MonacoCodeEditor({ assessmentId, questionId, fileName, language, value, onChange }: MonacoCodeEditorProps) {
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
          wordWrap: "on"
        }}
      />
    </div>
  );
}

// Inline completion is intentionally omitted until the backend supports /api/v1/ai/inline-completion.
