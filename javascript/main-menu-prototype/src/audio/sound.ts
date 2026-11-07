/**
 * All sound is synthesised, so the prototype has no audio files. A browser
 * opens the audio only after a user gesture. The first key press or click unlocks
 * the sound, so a first run may play the explosion silently. A replay has the full mix.
 */

type TSound = {
  readonly unlock: () => void;
  readonly setMuted: (isMuted: boolean) => void;
  readonly rumble: (delaySeconds: number) => void;
  readonly blast: (delaySeconds: number) => void;
  readonly drone: () => void;
  readonly hover: () => void;
  readonly confirm: () => void;
  readonly silenceIntro: () => void;
};

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
  let isDroneOn = false;
  let isMuted = false;

  const ensure = (): AudioContext | null => {
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
    introBus.connect(master);
    introBus.connect(hall);

    noise = createNoise(created, 8);
    context = created;

    return created;
  };

  const rumble = (delaySeconds: number): void => {
    const audio = ensure();
    if (!audio || !noise || !introBus || delaySeconds < 0) {
      return;
    }

    const at = audio.currentTime + delaySeconds;
    const source = audio.createBufferSource();
    source.buffer = noise;
    const filter = audio.createBiquadFilter();
    filter.type = "lowpass";
    filter.frequency.setValueAtTime(70, at);
    filter.frequency.exponentialRampToValueAtTime(260, at + 1.35);
    const gain = audio.createGain();
    gain.gain.setValueAtTime(0.0001, at);
    gain.gain.exponentialRampToValueAtTime(0.9, at + 1.3);
    // The silence of the inhale: the rumble drops right before the blast.
    gain.gain.exponentialRampToValueAtTime(0.0001, at + 1.65);
    source.connect(filter).connect(gain).connect(introBus);
    source.start(at);
    source.stop(at + 1.8);
    introSources.push(source);
  };

  const blast = (delaySeconds: number): void => {
    const audio = ensure();
    if (!audio || !noise || !introBus || delaySeconds < 0) {
      return;
    }

    const at = audio.currentTime + delaySeconds;

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

  /** A slow minor chord of filtered saws, close to a far choir. */
  const drone = (): void => {
    const audio = ensure();
    if (!audio || !master || !hall || isDroneOn) {
      return;
    }

    isDroneOn = true;
    const bus = audio.createGain();
    bus.gain.setValueAtTime(0.0001, audio.currentTime);
    bus.gain.exponentialRampToValueAtTime(0.1, audio.currentTime + 7);
    const filter = audio.createBiquadFilter();
    filter.type = "lowpass";
    filter.frequency.value = 520;
    filter.Q.value = 3;
    const sweep = audio.createOscillator();
    sweep.frequency.value = 0.045;
    const sweepDepth = audio.createGain();
    sweepDepth.gain.value = 240;
    sweep.connect(sweepDepth).connect(filter.frequency);
    sweep.start();

    [55, 82.41, 110, 130.81, 164.81].forEach((frequency, index) => {
      [-5, 6].forEach((detune) => {
        const voice = audio.createOscillator();
        voice.type = "sawtooth";
        voice.frequency.value = frequency;
        voice.detune.value = detune + index;
        const level = audio.createGain();
        level.gain.value = 0.2 / (index + 1.4);
        voice.connect(level).connect(filter);
        voice.start();
      });
    });

    filter.connect(bus);
    bus.connect(master);
    bus.connect(hall);
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
    unlock: () => {
      const audio = ensure();
      if (audio && audio.state === "suspended") {
        void audio.resume();
      }
    },
    setMuted: (next) => {
      isMuted = next;
      if (master && context) {
        master.gain.setTargetAtTime(next ? 0 : 0.8, context.currentTime, 0.06);
      }
    },
    rumble,
    blast,
    drone,
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
        bus.gain.setTargetAtTime(1, audio.currentTime, 0.01);
      }, 160);
    },
  };
};

export type { TSound };
export { createSound };
