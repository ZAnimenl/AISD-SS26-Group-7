"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { WorkspaceClient } from "@/components/workspace/WorkspaceClient";
import { getSystemConfig, getWorkspace, getWorkspaceContext, isAuthenticationError, startAssessment } from "@/lib/api";
import { SectionHeader } from "@/components/ui/SectionHeader";
import type { Assessment, WorkspaceState } from "@/lib/types";

export default function WorkspacePage() {
  const router = useRouter();
  const params = useParams<{ assessmentId: string }>();
  const assessmentId = params.assessmentId;
  const [assessment, setAssessment] = useState<Assessment | null>(null);
  const [workspace, setWorkspace] = useState<WorkspaceState | null>(null);
  const [sandboxAvailable, setSandboxAvailable] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    let retryTimer: number | null = null;

    const scheduleSandboxRefresh = (remainingAttempts: number) => {
      if (cancelled || remainingAttempts <= 0) {
        return;
      }

      retryTimer = window.setTimeout(() => {
        void refreshSandboxAvailability(remainingAttempts - 1);
      }, 3000);
    };

    const refreshSandboxAvailability = async (remainingAttempts = 20) => {
      try {
        const systemConfig = await getSystemConfig();
        if (cancelled) {
          return;
        }

        const isAvailable = systemConfig.features.real_sandbox_enabled;
        setSandboxAvailable(isAvailable);
        if (!isAvailable) {
          scheduleSandboxRefresh(remainingAttempts);
        }
      } catch {
        scheduleSandboxRefresh(remainingAttempts);
      }
    };

    async function load() {
      setError(null);
      try {
        await startAssessment(assessmentId);
        const [nextAssessment, nextWorkspace] = await Promise.all([
          getWorkspaceContext(assessmentId),
          getWorkspace(assessmentId)
        ]);
        if (cancelled) {
          return;
        }

        setAssessment(nextAssessment);
        setWorkspace(nextWorkspace);
        void refreshSandboxAvailability(20);
      } catch (exception) {
        if (cancelled) {
          return;
        }

        if (isAuthenticationError(exception)) {
          router.replace("/login");
          return;
        }

        setError(exception instanceof Error ? exception.message : "Unable to load workspace.");
      }
    }

    const recheckWhenVisible = () => {
      if (document.visibilityState === "visible") {
        void refreshSandboxAvailability(3);
      }
    };

    const recheckOnFocus = () => {
      void refreshSandboxAvailability(3);
    };

    document.addEventListener("visibilitychange", recheckWhenVisible);
    window.addEventListener("focus", recheckOnFocus);
    load();

    return () => {
      cancelled = true;
      if (retryTimer !== null) {
        window.clearTimeout(retryTimer);
      }

      document.removeEventListener("visibilitychange", recheckWhenVisible);
      window.removeEventListener("focus", recheckOnFocus);
    };
  }, [assessmentId, router]);

  if (error) {
    return <SectionHeader eyebrow="Workspace" title={error} />;
  }

  if (!assessment || !workspace) {
    return <SectionHeader eyebrow="Workspace" title="Preparing workspace..." />;
  }

  return <WorkspaceClient assessment={assessment} workspace={workspace} sandboxAvailable={sandboxAvailable} />;
}
