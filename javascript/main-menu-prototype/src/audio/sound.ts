import musicUrl from "./background-music.aac";

/**
 * The music is a file, the effects are synthesised. A browser opens the audio only
 * after a user gesture, so the intro waits for the first key press. The track is
 * also the clock of the intro: the picture follows `musicTime`, and the effects are
 * scheduled against the same zero, so all three stay locked together.
 */

type TSound = {
  /** Loads and decodes the track. Without the track the page still runs, with the effects only. */
  readonly prepare: () => Promise<void>;
  /** True when the browser already allows the audio to play. */
  readonly isRunning: () => boolean;
  readonly unlock: () => void;
  readonly setMuted: (isMuted: boolean) => void;
  readonly startMusic: (fromSeconds: number) => void;
  readonly isMusicOn: () => boolean;
  /** The position that the listener hears now, or null if no track plays. */
  readonly musicTime: () => number | null;
  /** The times are positions in the track. */
  readonly rumble: (from: number, to: number) => void;
  readonly blast: (at: number) => void;
  readonly hover: () => void;
  readonly confirm: () => void;
  readonly silenceIntro: () => void;
};

/** All parts of a run start this far ahead, so they start together. */
const LEAD_SECONDS = 0.08;
const MUSIC_LEVEL = 0.9;
/** The blast stays below full scale on top of the music: 1 clips the output. */
const EFFECTS_LEVEL = 0.55;
/** A jump in the track fades in, so the cut does not click. */
const SEEK_FADE_SECONDS = 0.25;

const createImpulse = (context: AudioContext, seconds: number, decay: number): AudioBuffer => {
  const length = Math.floor(context.sampleRate * seconds);
  const impulse = context.createBuffer(2, length, context.sampleRate);
  for (let channel = 0; channel < 2; channel += 1) {
    const data = impulse.getChannelData(channel);
    for (let i = 0; i < length; i += 1) {
      data[i] = (Math.random() * 2 - 1) * (1 - i / length) ** decay;
    }
  }

  return impulse;
};

const createNoise = (context: AudioContext, seconds: number): AudioBuffer => {
  const buffer = context.createBuffer(1, Math.floor(context.sampleRate * seconds), context.sampleRate);
  const data = buffer.getChannelData(0);
  // Brown noise: every sample walks from the one before, so the energy sits in the bass.
  let last = 0;
  for (let i = 0; i < data.length; i += 1) {
    last = (last + (Math.random() * 2 - 1) * 0.02) / 1.02;
    data[i] = last * 3.5;
  }

  return buffer;
};

