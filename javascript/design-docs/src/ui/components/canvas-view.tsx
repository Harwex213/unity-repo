import { useCallback, useEffect, useRef, useState } from "react";
import { fitRect, zoomAt } from "../../core/camera";
import { renderFrame } from "../../canvas/renderer";
import { attachViewportInput } from "../../canvas/viewport-input";
import type { FC } from "react";
import type { TCamera } from "../../core/camera";
import type { TDocument } from "../../core/types";

type TCanvasViewProps = {
  doc: TDocument | null;
};

const BUTTON_ZOOM_FACTOR = 1.25;

// The camera lives in a ref. Input changes it many times per frame,
// and a React state update per event would re-render the tree too often.
// Only the zoom label goes through React state.
const CanvasView: FC<TCanvasViewProps> = ({ doc }) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const cameraRef = useRef<TCamera>({ x: 0, y: 0, scale: 1 });
  const docRef = useRef(doc);
  const frameRef = useRef(0);
  const [scale, setScale] = useState(1);

  const requestRender = useCallback(() => {
    if (frameRef.current) {
      return;
    }
    frameRef.current = requestAnimationFrame(() => {
      frameRef.current = 0;
      const canvas = canvasRef.current;
      if (canvas) {
        renderFrame(canvas, cameraRef.current, docRef.current);
        setScale(cameraRef.current.scale);
      }
    });
  }, []);

  const setCamera = useCallback(
    (camera: TCamera) => {
      cameraRef.current = camera;
      requestRender();
    },
    [requestRender],
  );

  const fit = useCallback(() => {
    const canvas = canvasRef.current;
    const current = docRef.current;
    if (canvas && current) {
      setCamera(fitRect(current.bounds, canvas.clientWidth, canvas.clientHeight));
    }
  }, [setCamera]);

  const zoomAtCenter = (factor: number) => {
    const canvas = canvasRef.current;
    if (canvas) {
      const center = { x: canvas.clientWidth / 2, y: canvas.clientHeight / 2 };
      setCamera(zoomAt(cameraRef.current, center, factor));
    }
  };

  // Input listeners, resize and theme changes.
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }
    const detachInput = attachViewportInput(canvas, {
      getCamera: () => cameraRef.current,
      setCamera,
    });
    const resizeObserver = new ResizeObserver(requestRender);
    resizeObserver.observe(canvas);
    const colorScheme = window.matchMedia("(prefers-color-scheme: dark)");
    colorScheme.addEventListener("change", requestRender);

    return () => {
      detachInput();
      resizeObserver.disconnect();
      colorScheme.removeEventListener("change", requestRender);
      cancelAnimationFrame(frameRef.current);
      frameRef.current = 0;
    };
  }, [setCamera, requestRender]);

  // A new document opens fitted to the view.
  useEffect(() => {
    docRef.current = doc;
    fit();
    return doc?.subscribe?.(requestRender);
  }, [doc, fit, requestRender]);

  return (
    <div className="canvas-view">
      <canvas ref={canvasRef} className="canvas" onDoubleClick={fit} />
      <div className="zoom-controls">
        <button type="button" onClick={() => zoomAtCenter(1 / BUTTON_ZOOM_FACTOR)} aria-label="Отдалить">
          −
        </button>
        <span className="zoom-label">{Math.round(scale * 100)}%</span>
        <button type="button" onClick={() => zoomAtCenter(BUTTON_ZOOM_FACTOR)} aria-label="Приблизить">
          +
        </button>
        <button type="button" onClick={fit}>
          Вписать
        </button>
      </div>
    </div>
  );
};

export { CanvasView };
