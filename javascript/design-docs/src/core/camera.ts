import type { TPoint, TRect } from "./types";

// Screen point = world point * scale + (x, y).
// Screen units are CSS pixels of the canvas element.
type TCamera = {
  x: number;
  y: number;
  scale: number;
};

const MIN_SCALE = 0.02;
const MAX_SCALE = 50;

const clampScale = (scale: number) => Math.min(MAX_SCALE, Math.max(MIN_SCALE, scale));

const screenToWorld = (camera: TCamera, point: TPoint): TPoint => ({
  x: (point.x - camera.x) / camera.scale,
  y: (point.y - camera.y) / camera.scale,
});

const panBy = (camera: TCamera, dx: number, dy: number): TCamera => ({
  ...camera,
  x: camera.x + dx,
  y: camera.y + dy,
});

// The world point under `anchor` stays under `anchor` after the zoom.
const zoomAt = (camera: TCamera, anchor: TPoint, factor: number): TCamera => {
  const scale = clampScale(camera.scale * factor);
  const k = scale / camera.scale;
  return {
    x: anchor.x - (anchor.x - camera.x) * k,
    y: anchor.y - (anchor.y - camera.y) * k,
    scale,
  };
};

// The camera shows the whole rect in the center of the view.
const fitRect = (rect: TRect, viewWidth: number, viewHeight: number, padding = 32): TCamera => {
  const width = Math.max(1, viewWidth - padding * 2);
  const height = Math.max(1, viewHeight - padding * 2);
  const scale = clampScale(Math.min(width / rect.width, height / rect.height));
  return {
    x: viewWidth / 2 - (rect.x + rect.width / 2) * scale,
    y: viewHeight / 2 - (rect.y + rect.height / 2) * scale,
    scale,
  };
};

export { MAX_SCALE, MIN_SCALE, fitRect, panBy, screenToWorld, zoomAt };
export type { TCamera };
