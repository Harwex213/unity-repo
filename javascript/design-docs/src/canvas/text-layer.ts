import { screenToWorld } from "../core/camera";
import type { TCamera } from "../core/camera";
import type { TPoint, TRect } from "../core/types";

// A canvas draws text as pixels, so the browser cannot select it.
// The renderer records every `fillText` call of a document as a text run.
// The text layer puts an invisible DOM copy of the runs over the canvas.
// A double click selects a whole text block in the DOM copy, and the user can copy it.

type TMatrix = [number, number, number, number, number, number];

// One `fillText` call.
type TTextRun = {
  text: string;
  font: string;
  align: CanvasTextAlign;
  // The matrix from the run's local space to world space, as in CSS `matrix(a, b, c, d, e, f)`.
  matrix: TMatrix;
  // The point passed to `fillText`, in local space.
  x: number;
  y: number;
  // The line box in local space.
  left: number;
  top: number;
  width: number;
  height: number;
};

// Lines of one paragraph.
type TTextBlock = {
  runs: TTextRun[];
  // The bounding box in world space.
  bounds: TRect;
  text: string;
};

// The left edge of the text relative to the point of `fillText`. The layer assumes left-to-right text.
const alignOffset = (align: CanvasTextAlign, width: number) => {
  switch (align) {
    case "center":
      return -width / 2;
    case "right":
    case "end":
      return -width;
    default:
      return 0;
  }
};

const toMatrix = (m: DOMMatrix): TMatrix => [m.a, m.b, m.c, m.d, m.e, m.f];

// Runs `draw` and returns the text runs it has drawn, in world space.
// The context must hold the world transform when the function is called.
const recordTextRuns = (ctx: CanvasRenderingContext2D, draw: () => void): TTextRun[] => {
  const runs: TTextRun[] = [];
  const screenToWorldMatrix = ctx.getTransform().inverse();
  const fillText = ctx.fillText;

  ctx.fillText = (text: string, x: number, y: number, maxWidth?: number) => {
    fillText.call(ctx, text, x, y, maxWidth);
    if (text.trim() === "" || ctx.globalAlpha === 0) {
      return;
    }
    const metrics = ctx.measureText(text);
    const width = maxWidth === undefined ? metrics.width : Math.min(metrics.width, maxWidth);
    // Both distances are measured from the baseline that `ctx.textBaseline` sets.
    const ascent = metrics.fontBoundingBoxAscent ?? metrics.actualBoundingBoxAscent;
    const descent = metrics.fontBoundingBoxDescent ?? metrics.actualBoundingBoxDescent;
    runs.push({
      text,
      font: ctx.font,
      align: ctx.textAlign,
      matrix: toMatrix(screenToWorldMatrix.multiply(ctx.getTransform())),
      x,
      y,
      left: x + alignOffset(ctx.textAlign, width),
      top: y - ascent,
      width,
      height: ascent + descent,
    });
  };

  try {
    draw();
  } finally {
    // The own property hides the prototype method. Removing it brings the method back.
    Reflect.deleteProperty(ctx, "fillText");
  }
  return runs;
};

const applyMatrix = ([a, b, c, d, e, f]: TMatrix, point: TPoint): TPoint => ({
  x: a * point.x + c * point.y + e,
  y: b * point.x + d * point.y + f,
});

const runBounds = (run: TTextRun): TRect => {
  const corners = [
    { x: run.left, y: run.top },
    { x: run.left + run.width, y: run.top },
    { x: run.left, y: run.top + run.height },
    { x: run.left + run.width, y: run.top + run.height },
  ].map((corner) => applyMatrix(run.matrix, corner));
  const xs = corners.map((corner) => corner.x);
  const ys = corners.map((corner) => corner.y);
  const x = Math.min(...xs);
  const y = Math.min(...ys);
  return { x, y, width: Math.max(...xs) - x, height: Math.max(...ys) - y };
};

const unionRect = (a: TRect, b: TRect): TRect => {
  const x = Math.min(a.x, b.x);
  const y = Math.min(a.y, b.y);
  return {
    x,
    y,
    width: Math.max(a.x + a.width, b.x + b.width) - x,
    height: Math.max(a.y + a.height, b.y + b.height) - y,
  };
};

const MATRIX_EPSILON = 1e-6;
const ANCHOR_EPSILON = 0.5;
// The next line of a block starts at most this many line heights below the previous line.
const MAX_LINE_GAP = 1.6;

const sameMatrix = (a: TMatrix, b: TMatrix) => a.every((value, index) => Math.abs(value - (b[index] as number)) < MATRIX_EPSILON);

// A run continues the block of the previous run when the document has drawn it as the next line:
// the same font, alignment, transform and anchor x, a little lower.
const continuesBlock = (previous: TTextRun, run: TTextRun) => {
  const gap = run.y - previous.y;
  return (
    run.font === previous.font &&
    run.align === previous.align &&
    sameMatrix(run.matrix, previous.matrix) &&
    Math.abs(run.x - previous.x) < ANCHOR_EPSILON &&
    gap > 0 &&
    gap <= previous.height * MAX_LINE_GAP
  );
};