const createSound = (): TSound => {
  let context: AudioContext | null = null;
  let master: GainNode | null = null;
  let hall: ConvolverNode | null = null;
  let noise: AudioBuffer | null = null;
  let introBus: GainNode | null = null;
  /** The scheduled parts of the intro. A skip has to cancel them before they play. */
  let introSources: AudioScheduledSourceNode[] = [];
  let musicBus: GainNode | null = null;
  let music: AudioBuffer | null = null;
  let musicSource: AudioBufferSourceNode | null = null;
  let musicFade: GainNode | null = null;
  /** The time of the audio context at the first sample of the track. */
  let musicZero = 0;
  let isMuted = false;

  const ensure = (): AudioContext => {
    if (context) {
      return context;
    }

    const created = new AudioContext();
    const limiter = created.createDynamicsCompressor();
    limiter.threshold.value = -9;
    limiter.ratio.value = 12;
    limiter.attack.value = 0.003;
    limiter.release.value = 0.25;
    limiter.connect(created.destination);

    master = created.createGain();
    master.gain.value = isMuted ? 0 : 0.8;
    master.connect(limiter);

    hall = created.createConvolver();
    hall.buffer = createImpulse(created, 5.5, 2.6);
    const hallGain = created.createGain();
    hallGain.gain.value = 0.32;
    hall.connect(hallGain).connect(master);

    introBus = created.createGain();
    introBus.gain.value = EFFECTS_LEVEL;
    introBus.connect(master);
    introBus.connect(hall);

    // The music skips the hall: the track has its own space.
    musicBus = created.createGain();
    musicBus.gain.value = MUSIC_LEVEL;
    musicBus.connect(master);

    noise = createNoise(created, 8);
    context = created;

    return created;
  };

  const startMusic = (fromSeconds: number): void => {
    const audio = ensure();
    if (!musicBus) {
      return;
    }

    const at = audio.currentTime + LEAD_SECONDS;
    musicZero = at - fromSeconds;
    if (musicSource && musicFade) {
      musicFade.gain.cancelScheduledValues(audio.currentTime);
      musicFade.gain.setTargetAtTime(0, audio.currentTime, 0.03);
      musicSource.stop(audio.currentTime + 0.2);
    }

    if (!music) {
      return;
    }

    const source = audio.createBufferSource();
    source.buffer = music;
    // The track ends in silence and starts with a quiet pad, so the whole track loops well.
    source.loop = true;
    const fade = audio.createGain();
    fade.gain.setValueAtTime(fromSeconds > 0 ? 0.0001 : 1, at);
    fade.gain.exponentialRampToValueAtTime(1, at + SEEK_FADE_SECONDS);
    source.connect(fade).connect(musicBus);
    source.start(at, fromSeconds % music.duration);
    musicSource = source;
    musicFade = fade;
  };

  const rumble = (from: number, to: number): void => {
    const audio = ensure();
    if (!noise || !introBus) {
      return;
    }

    const at = musicZero + from;
    const rise = to - from;
    const source = audio.createBufferSource();
    source.buffer = noise;
    const filter = audio.createBiquadFilter();
    filter.type = "lowpass";
    filter.frequency.setValueAtTime(70, at);
    filter.frequency.exponentialRampToValueAtTime(260, at + rise);
    const gain = audio.createGain();
    gain.gain.setValueAtTime(0.0001, at);
    gain.gain.exponentialRampToValueAtTime(1.3, at + rise);
    // The silence of the inhale: the rumble drops right before the blast.
    gain.gain.exponentialRampToValueAtTime(0.0001, at + rise + 0.32);
    source.connect(filter).connect(gain).connect(introBus);
    source.start(at);
    source.stop(at + rise + 0.45);
    introSources.push(source);
  };

  const blast = (position: number): void => {
    const audio = ensure();
    if (!noise || !introBus) {
      return;
    }

    const at = musicZero + position;

    const sub = audio.createOscillator();
    sub.type = "sine";
    sub.frequency.setValueAtTime(92, at);
    sub.frequency.exponentialRampToValueAtTime(24, at + 3.4);
    const subGain = audio.createGain();
    subGain.gain.setValueAtTime(0.0001, at);
    subGain.gain.exponentialRampToValueAtTime(1, at + 0.03);
    subGain.gain.exponentialRampToValueAtTime(0.0001, at + 5);
    sub.connect(subGain).connect(introBus);
    sub.start(at);
    sub.stop(at + 5.1);

    const crack = audio.createBufferSource();
    crack.buffer = noise;
    const crackFilter = audio.createBiquadFilter();
    crackFilter.type = "lowpass";
    crackFilter.frequency.setValueAtTime(5200, at);
    crackFilter.frequency.exponentialRampToValueAtTime(90, at + 4.6);
    const crackGain = audio.createGain();
    crackGain.gain.setValueAtTime(0.0001, at);
    crackGain.gain.exponentialRampToValueAtTime(1.8, at + 0.015);
    crackGain.gain.exponentialRampToValueAtTime(0.0001, at + 5.8);
    crack.connect(crackFilter).connect(crackGain).connect(introBus);
    crack.start(at);
    crack.stop(at + 6);
    introSources.push(sub, crack);
  };

  const ping = (frequency: number, level: number, seconds: number): void => {
    const audio = context;
    if (!audio || !master || !hall) {
      return;
    }

    const at = audio.currentTime;
    const gain = audio.createGain();
    gain.gain.setValueAtTime(0.0001, at);
    gain.gain.exponentialRampToValueAtTime(level, at + 0.008);
    gain.gain.exponentialRampToValueAtTime(0.0001, at + seconds);
    gain.connect(master);
    gain.connect(hall);
    [1, 2.76, 5.4].forEach((partial, index) => {
      const voice = audio.createOscillator();
      voice.type = "sine";
      voice.frequency.value = frequency * partial;
      const share = audio.createGain();
      share.gain.value = 1 / (index * 2 + 1);
      voice.connect(share).connect(gain);
      voice.start(at);
      voice.stop(at + seconds);
    });
  };

  return {
    prepare: async () => {
      const audio = ensure();
      try {
        const response = await fetch(musicUrl);
        music = await audio.decodeAudioData(await response.arrayBuffer());
      } catch (error) {
        console.warn("The music did not load", error);
      }
    },
    isRunning: () => context?.state === "running",
    unlock: () => {
      const audio = ensure();
      if (audio.state === "suspended") {
        void audio.resume();
      }
    },
    setMuted: (next) => {
      isMuted = next;
      if (master && context) {
        master.gain.setTargetAtTime(next ? 0 : 0.8, context.currentTime, 0.06);
      }
    },
    startMusic,
    isMusicOn: () => musicSource !== null,
    musicTime: () => {
      const audio = context;
      if (!audio || !musicSource) {
        return null;
      }

      return Math.max(audio.currentTime - musicZero - (audio.outputLatency || audio.baseLatency || 0), 0);
    },
    rumble,
    blast,
    hover: () => ping(660, 0.03, 0.5),
    confirm: () => {
      ping(165, 0.18, 2.6);
      ping(247, 0.1, 2.2);
    },
    silenceIntro: () => {
      const audio = context;
      const bus = introBus;
      if (!audio || !bus) {
        return;
      }

      // A short fade, so the cut does not click.
      bus.gain.setTargetAtTime(0, audio.currentTime, 0.03);
      const sources = introSources;
      introSources = [];
      window.setTimeout(() => {
        sources.forEach((source) => source.stop());
        bus.gain.setTargetAtTime(EFFECTS_LEVEL, audio.currentTime, 0.01);
      }, 160);
    },
  };
};

export type { TSound };
export { createSound };
