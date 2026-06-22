"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, Clock3, Loader2, Send } from "lucide-react";
import { getReflection, isAuthenticationError, saveReflection, submitReflection } from "@/lib/api";
import { SectionHeader } from "@/components/ui/SectionHeader";
import type { ReflectionState } from "@/lib/types";

const REFLECTION_PROMPT = "In no more than 100 words, explain how you used the AI assistant during this assessment. Include one suggestion that helped and how you verified it, and one suggestion that you rejected, corrected, or found unhelpful.";

function countWords(value: string) {
  return value.trim() ? value.trim().split(/\s+/).length : 0;
}

function formatRemaining(milliseconds: number) {
  const seconds = Math.max(0, Math.ceil(milliseconds / 1000));
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
}

export default function AssessmentReflectionPage() {
  const router = useRouter();
  const params = useParams<{ assessmentId: string }>();
  const assessmentId = params.assessmentId;
  const [state, setState] = useState<ReflectionState | null>(null);
  const [text, setText] = useState("");
  const [remaining, setRemaining] = useState<number | null>(null);
  const [saveStatus, setSaveStatus] = useState<"saved" | "saving" | "unsaved">("saved");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const timeoutSubmittedRef = useRef(false);
  const wordCount = useMemo(() => countWords(text), [text]);

  useEffect(() => {
    getReflection(assessmentId)
      .then((next) => {
        if (next.reflection_submitted_at) {
          router.replace(`/student/assessments/${assessmentId}/review`);
          return;
        }
        setState(next);
        setText(next.reflection_text);
      })
      .catch((exception) => {
        if (isAuthenticationError(exception)) {
          router.replace("/login");
          return;
        }
        setError(exception instanceof Error ? exception.message : "Unable to load reflection.");
      });
  }, [assessmentId, router]);

  useEffect(() => {
    if (!state?.reflection_deadline) {
      return;
    }
    const update = () => setRemaining(new Date(state.reflection_deadline).getTime() - Date.now());
    update();
    const timer = window.setInterval(update, 250);
    return () => window.clearInterval(timer);
  }, [state?.reflection_deadline]);

  useEffect(() => {
    if (!state || text === state.reflection_text || wordCount > 100) {
      return;
    }
    const timer = window.setTimeout(() => {
      setSaveStatus("saving");
      saveReflection(assessmentId, text)
        .then((next) => {
          setState(next);
          setSaveStatus("saved");
          setError(null);
        })
        .catch((exception) => {
          setSaveStatus("unsaved");
          setError(exception instanceof Error ? exception.message : "Reflection autosave failed.");
        });
    }, 600);
    return () => window.clearTimeout(timer);
  }, [assessmentId, state, text, wordCount]);

  useEffect(() => {
    if (!state || remaining === null || remaining > 0 || timeoutSubmittedRef.current) {
      return;
    }
    timeoutSubmittedRef.current = true;
    setSubmitting(true);
    window.setTimeout(() => {
      submitReflection(assessmentId, text)
        .then(() => router.replace(`/student/assessments/${assessmentId}/review`))
        .catch(() => getReflection(assessmentId)
          .then(() => router.replace(`/student/assessments/${assessmentId}/review`))
          .catch((exception) => setError(exception instanceof Error ? exception.message : "Reflection submission failed.")))
        .finally(() => setSubmitting(false));
    }, 750);
  }, [assessmentId, remaining, router, state, text]);

  async function handleSubmit() {
    if (submitting || wordCount === 0 || wordCount > 100) {
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await submitReflection(assessmentId, text);
      router.replace(`/student/assessments/${assessmentId}/review`);
    } catch (exception) {
      setError(exception instanceof Error ? exception.message : "Reflection submission failed.");
      setSubmitting(false);
    }
  }

  if (!state && !error) {
    return <SectionHeader eyebrow="Final reflection" title="Loading reflection..." />;
  }

  if (!state) {
    return (
      <div className="mx-auto max-w-4xl">
        <SectionHeader eyebrow="AI-enabled assessment" title="Reflection unavailable" />
        <section className="panel">
          <p className="relative text-white/60">{error ?? "Submit the assessment before completing the reflection."}</p>
          <Link className="btn-secondary relative mt-5" href={`/student/assessments/${assessmentId}/workspace`}>
            <ArrowLeft size={16} />
            Back to workspace
          </Link>
        </section>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-4xl">
      <SectionHeader eyebrow="AI-enabled assessment" title="Final reflection" />
      <section className="panel">
        <div className="relative">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <span className="badge"><Clock3 size={15} /> {remaining === null ? "—" : formatRemaining(remaining)}</span>
            <span className={`text-sm ${wordCount > 100 ? "text-pinkGlow" : "text-white/45"}`}>{wordCount}/100 words · {saveStatus}</span>
          </div>
          <p className="mt-6 text-lg leading-8 text-white/80">{REFLECTION_PROMPT}</p>
          <textarea
            className="mt-5 min-h-64 w-full rounded-2xl border border-white/10 bg-black/25 p-4 text-white outline-none transition focus:border-cyanGlow/60"
            value={text}
            disabled={submitting || remaining === null || remaining <= 0}
            onChange={(event) => {
              setText(event.target.value);
              setSaveStatus("unsaved");
            }}
            placeholder="Write your concise reflection here..."
          />
          {error ? <p className="mt-3 text-sm text-pinkGlow">{error}</p> : null}
          <p className="mt-3 text-sm text-white/45">Your submitted code is frozen. This draft is autosaved and will be finalized automatically when time expires.</p>
          <div className="mt-6 flex justify-end">
            <button className="btn-primary" onClick={handleSubmit} disabled={submitting || wordCount === 0 || wordCount > 100 || remaining === null || remaining <= 0}>
              {submitting ? <Loader2 className="animate-spin" size={16} /> : <Send size={16} />}
              Submit reflection
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}