const groupBlocks = (runs: TTextRun[]): TTextBlock[] => {
  const blocks: TTextBlock[] = [];
  let current: TTextBlock | null = null;
  let previous: TTextRun | null = null;
  for (const run of runs) {
    if (current && previous && continuesBlock(previous, run)) {
      current.runs.push(run);
      current.bounds = unionRect(current.bounds, runBounds(run));
      current.text += `\n${run.text}`;
    } else {
      current = { runs: [run], bounds: runBounds(run), text: run.text };
      blocks.push(current);
    }
    previous = run;
  }
  return blocks;
};

const sameRuns = (a: TTextRun[], b: TTextRun[]) =>
  a.length === b.length &&
  a.every((run, index) => {
    const other = b[index] as TTextRun;
    return (
      run.text === other.text &&
      run.font === other.font &&
      run.left === other.left &&
      run.top === other.top &&
      sameMatrix(run.matrix, other.matrix)
    );
  });

// A hit is a little wider than the text, so a double click between two lines still finds the block.
const HIT_PADDING = 2;

const contains = (rect: TRect, point: TPoint) =>
  point.x >= rect.x - HIT_PADDING &&
  point.x <= rect.x + rect.width + HIT_PADDING &&
  point.y >= rect.y - HIT_PADDING &&
  point.y <= rect.y + rect.height + HIT_PADDING;

type TTextLayer = {
  // Rebuilds the DOM copy when the runs have changed and moves the copy with the camera.
  update: (runs: TTextRun[], camera: TCamera) => void;
  // Selects the block under a point in element coordinates. A point outside text clears the selection.
  selectAt: (point: TPoint, camera: TCamera) => void;
  destroy: () => void;
};

// `container` must cover the canvas exactly.
const createTextLayer = (container: HTMLElement): TTextLayer => {
  const world = document.createElement("div");
  world.className = "text-layer-world";
  container.append(world);

  let runs: TTextRun[] = [];
  let blocks: { block: TTextBlock; element: HTMLElement }[] = [];
  let selected: HTMLElement | null = null;

  const markSelected = (element: HTMLElement | null) => {
    selected?.classList.remove("is-selected");
    selected = element;
    selected?.classList.add("is-selected");
  };

  const rebuild = () => {
    markSelected(null);
    world.replaceChildren();
    blocks = groupBlocks(runs).map((block) => {
      const element = document.createElement("div");
      element.className = "text-block";
      for (const run of block.runs) {
        const line = document.createElement("span");
        line.className = "text-run";
        line.textContent = run.text;
        line.style.font = run.font;
        line.style.width = `${run.width}px`;
        line.style.height = `${run.height}px`;
        line.style.lineHeight = `${run.height}px`;
        line.style.transform = `matrix(${run.matrix.join(",")}) translate(${run.left}px, ${run.top}px)`;
        element.append(line);
      }
      world.append(element);
      return { block, element };
    });
  };

  // The selection always covers one whole block. The copy gets the lines of the block, one per line.
  const blockOfSelection = () => {
    const selection = document.getSelection();
    if (!selection || selection.rangeCount === 0 || !selected) {
      return null;
    }
    return selected.contains(selection.anchorNode) ? blocks.find((entry) => entry.element === selected) : null;
  };

  const onCopy = (event: ClipboardEvent) => {
    const entry = blockOfSelection();
    if (entry && event.clipboardData) {
      event.clipboardData.setData("text/plain", entry.block.text);
      event.preventDefault();
    }
  };

  const onSelectionChange = () => {
    if (selected && !blockOfSelection()) {
      markSelected(null);
    }
  };

  document.addEventListener("copy", onCopy);
  document.addEventListener("selectionchange", onSelectionChange);

  return {
    update: (nextRuns, camera) => {
      if (!sameRuns(runs, nextRuns)) {
        runs = nextRuns;
        rebuild();
      }
      world.style.transform = `matrix(${camera.scale}, 0, 0, ${camera.scale}, ${camera.x}, ${camera.y})`;
    },
    selectAt: (point, camera) => {
      const worldPoint = screenToWorld(camera, point);
      // Nested texts: the smallest block under the point wins.
      let best: (typeof blocks)[number] | null = null;
      for (const entry of blocks) {
        const { bounds } = entry.block;
        if (contains(bounds, worldPoint) && (!best || bounds.width * bounds.height < best.block.bounds.width * best.block.bounds.height)) {
          best = entry;
        }
      }
      const selection = document.getSelection();
      if (!best) {
        if (selected) {
          selection?.removeAllRanges();
        }
        markSelected(null);
        return;
      }
      selection?.selectAllChildren(best.element);
      markSelected(best.element);
    },
    destroy: () => {
      document.removeEventListener("copy", onCopy);
      document.removeEventListener("selectionchange", onSelectionChange);
      world.remove();
    },
  };
};

export { createTextLayer, recordTextRuns };
export type { TTextLayer, TTextRun };
