import type { TCamera } from "../core/camera";
import type { TDocument } from "../core/types";

// Grid lines closer than this many CSS pixels are not drawn.
const MIN_GRID_STEP = 12;

const cssVar = (name: string) => getComputedStyle(document.documentElement).getPropertyValue(name).trim();

// The grid step in world units is a power of ten.
// Every tenth line is a major line.
const drawGrid = (ctx: CanvasRenderingContext2D, camera: TCamera, width: number, height: number) => {
  const step = 10 ** Math.ceil(Math.log10(MIN_GRID_STEP / camera.scale));
  const screenStep = step * camera.scale;

  // Minor lines fade in while the step grows on screen.
  const minorAlpha = Math.min(1, (screenStep - MIN_GRID_STEP) / (MIN_GRID_STEP * 4));

  const drawLines = (lineStep: number, color: string, alpha: number) => {
    if (alpha <= 0) {
      return;
    }
    const screenLineStep = lineStep * camera.scale;
    const startX = camera.x % screenLineStep;
    const startY = camera.y % screenLineStep;

    ctx.beginPath();
    for (let x = startX; x < width; x += screenLineStep) {
      const px = Math.round(x) + 0.5;
      ctx.moveTo(px, 0);
      ctx.lineTo(px, height);
    }
    for (let y = startY; y < height; y += screenLineStep) {
      const py = Math.round(y) + 0.5;
      ctx.moveTo(0, py);
      ctx.lineTo(width, py);
    }
    ctx.globalAlpha = alpha;
    ctx.strokeStyle = color;
    ctx.lineWidth = 1;
    ctx.stroke();
    ctx.globalAlpha = 1;
  };

  drawLines(step, cssVar("--grid-minor"), minorAlpha);
  drawLines(step * 10, cssVar("--grid-major"), 1);
};

// The canvas backing store follows the element size and devicePixelRatio,
// so lines and text stay sharp at any zoom.
const renderFrame = (canvas: HTMLCanvasElement, camera: TCamera, doc: TDocument | null) => {
  const ctx = canvas.getContext("2d");
  if (!ctx) {
    return;
  }

  const dpr = window.devicePixelRatio || 1;
  const width = canvas.clientWidth;
  const height = canvas.clientHeight;
  const pixelWidth = Math.round(width * dpr);
  const pixelHeight = Math.round(height * dpr);
  if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
    canvas.width = pixelWidth;
    canvas.height = pixelHeight;
  }

  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.fillStyle = cssVar("--canvas-bg");
  ctx.fillRect(0, 0, width, height);
  drawGrid(ctx, camera, width, height);

  if (!doc) {
    return;
  }

  ctx.save();
  ctx.translate(camera.x, camera.y);
  ctx.scale(camera.scale, camera.scale);
  doc.draw(ctx);
  ctx.restore();
};

export { renderFrame };
