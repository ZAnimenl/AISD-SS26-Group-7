"use client";

import dynamic from "next/dynamic";
import type { OnChange, OnMount } from "@monaco-editor/react";
import type { Language } from "@/lib/types";

const Editor = dynamic(() => import("@monaco-editor/react").then((module) => module.default), {
  ssr: false,
  loading: () => (
    <div className="grid h-full min-h-[320px] place-items-center rounded-xl border border-white/10 bg-[#080b14] font-mono text-sm text-white/45">
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
  onChange: (value: string) => void;
}

const MONACO_LANGUAGE_BY_WORKSPACE_LANGUAGE: Record<Language, string> = {
  python: "python",
  javascript: "javascript"
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

export function MonacoCodeEditor({ assessmentId, questionId, fileName, language, value, onChange }: MonacoCodeEditorProps) {
  const modelPath = getModelPath(assessmentId, questionId, fileName);

  const handleEditorChange: OnChange = (nextValue) => {
    onChange(nextValue ?? "");
  };

  const handleEditorMount: OnMount = (editor) => {
    editor.focus();
  };

  return (
    <div className="h-full min-h-[320px] overflow-hidden rounded-xl border border-white/10 bg-[#080b14]">
      <Editor
        height="100%"
        path={modelPath}
        language={MONACO_LANGUAGE_BY_WORKSPACE_LANGUAGE[language]}
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
