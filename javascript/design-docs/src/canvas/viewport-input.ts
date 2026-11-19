import { panBy, zoomAt } from "../core/camera";
import type { TCamera } from "../core/camera";
import type { TPoint } from "../core/types";

type TViewportInputOptions = {
  getCamera: () => TCamera;
  setCamera: (camera: TCamera) => void;
};

// Pixels per wheel step for line and page delta modes.
const LINE_HEIGHT = 16;
const PAGE_HEIGHT = 800;

// Zoom speed per wheel pixel.
// A trackpad pinch arrives as a wheel event with ctrlKey and small deltas,
// so the pinch gets a higher speed.
const WHEEL_ZOOM_SPEED = 0.002;
const PINCH_ZOOM_SPEED = 0.01;

const wheelPixels = (event: WheelEvent) => {
  switch (event.deltaMode) {
    case WheelEvent.DOM_DELTA_LINE:
      return event.deltaY * LINE_HEIGHT;
    case WheelEvent.DOM_DELTA_PAGE:
      return event.deltaY * PAGE_HEIGHT;
    default:
      return event.deltaY;
  }
};

const centroid = (points: TPoint[]): TPoint => {
  let x = 0;
  let y = 0;
  for (const point of points) {
    x += point.x;
    y += point.y;
  }
  return { x: x / points.length, y: y / points.length };
};

const distance = (a: TPoint, b: TPoint) => Math.hypot(a.x - b.x, a.y - b.y);

// Mouse drag and one-finger drag pan the view.
// Two fingers pan and zoom the view around their midpoint.
// The wheel zooms around the cursor.
// The function returns a callback that removes all listeners.
const attachViewportInput = (element: HTMLElement, { getCamera, setCamera }: TViewportInputOptions) => {
  // Active pointers by id, in element coordinates.
  const pointers = new Map<number, TPoint>();

  const toLocal = (event: { clientX: number; clientY: number }): TPoint => {
    const rect = element.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  };

  const onPointerDown = (event: PointerEvent) => {
    if (event.pointerType === "mouse" && event.button !== 0 && event.button !== 1) {
      return;
    }
    element.setPointerCapture(event.pointerId);
    pointers.set(event.pointerId, toLocal(event));
    element.classList.add("is-dragging");
  };

  const onPointerMove = (event: PointerEvent) => {
    const previous = pointers.get(event.pointerId);
    if (!previous) {
      return;
    }

    // Only the first two pointers take part in the gesture.
    const ids = [...pointers.keys()].slice(0, 2);
    const before = ids.map((id) => pointers.get(id) as TPoint);
    pointers.set(event.pointerId, toLocal(event));
    const after = ids.map((id) => pointers.get(id) as TPoint);

    const centerBefore = centroid(before);
    const centerAfter = centroid(after);
    let camera = panBy(getCamera(), centerAfter.x - centerBefore.x, centerAfter.y - centerBefore.y);

    const [a0, b0] = before;
    const [a1, b1] = after;
    if (a0 && b0 && a1 && b1) {
      const distanceBefore = distance(a0, b0);
      if (distanceBefore > 0) {
        camera = zoomAt(camera, centerAfter, distance(a1, b1) / distanceBefore);
      }
    }

    setCamera(camera);
  };

  const onPointerEnd = (event: PointerEvent) => {
    pointers.delete(event.pointerId);
    if (pointers.size === 0) {
      element.classList.remove("is-dragging");
    }
  };

  const onWheel = (event: WheelEvent) => {
    // The page must not scroll or zoom under the canvas.
    event.preventDefault();
    const speed = event.ctrlKey ? PINCH_ZOOM_SPEED : WHEEL_ZOOM_SPEED;
    const factor = Math.exp(-wheelPixels(event) * speed);
    setCamera(zoomAt(getCamera(), toLocal(event), factor));
  };

  // Safari sends its own gesture events for a trackpad pinch.
  // The wheel handler already covers the pinch, so these events are blocked.
  const onGesture = (event: Event) => event.preventDefault();

  element.addEventListener("pointerdown", onPointerDown);
  element.addEventListener("pointermove", onPointerMove);
  element.addEventListener("pointerup", onPointerEnd);
  element.addEventListener("pointercancel", onPointerEnd);
  element.addEventListener("wheel", onWheel, { passive: false });
  element.addEventListener("gesturestart", onGesture);
  element.addEventListener("gesturechange", onGesture);

  return () => {
    element.removeEventListener("pointerdown", onPointerDown);
    element.removeEventListener("pointermove", onPointerMove);
    element.removeEventListener("pointerup", onPointerEnd);
    element.removeEventListener("pointercancel", onPointerEnd);
    element.removeEventListener("wheel", onWheel);
    element.removeEventListener("gesturestart", onGesture);
    element.removeEventListener("gesturechange", onGesture);
  };
};

export { attachViewportInput };
