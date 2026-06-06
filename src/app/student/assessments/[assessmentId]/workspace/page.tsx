"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { WorkspaceClient } from "@/components/workspace/WorkspaceClient";
import { getWorkspace, getWorkspaceContext, isAuthenticationError, startAssessment } from "@/lib/api";
import { SectionHeader } from "@/components/ui/SectionHeader";
import type { Assessment, WorkspaceState } from "@/lib/types";

export default function WorkspacePage() {
  const router = useRouter();
  const params = useParams<{ assessmentId: string }>();
  const assessmentId = params.assessmentId;
  const [assessment, setAssessment] = useState<Assessment | null>(null);
  const [workspace, setWorkspace] = useState<WorkspaceState | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function load() {
      setError(null);
      try {
        await startAssessment(assessmentId);
        const nextAssessment = await getWorkspaceContext(assessmentId);
        const nextWorkspace = await getWorkspace(assessmentId);
        setAssessment(nextAssessment);
        setWorkspace(nextWorkspace);
      } catch (exception) {
        if (isAuthenticationError(exception)) {
          router.replace("/login");
          return;
        }

        setError(exception instanceof Error ? exception.message : "Unable to load workspace.");
      }
    }

    load();
  }, [assessmentId, router]);

  if (error) {
    return <SectionHeader eyebrow="Workspace" title={error} />;
  }

  if (!assessment || !workspace) {
    return <SectionHeader eyebrow="Workspace" title="Connecting to backend..." />;
  }

  return <WorkspaceClient assessment={assessment} workspace={workspace} />;
}
