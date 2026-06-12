import type React from "react";
import type { Language } from "@/lib/types";

export function renderMarkdown(text: string) {
  if (!text) return null;

  const elements: React.ReactNode[] = [];
  const lines = text.split("\n");
  let inCodeBlock = false;
  let codeBlockLines: string[] = [];
  let codeBlockLang = "";

  const parseBoldAndInlineCode = (str: string): React.ReactNode => {
    const boldParts = str.split("**");
    return boldParts.map((boldPart, boldIndex) => {
      const isBold = boldIndex % 2 === 1;
      const codeParts = boldPart.split("`");
      const renderedCodeParts = codeParts.map((codePart, codeIndex) => {
        if (codeIndex % 2 === 1) {
          return (
            <code key={`code-${boldIndex}-${codeIndex}`} className="rounded bg-white/10 px-1.5 py-0.5 font-mono text-[11px] text-cyanGlow">
              {codePart}
            </code>
          );
        }
        return codePart;
      });

      if (isBold) {
        return <strong key={`bold-${boldIndex}`} className="font-semibold text-white">{renderedCodeParts}</strong>;
      }
      return <span key={`text-${boldIndex}`}>{renderedCodeParts}</span>;
    });
  };

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];
    const trimmed = line.trim();

    if (trimmed.startsWith("```")) {
      if (inCodeBlock) {
        const codeContent = codeBlockLines.join("\n");
        elements.push(
          <div key={`code-${index}`} className="my-3 overflow-hidden rounded-xl border border-white/10 bg-black/40 font-mono text-xs">
            {codeBlockLang && (
              <div className="flex items-center justify-between border-b border-white/5 bg-white/5 px-3 py-1.5 text-[10px] uppercase tracking-wider text-white/40">
                <span>{codeBlockLang}</span>
              </div>
            )}
            <pre className="scrollbar-soft overflow-x-auto whitespace-pre p-3 text-cyanGlow/90">
              {codeContent}
            </pre>
          </div>
        );
        inCodeBlock = false;
        codeBlockLines = [];
        codeBlockLang = "";
      } else {
        inCodeBlock = true;
        codeBlockLang = trimmed.slice(3).trim();
      }
      continue;
    }

    if (inCodeBlock) {
      codeBlockLines.push(line);
      continue;
    }

    if (trimmed.startsWith("### ")) {
      elements.push(
        <h4 key={index} className="mt-4 text-xs font-semibold uppercase tracking-wider text-cyanGlow/90">
          {parseBoldAndInlineCode(trimmed.slice(4))}
        </h4>
      );
    } else if (trimmed.startsWith("## ")) {
      elements.push(
        <h3 key={index} className="mt-5 text-sm font-bold uppercase tracking-wider text-white">
          {parseBoldAndInlineCode(trimmed.slice(3))}
        </h3>
      );
    } else if (trimmed.startsWith("# ")) {
      elements.push(
        <h2 key={index} className="mt-6 text-base font-extrabold text-white">
          {parseBoldAndInlineCode(trimmed.slice(2))}
        </h2>
      );
    } else if (trimmed.startsWith("- ") || trimmed.startsWith("* ")) {
      elements.push(
        <div key={index} className="ml-2 mt-1 flex items-start gap-2 text-sm text-white/70">
          <span className="mt-0.5 select-none text-cyanGlow">-</span>
          <span>{parseBoldAndInlineCode(trimmed.slice(2))}</span>
        </div>
      );
    } else if (trimmed.startsWith("> ")) {
      elements.push(
        <blockquote key={index} className="my-2 rounded-r-lg border-l-2 border-cyanGlow/40 bg-cyanGlow/5 py-1.5 pl-3 pr-2 text-xs italic leading-5 text-white/60">
          {parseBoldAndInlineCode(trimmed.slice(2))}
        </blockquote>
      );
    } else if (trimmed === "") {
      elements.push(<div key={index} className="h-2" />);
    } else if (trimmed.startsWith("| ")) {
      const tableLines: string[] = [trimmed];
      let nextIndex = index + 1;
      while (nextIndex < lines.length && lines[nextIndex].trim().startsWith("|")) {
        tableLines.push(lines[nextIndex].trim());
        nextIndex += 1;
      }
      const headerCells = tableLines[0].split("|").filter(Boolean).map((cell) => cell.trim());
      const dataRows = tableLines.slice(2);
      elements.push(
        <div key={index} className="my-3 overflow-x-auto rounded-lg border border-white/10">
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b border-white/10 bg-white/5">
                {headerCells.map((cell, cellIndex) => (
                  <th key={cellIndex} className="px-3 py-1.5 text-left font-semibold text-white/70">{cell}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {dataRows.map((row, rowIndex) => {
                const cells = row.split("|").filter(Boolean).map((cell) => cell.trim());
                return (
                  <tr key={rowIndex} className="border-b border-white/5">
                    {cells.map((cell, cellIndex) => (
                      <td key={cellIndex} className="px-3 py-1.5 text-white/55">{parseBoldAndInlineCode(cell)}</td>
                    ))}
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      );
      index = nextIndex - 1;
    } else {
      elements.push(
        <p key={index} className="mt-1 text-sm leading-6 text-white/65">
          {parseBoldAndInlineCode(line)}
        </p>
      );
    }
  }

  if (inCodeBlock && codeBlockLines.length > 0) {
    elements.push(
      <div key="code-unclosed" className="my-3 overflow-hidden rounded-xl border border-white/10 bg-black/40 font-mono text-xs">
        <pre className="scrollbar-soft overflow-x-auto whitespace-pre p-3 text-cyanGlow/90">
          {codeBlockLines.join("\n")}
        </pre>
      </div>
    );
  }

  return elements;
}

export function extractSuggestedCode(markdown: string, language: Language) {
  const codeBlocks = Array.from(markdown.matchAll(/```([a-zA-Z0-9_-]+)?\s*\n([\s\S]*?)```/g));
  if (!codeBlocks.length) {
    return null;
  }

  const preferredLanguages = {
    python: ["python", "py"],
    javascript: ["javascript", "js"],
    typescript: ["typescript", "ts"],
    html: ["html"],
    sql: ["sql"]
  }[language];
  const matchingBlock = codeBlocks.find((block) => preferredLanguages.includes((block[1] ?? "").toLowerCase())) ?? codeBlocks[0];
  const code = matchingBlock[2]?.trim();
  return code ? code : null;
}
