"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { WorkspaceClient } from "@/components/workspace/WorkspaceClient";
import { getWorkspace, getWorkspaceContext, startAssessment } from "@/lib/api";
import { SectionHeader } from "@/components/ui/SectionHeader";
import type { Assessment, WorkspaceState } from "@/lib/types";

export default function WorkspacePage({ params }: { params: { assessmentId: string } }) {
  const router = useRouter();
  const [assessment, setAssessment] = useState<Assessment | null>(null);
  const [workspace, setWorkspace] = useState<WorkspaceState | null>(null);

  useEffect(() => {
    async function load() {
      try {
        await startAssessment(params.assessmentId);
        const [nextAssessment, nextWorkspace] = await Promise.all([
          getWorkspaceContext(params.assessmentId),
          getWorkspace(params.assessmentId)
        ]);
        setAssessment(nextAssessment);
        setWorkspace(nextWorkspace);
      } catch {
        router.replace("/login");
      }
    }

    load();
  }, [params.assessmentId, router]);

  if (!assessment || !workspace) {
    return <SectionHeader eyebrow="Workspace" title="Connecting to backend..." />;
  }

  return <WorkspaceClient assessment={assessment} workspace={workspace} />;
}
