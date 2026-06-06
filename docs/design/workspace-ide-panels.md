# Workspace IDE Panels

## Problem Definition

The student workspace currently uses three fixed columns plus an absolute output
overlay. On small or dense screens the output surface overlaps the editor and
transparent panel backgrounds make logs hard to read. The first browser-preview
task also has a file-name contract mismatch: starter files use
`todo_summary_panel.py` or `todo_summary_panel.js`, while existing preview
tests import `TodoSummaryPanel`.
When AI suggestions are applied to the visible starter file, real sandbox runs
can fail before evaluating the student's logic.

## Option Comparison

- Fixed grid plus overlay status quo: simple, but poor for IDE workflows and
  causes the selected output text to look like corrupted overlapped content.
- Off-the-shelf splitter dependency: fast, but adds a new dependency for a small
  interaction surface.
- Native React state with CSS grid and pointer resize handles: no new
  dependency, reversible, and enough for the current four workspace panels.

Chosen option: native React state with explicit panel modes and pointer resize
handles.

## State Machine

States:

- `normal`: task panel, editor, AI panel, and output panel are visible.
- `left-collapsed`: task panel is collapsed to an icon rail.
- `right-collapsed`: AI panel is collapsed to an icon rail.
- `output-collapsed`: output panel is collapsed to its status bar.
- `resizing-left`: pointer drag updates task panel width.
- `resizing-right`: pointer drag updates AI panel width.
- `resizing-output`: pointer drag updates output panel height.

Events:

- `toggle-left`, `toggle-right`, `toggle-output`
- `start-resize-left`, `start-resize-right`, `start-resize-output`
- `drag`, `end-resize`
- `reset-layout`

Guards:

- Widths are clamped to practical IDE bounds.
- Output height is clamped so it cannot hide the editor completely.
- Collapsed panels do not render large scroll containers.

Transitions:

- `normal` + toggle event moves the target panel to its collapsed state.
- A collapsed target + toggle returns it to `normal`.
- Any visible target + start-resize enters its matching resizing state.
- Resizing + drag updates only the target dimension.
- Resizing + end-resize returns to the previous visibility state.
- Any state + reset returns to default dimensions and all panels visible.

Side effects:

- Layout dimensions are kept in component state only.
- No database writes are triggered by resizing or collapsing.
- Run output still comes only from the backend sandbox.

Failure Paths

- If a pointer event cannot be completed, the existing dimensions remain.
- If sandbox output is unavailable, the UI shows a clear dependency/error
  state rather than fabricating preview output.
- If AI returns a suggestion, applying it updates the current visible file only;
  sandbox compatibility aliases make legacy test imports resolve without
  changing the submitted file names.

Rollback Path

- Remove the workspace layout state and resize handles to return to the fixed
  three-column grid.
- Remove grader alias-file writing to return to strict filename matching.

## Impact Surface

- Module 2 workspace layout, output panel, and AI panel rendering.
- Module 3 grader workspace file preparation for public/hidden tests.
- Documentation and acceptance criteria for workspace usability and Task 1
  preview execution.

## Primitive Acceptance Criteria

- The output panel background is opaque enough that editor/sidebar text behind
  it is not readable through the output body.
- Users can collapse and expand the task panel, AI panel, and output panel.
- Users can drag resize the task panel width, AI panel width, and output panel
  height.
- Running the first browser-preview task does not fail only because
  `TodoSummaryPanel` cannot be imported when the visible starter file is named
  `todo_summary_panel`.
- Hidden test inputs and expected outputs remain unavailable to student UI.
