"use client";

import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties, type PointerEvent as ReactPointerEvent } from "react";

type ResizeTarget = "tasks" | "agent" | "output";

const COLLAPSED_SIDE_WIDTH = 52;
const COLLAPSED_OUTPUT_HEIGHT = 46;
const DEFAULT_SIDE_WIDTH = 230;
const DEFAULT_AGENT_WIDTH = 340;
const DEFAULT_OUTPUT_HEIGHT = 280;
const MIN_SIDE_WIDTH = 200;
const MAX_SIDE_WIDTH = 420;
const MIN_OUTPUT_HEIGHT = 160;

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

function getMaxOutputHeight() {
  if (typeof window === "undefined") {
    return 520;
  }

  return Math.max(260, Math.min(520, window.innerHeight * 0.62));
}

export function useWorkspaceIdeLayout() {
  const [tasksWidth, setTasksWidth] = useState(DEFAULT_SIDE_WIDTH);
  const [agentWidth, setAgentWidth] = useState(DEFAULT_AGENT_WIDTH);
  const [outputHeight, setOutputHeight] = useState(DEFAULT_OUTPUT_HEIGHT);
  const [isTasksCollapsed, setIsTasksCollapsed] = useState(false);
  const [isAgentCollapsed, setIsAgentCollapsed] = useState(false);
  const [isOutputCollapsed, setIsOutputCollapsed] = useState(false);
  const [resizeTarget, setResizeTarget] = useState<ResizeTarget | null>(null);
  const resizeStartRef = useRef<{
    target: ResizeTarget;
    pointerX: number;
    pointerY: number;
    tasksWidth: number;
    agentWidth: number;
    outputHeight: number;
  } | null>(null);

  useEffect(() => {
    if (!resizeTarget) {
      return;
    }

    function handlePointerMove(event: PointerEvent) {
      const start = resizeStartRef.current;
      if (!start) {
        return;
      }

      if (start.target === "tasks") {
        setTasksWidth(clamp(start.tasksWidth + event.clientX - start.pointerX, MIN_SIDE_WIDTH, MAX_SIDE_WIDTH));
        return;
      }

      if (start.target === "agent") {
        setAgentWidth(clamp(start.agentWidth + start.pointerX - event.clientX, MIN_SIDE_WIDTH, MAX_SIDE_WIDTH));
        return;
      }

      setOutputHeight(clamp(start.outputHeight + start.pointerY - event.clientY, MIN_OUTPUT_HEIGHT, getMaxOutputHeight()));
    }

    function handlePointerUp() {
      resizeStartRef.current = null;
      setResizeTarget(null);
      document.body.style.cursor = "";
      document.body.style.userSelect = "";
    }

    window.addEventListener("pointermove", handlePointerMove);
    window.addEventListener("pointerup", handlePointerUp);
    return () => {
      window.removeEventListener("pointermove", handlePointerMove);
      window.removeEventListener("pointerup", handlePointerUp);
      document.body.style.cursor = "";
      document.body.style.userSelect = "";
    };
  }, [resizeTarget]);

  const startResize = useCallback((target: ResizeTarget) => (event: ReactPointerEvent<HTMLButtonElement>) => {
    event.preventDefault();
    resizeStartRef.current = {
      target,
      pointerX: event.clientX,
      pointerY: event.clientY,
      tasksWidth,
      agentWidth,
      outputHeight
    };
    setResizeTarget(target);
    document.body.style.cursor = target === "output" ? "row-resize" : "col-resize";
    document.body.style.userSelect = "none";
  }, [agentWidth, outputHeight, tasksWidth]);

  const resetLayout = useCallback(() => {
    setTasksWidth(DEFAULT_SIDE_WIDTH);
    setAgentWidth(DEFAULT_AGENT_WIDTH);
    setOutputHeight(DEFAULT_OUTPUT_HEIGHT);
    setIsTasksCollapsed(false);
    setIsAgentCollapsed(false);
    setIsOutputCollapsed(false);
  }, []);

  const gridStyle = useMemo<CSSProperties>(() => ({
    gridTemplateColumns: `${isTasksCollapsed ? COLLAPSED_SIDE_WIDTH : tasksWidth}px 8px minmax(0, 1fr) 8px ${isAgentCollapsed ? COLLAPSED_SIDE_WIDTH : agentWidth}px`,
    gridTemplateRows: `minmax(0, 1fr) 8px ${isOutputCollapsed ? COLLAPSED_OUTPUT_HEIGHT : outputHeight}px`
  }), [agentWidth, isAgentCollapsed, isOutputCollapsed, isTasksCollapsed, outputHeight, tasksWidth]);

  return {
    gridStyle,
    isAgentCollapsed,
    isOutputCollapsed,
    isTasksCollapsed,
    resetLayout,
    resizeTarget,
    startResize,
    toggleAgentCollapsed: () => setIsAgentCollapsed((current) => !current),
    toggleOutputCollapsed: () => setIsOutputCollapsed((current) => !current),
    toggleTasksCollapsed: () => setIsTasksCollapsed((current) => !current)
  };
}
