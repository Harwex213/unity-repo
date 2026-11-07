import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { TIMELINE } from "../core/timeline";
import { createSound } from "../audio/sound";
import { createEngine } from "../render/engine";
import { Hud } from "./hud";
import type { FC } from "react";
import type { TEngine } from "../render/engine";

/** A key press or click during the intro jumps here, after the blast has settled. */
const SKIP_TO = TIMELINE.hud;

const readDebugTime = (): number | null => {
  const raw = new URLSearchParams(window.location.search).get("t");

  return raw === null ? null : Number(raw);
};

/**
 * Owns the timeline of the page. The engine owns the time, and this component
 * only reads it to show the HUD and to start the sound at the right moment.
 */
const App: FC = () => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const engineRef = useRef<TEngine | null>(null);
  const sound = useMemo(() => createSound(), []);
  const [isHudVisible, setHudVisible] = useState(false);
  const [isMuted, setMuted] = useState(false);
  const isUnlocked = useRef(false);
  const hudVisibleRef = useRef(false);
  /** The run of the intro the scheduled sounds belong to. */
  const scoredRun = useRef(false);

  const scoreIntro = useCallback(() => {
    const engine = engineRef.current;
    if (!engine || scoredRun.current || !isUnlocked.current) {
      return;
    }

    scoredRun.current = true;
    const time = engine.getTime();
    sound.rumble(TIMELINE.crackStart - time);
    sound.blast(TIMELINE.detonate - time);
  }, [sound]);

  const showHud = useCallback(() => {
    hudVisibleRef.current = true;
    setHudVisible(true);
    if (isUnlocked.current) {
      sound.drone();
    }
  }, [sound]);

  const skip = useCallback(() => {
    const engine = engineRef.current;
    if (!engine || engine.getTime() >= SKIP_TO) {
      return;
    }

    sound.silenceIntro();
    engine.setTime(SKIP_TO);
    showHud();
  }, [showHud, sound]);

  const replay = useCallback(() => {
    const engine = engineRef.current;
    if (!engine) {
      return;
    }

    hudVisibleRef.current = false;
    setHudVisible(false);
    scoredRun.current = false;
    engine.setTime(0);
    scoreIntro();
  }, [scoreIntro]);

  const toggleMute = useCallback(() => {
    setMuted((current) => {
      sound.setMuted(!current);

      return !current;
    });
  }, [sound]);

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return undefined;
    }

    let isDisposed = false;
    const isCalm = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const debugTime = readDebugTime();

    void createEngine({ canvas, isCalm }).then((engine) => {
      if (isDisposed) {
        engine.dispose();

        return;
      }

      engineRef.current = engine;
      Object.assign(window, { __engine: engine, __showHud: showHud });
      if (debugTime !== null) {
        engine.freeze(debugTime);

        return;
      }

      if (isCalm) {
        engine.setTime(SKIP_TO + 6);
      }
      engine.start();
    });

    const timer = window.setInterval(() => {
      const engine = engineRef.current;
      if (engine && !hudVisibleRef.current && engine.getTime() >= TIMELINE.hud) {
        showHud();
      }
    }, 50);

    const onPointerMove = (event: PointerEvent): void => {
      engineRef.current?.setPointer(
        (event.clientX / window.innerWidth) * 2 - 1,
        (event.clientY / window.innerHeight) * 2 - 1,
      );
    };

    window.addEventListener("pointermove", onPointerMove);

    return () => {
      isDisposed = true;
      window.clearInterval(timer);
      window.removeEventListener("pointermove", onPointerMove);
      engineRef.current?.dispose();
      engineRef.current = null;
    };
  }, [showHud]);

  // The first gesture of the page unlocks the audio. During the intro a gesture skips it.
  useEffect(() => {
    const onGesture = (event: Event): void => {
      if (event instanceof KeyboardEvent && (event.metaKey || event.ctrlKey || event.altKey)) {
        return;
      }

      const wasLocked = !isUnlocked.current;
      isUnlocked.current = true;
      sound.unlock();

      if (hudVisibleRef.current) {
        if (wasLocked) {
          sound.drone();
        }
      } else {
        skip();
      }
    };

    window.addEventListener("keydown", onGesture);
    window.addEventListener("pointerdown", onGesture);

    return () => {
      window.removeEventListener("keydown", onGesture);
      window.removeEventListener("pointerdown", onGesture);
    };
  }, [skip, sound]);

  return (
    <main className="stage">
      <canvas ref={canvasRef} className="stage__canvas" />

      <Hud
        isVisible={isHudVisible}
        isMuted={isMuted}
        onHover={sound.hover}
        onConfirm={sound.confirm}
        onReplay={replay}
        onToggleMute={toggleMute}
      />
    </main>
  );
};

export { App };
