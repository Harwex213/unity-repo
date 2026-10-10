import { useEffect, useRef, useState } from "react";
import artSrc from "../../../assets/menu/menu-bg.webp";
import maskSrc from "../../../assets/menu/menu-bg-mask.png";
import { createMenuBackgroundRenderer } from "./menu-background-gl";
import type { FC } from "react";

/**
 * The canvas never draws more pixels than this. A 4K or retina screen gets an
 * upscaled frame: the art is soft anyway, and the frame rate stays at 60.
 */
const MAX_PIXELS = 2560 * 1440;
/** How fast the parallax follows the pointer, per frame. */
const MOUSE_EASE = 0.06;

type TStatus = "loading" | "ready" | "fallback";

/**
 * The full-screen background of the main menu. The art is a CSS background
 * first. The WebGL2 canvas fades in over it once the shader and both images
 * are ready. Without WebGL2 the CSS background stays.
 */
const MenuBackground: FC = () => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [status, setStatus] = useState<TStatus>("loading");

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }

    const renderer = createMenuBackgroundRenderer(canvas, artSrc, maskSrc);
    if (!renderer) {
      setStatus("fallback");

      return;
    }

    const motionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
    const startedAt = performance.now();
    const mouse = { x: 0, y: 0, targetX: 0, targetY: 0 };
    let isReduced = motionQuery.matches;
    let isLoaded = false;
    let isDisposed = false;
    let frameId = 0;

    const fit = () => {
      const width = canvas.clientWidth;
      const height = canvas.clientHeight;
      if (width === 0 || height === 0) {
        return;
      }

      const dpr = Math.min(window.devicePixelRatio || 1, 2);
      const scale = Math.min(dpr, Math.sqrt(MAX_PIXELS / (width * height)));
      renderer.resize(Math.round(width * scale), Math.round(height * scale));
    };

    const draw = (now: number) => {
      mouse.x += (mouse.targetX - mouse.x) * MOUSE_EASE;
      mouse.y += (mouse.targetY - mouse.y) * MOUSE_EASE;

      renderer.render({
        timeSeconds: isReduced ? 0 : (now - startedAt) / 1000,
        mouseX: isReduced ? 0 : mouse.x,
        mouseY: isReduced ? 0 : mouse.y,
        motion: isReduced ? 0 : 1,
      });
    };

    // A hidden tab and reduced motion get no animation loop.
    const schedule = () => {
      if (frameId !== 0 || isDisposed || !isLoaded || isReduced || document.hidden) {
        return;
      }

      frameId = requestAnimationFrame(tick);
    };

    const tick = (now: number) => {
      frameId = 0;
      draw(now);
      schedule();
    };

    const stop = () => {
      if (frameId !== 0) {
        cancelAnimationFrame(frameId);
        frameId = 0;
      }
    };

    // A new canvas size clears the canvas, so the frame is drawn again at once.
    const redraw = () => {
      fit();
      if (isLoaded) {
        draw(performance.now());
      }
    };

    const onVisibilityChange = () => {
      if (document.hidden) {
        stop();
      } else {
        schedule();
      }
    };

    const onMotionChange = (event: MediaQueryListEvent) => {
      isReduced = event.matches;
      redraw();
      schedule();
    };

    const onPointerMove = (event: PointerEvent) => {
      mouse.targetX = (event.clientX / window.innerWidth) * 2 - 1;
      mouse.targetY = (event.clientY / window.innerHeight) * 2 - 1;
    };

    const resizeObserver = new ResizeObserver(redraw);
    resizeObserver.observe(canvas);
    document.addEventListener("visibilitychange", onVisibilityChange);
    motionQuery.addEventListener("change", onMotionChange);
    window.addEventListener("pointermove", onPointerMove);

    renderer.ready
      .then(() => {
        if (isDisposed) {
          return;
        }

        isLoaded = true;
        redraw();
        setStatus("ready");
        schedule();
      })
      .catch((error: unknown) => {
        console.warn("Main menu background art did not load", error);
        if (!isDisposed) {
          setStatus("fallback");
        }
      });

    return () => {
      isDisposed = true;
      stop();
      resizeObserver.disconnect();
      document.removeEventListener("visibilitychange", onVisibilityChange);
      motionQuery.removeEventListener("change", onMotionChange);
      window.removeEventListener("pointermove", onPointerMove);
      renderer.dispose();
    };
  }, []);

  return (
    <div className="main-menu-bg" style={{ backgroundImage: `url(${artSrc})` }} aria-hidden="true">
      <canvas
        ref={canvasRef}
        className={`main-menu-bg__canvas ${status === "ready" ? "main-menu-bg__canvas--ready" : ""} ${status === "fallback" ? "main-menu-bg__canvas--off" : ""}`}
      />
    </div>
  );
};

export { MenuBackground };
