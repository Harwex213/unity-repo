import { useCallback, useEffect, useRef, useState } from "react";
import { fitRect, zoomAt } from "../../core/camera";
import { renderFrame } from "../../canvas/renderer";
import { createTextLayer } from "../../canvas/text-layer";
import { attachViewportInput } from "../../canvas/viewport-input";
import type { FC, MouseEvent } from "react";
import type { TTextLayer } from "../../canvas/text-layer";
import type { TCamera } from "../../core/camera";
import type { TDocument, TRect } from "../../core/types";

type TCanvasViewProps = {
  doc: TDocument | null;
  // The rect to show when the document opens. Null shows the whole document.
  view: TRect | null;
};

const BUTTON_ZOOM_FACTOR = 1.25;

// The camera lives in a ref. Input changes it many times per frame,
// and a React state update per event would re-render the tree too often.
// Only the zoom label goes through React state.
const CanvasView: FC<TCanvasViewProps> = ({ doc, view }) => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const textLayerElementRef = useRef<HTMLDivElement>(null);
  const textLayerRef = useRef<TTextLayer | null>(null);
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
        const runs = renderFrame(canvas, cameraRef.current, docRef.current);
        textLayerRef.current?.update(runs, cameraRef.current);
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

  // A double click selects the text block under the cursor.
  const selectTextAt = (event: MouseEvent<HTMLCanvasElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    const point = { x: event.clientX - rect.left, y: event.clientY - rect.top };
    textLayerRef.current?.selectAt(point, cameraRef.current);
  };

  // Input listeners, the text layer, resize and theme changes.
  useEffect(() => {
    const canvas = canvasRef.current;
    const textLayerElement = textLayerElementRef.current;
    if (!canvas || !textLayerElement) {
      return;
    }
    const textLayer = createTextLayer(textLayerElement);
    textLayerRef.current = textLayer;
    requestRender();
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
      textLayer.destroy();
      textLayerRef.current = null;
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

  // A view from the URL zooms to its rect. The effect runs after the fit above.
  const viewKey = view ? `${view.x},${view.y},${view.width},${view.height}` : "";
  useEffect(() => {
    const canvas = canvasRef.current;
    if (canvas && view) {
      setCamera(fitRect(view, canvas.clientWidth, canvas.clientHeight));
    }
    // `viewKey` stands for `view`: a new object with the same rect must not move the camera.
  }, [doc, viewKey, setCamera]);

  return (
    <div className="canvas-view">
      <canvas ref={canvasRef} className="canvas" onDoubleClick={selectTextAt} />
      <div ref={textLayerElementRef} className="text-layer" />
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
