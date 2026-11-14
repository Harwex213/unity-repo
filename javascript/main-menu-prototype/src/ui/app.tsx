import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { TIMELINE } from "../core/timeline";
import { createSound } from "../audio/sound";
import { createEngine } from "../render/engine";
import { Hud } from "./hud";
import type { FC } from "react";
import type { TEngine } from "../render/engine";

/** A key press or click during the intro jumps here, after the blast has settled. */
const SKIP_TO = TIMELINE.hud;
/** The key press that starts the intro must not skip it with a double click. */
const SKIP_GUARD_SECONDS = 0.6;
/** The synthesised blast is off for now: the explosion plays on the music alone. */
const IS_BLAST_SOUND_ON = false;

const readDebugTime = (): number | null => {
  const raw = new URLSearchParams(window.location.search).get("t");

  return raw === null ? null : Number(raw);
};

/**
 * Owns the timeline of the page. The music owns the time, the engine follows the
 * music, and this component reads the engine to show the HUD.
 */
const App: FC = () => {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const engineRef = useRef<TEngine | null>(null);
  const sound = useMemo(() => createSound(), []);
  const [isHudVisible, setHudVisible] = useState(false);
  const [isGateVisible, setGateVisible] = useState(false);
  const [isMuted, setMuted] = useState(false);
  const hudVisibleRef = useRef(false);
  const gateVisibleRef = useRef(false);

  const scoreIntro = useCallback(() => {
    sound.startMusic(0);
    sound.rumble(TIMELINE.crackStart, TIMELINE.inhale);
    if (IS_BLAST_SOUND_ON) {
      sound.blast(TIMELINE.detonate);
    }
  }, [sound]);

  const begin = useCallback((engine: TEngine) => {
    gateVisibleRef.current = false;
    setGateVisible(false);
    scoreIntro();
    engine.setTime(0);
    engine.start();
  }, [scoreIntro]);

  const showHud = useCallback(() => {
    hudVisibleRef.current = true;
    setHudVisible(true);
  }, []);

  const skip = useCallback(() => {
    const engine = engineRef.current;
    if (!engine || engine.getTime() >= SKIP_TO || engine.getTime() < SKIP_GUARD_SECONDS) {
      return;
    }

    sound.silenceIntro();
    sound.startMusic(SKIP_TO);
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
    sound.silenceIntro();
    scoreIntro();
    engine.setTime(0);
  }, [scoreIntro, sound]);

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

    void Promise.all([createEngine({ canvas, isCalm }), sound.prepare()]).then(([engine]) => {
      if (isDisposed) {
        engine.dispose();

        return;
      }

      engineRef.current = engine;
      engine.setClock(sound.musicTime);
      Object.assign(window, { __engine: engine, __showHud: showHud, __sound: sound });
      if (debugTime !== null) {
        engine.freeze(debugTime);

        return;
      }

      if (isCalm) {
        engine.setTime(SKIP_TO + 6);
        engine.start();
      } else if (sound.isRunning()) {
        begin(engine);
      } else {
        gateVisibleRef.current = true;
        setGateVisible(true);
      }
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
  }, [begin, showHud, sound]);

  // The first gesture unlocks the audio and starts the intro. Later a gesture skips the intro.
  useEffect(() => {
    const onGesture = (event: Event): void => {
      if (event instanceof KeyboardEvent && (event.metaKey || event.ctrlKey || event.altKey || event.repeat)) {
        return;
      }

      sound.unlock();
      const engine = engineRef.current;
      if (gateVisibleRef.current && engine) {
        begin(engine);
      } else if (!hudVisibleRef.current) {
        skip();
      } else if (engine && !sound.isMusicOn()) {
        sound.startMusic(engine.getTime());
      }
    };

    window.addEventListener("keydown", onGesture);
    window.addEventListener("pointerdown", onGesture);

    return () => {
      window.removeEventListener("keydown", onGesture);
      window.removeEventListener("pointerdown", onGesture);
    };
  }, [begin, skip, sound]);

  return (
    <main className="stage">
      <canvas ref={canvasRef} className="stage__canvas" />

      <p className={`gate ${isGateVisible ? "gate--visible" : ""}`}>{"Нажмите любую клавишу"}</p>

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
