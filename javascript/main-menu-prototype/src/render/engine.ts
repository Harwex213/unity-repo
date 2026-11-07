import {
  AdditiveBlending, BufferAttribute, BufferGeometry, Camera, ClampToEdgeWrapping, CustomBlending, Data3DTexture, 
  DepthTexture, FloatType, GLSL3, HalfFloatType, IcosahedronGeometry, InstancedBufferAttribute, InstancedBufferGeometry,
  LinearFilter, LinearMipmapLinearFilter, Matrix4, Mesh, MirroredRepeatWrapping, NearestFilter, NoBlending,
  OneFactor, OneMinusSrcAlphaFactor, PerspectiveCamera, PlaneGeometry, Quaternion, RawShaderMaterial, RepeatWrapping, RGBAFormat, RGFormat, Scene,
  SRGBColorSpace, TextureLoader, UnsignedByteType, Vector2, Vector3, WebGLRenderer, WebGLRenderTarget,
} from "three";
import albedoSrc from "../assets/planet-albedo.webp";
import cloudsSrc from "../assets/planet-clouds.webp";
import islandSrc from "../assets/island.webp";
import lavaSrc from "../assets/lava.webp";
import maskSrc from "../assets/planet-mask.webp";
import nebulaSrc from "../assets/nebula.webp";
import noiseSrc from "../assets/noise3d.bin";
import rockSrc from "../assets/rock.webp";
import { TIMELINE, menuShift, sceneAt, smooth } from "../core/timeline";
import { createPlanetGeometry, createRandom } from "./fracture";
import { BACKGROUND_FRAGMENT, BACKGROUND_VERTEX } from "./shaders/background";
import { HEADER } from "./shaders/common";
import {
  EMBER_FRAGMENT, EMBER_VERTEX, ROCK_FRAGMENT, ROCK_VERTEX, SPARK_FRAGMENT, SPARK_VERTEX,
} from "./shaders/debris";
import { PLANET_FRAGMENT, PLANET_VERTEX } from "./shaders/planet";
import {
  BRIGHT_FRAGMENT, DOWN_FRAGMENT, FINAL_FRAGMENT, MERGE_FRAGMENT, POST_VERTEX, SHAFT_FRAGMENT, STREAK_FRAGMENT, UP_FRAGMENT,
} from "./shaders/post";
import { VOLUME_FRAGMENT, VOLUME_VERTEX } from "./shaders/volume";
import type { Texture } from "three";
import type { TVec3 } from "../core/timeline";

const BLOOM_LEVELS = 7;
const ROCK_COUNT = 5200;
const SPARK_COUNT = 13000;
const EMBER_COUNT = 460;
const NOISE_SIZE = 128;
const MAX_PIXELS = 2560 * 1440;
const SPIN_SPEED = 0.028;
/** The world direction of the first fissure. It looks at the camera at the moment of the blast. */
const EPICENTRE_WORLD = new Vector3(0.2, 0.12, 0.97).normalize();
const SPIN_AXIS = new Vector3(0.3, 0.9, 0.31).normalize();
const SUN_DIRECTION = new Vector3(-0.9, 0.3, 0.16).normalize();

type TEngineOptions = {
  readonly canvas: HTMLCanvasElement;
  /** Reduced motion: no flash and no shake, the engine starts in the menu. */
  readonly isCalm: boolean;
};

type TEngine = {
  readonly start: () => void;
  readonly setTime: (seconds: number) => void;
  readonly freeze: (seconds: number) => void;
  readonly getTime: () => number;
  readonly setPointer: (x: number, y: number) => void;
  readonly dispose: () => void;
};

type TUniform<T> = { value: T };

const loadTexture = async (src: string, isColor: boolean, wrap: "repeat" | "mirror" | "clamp"): Promise<Texture> => {
  const texture = await new TextureLoader().loadAsync(src);
  if (isColor) {
    // The shaders do the conversion, the raw values are needed for the masks.
    texture.colorSpace = "";
  }
  const wrapping = wrap === "repeat" ? RepeatWrapping : wrap === "mirror" ? MirroredRepeatWrapping : ClampToEdgeWrapping;
  texture.wrapS = wrapping;
  texture.wrapT = wrap === "repeat" ? ClampToEdgeWrapping : wrapping;
  texture.minFilter = LinearMipmapLinearFilter;
  texture.magFilter = LinearFilter;
  texture.generateMipmaps = true;
  texture.anisotropy = 8;

  return texture;
};

const loadNoise = async (): Promise<Data3DTexture> => {
  const response = await fetch(noiseSrc);
  const bytes = new Uint8Array(await response.arrayBuffer());
  const texture = new Data3DTexture(bytes, NOISE_SIZE, NOISE_SIZE, NOISE_SIZE);
  texture.format = RGFormat;
  texture.type = UnsignedByteType;
  texture.minFilter = LinearFilter;
  texture.magFilter = LinearFilter;
  texture.wrapS = RepeatWrapping;
  texture.wrapT = RepeatWrapping;
  texture.wrapR = RepeatWrapping;
  texture.unpackAlignment = 1;
  texture.needsUpdate = true;

  return texture;
};

const planetRotation = (time: number): Quaternion => {
  const tilt = new Quaternion().setFromUnitVectors(new Vector3(0, 1, 0), SPIN_AXIS);
  // The field of debris stops turning soon after the blast: the gap toward the star must stay open.
  const blast = TIMELINE.detonate;
  const turned = time < blast ? time : blast + (1 - Math.exp(blast - time));
  const spin = new Quaternion().setFromAxisAngle(new Vector3(0, 1, 0), turned * SPIN_SPEED + 3.75);

  return tilt.multiply(spin);
};

const createRawMaterial = (
  vertexShader: string,
  fragmentShader: string,
  uniforms: Record<string, TUniform<unknown>>,
): RawShaderMaterial =>
  new RawShaderMaterial({
    glslVersion: GLSL3,
    vertexShader,
    fragmentShader,
    uniforms,
    depthTest: false,
    depthWrite: false,
    blending: NoBlending,
  });

const createInstances = (base: BufferGeometry, count: number, seed: number): InstancedBufferGeometry => {
  const random = createRandom(seed);
  const direction = new Float32Array(count * 3);
  const values = new Float32Array(count * 4);
  for (let i = 0; i < count; i += 1) {
    let x = 0;
    let y = 0;
    let z = 0;
    let length = 0;
    do {
      x = random() * 2 - 1;
      y = random() * 2 - 1;
      z = random() * 2 - 1;
      length = Math.hypot(x, y, z);
    } while (length > 1 || length < 0.05);
    direction.set([x / length, y / length, z / length], i * 3);
    values.set([random(), random(), random(), random()], i * 4);
  }

  const geometry = new InstancedBufferGeometry();
  geometry.setAttribute("position", base.getAttribute("position"));
  if (base.index) {
    geometry.setIndex(base.index);
  }
  geometry.setAttribute("iDirection", new InstancedBufferAttribute(direction, 3));
  geometry.setAttribute("iRand", new InstancedBufferAttribute(values, 4));
  geometry.instanceCount = count;

  return geometry;
};

const createRockShape = (): BufferGeometry => {
  const geometry = new IcosahedronGeometry(1, 1);
  const position = geometry.getAttribute("position");
  for (let i = 0; i < position.count; i += 1) {
    const x = position.getX(i);
    const y = position.getY(i);
    const z = position.getZ(i);
    // The same noise for equal corners, so the non-indexed faces stay closed.
    const n = 1 + 0.42 * Math.sin(x * 5.3 + y * 3.1) * Math.sin(y * 4.7 + z * 6.1) + 0.2 * Math.sin(z * 9.1 + x * 7.7);
    position.setXYZ(i, x * n, y * n, z * n);
  }

  return geometry;
};

const createFullscreenGeometry = (): BufferGeometry => {
  const geometry = new BufferGeometry();
  geometry.setAttribute("position", new BufferAttribute(new Float32Array([-1, -1, 0, 3, -1, 0, -1, 3, 0]), 3));

  return geometry;
};

const hdrTarget = (width: number, height: number): WebGLRenderTarget =>
  new WebGLRenderTarget(width, height, {
    type: HalfFloatType,
    format: RGBAFormat,
    minFilter: LinearFilter,
    magFilter: LinearFilter,
    wrapS: ClampToEdgeWrapping,
    wrapT: ClampToEdgeWrapping,
    depthBuffer: false,
    generateMipmaps: false,
  });

const createEngine = async ({ canvas, isCalm }: TEngineOptions): Promise<TEngine> => {
  const renderer = new WebGLRenderer({
    canvas,
    antialias: false,
    alpha: false,
    depth: false,
    stencil: false,
    powerPreference: "high-performance",
  });
  renderer.autoClear = false;
  renderer.outputColorSpace = SRGBColorSpace;
  renderer.setClearColor(0x000000, 1);

  const [albedo, mask, clouds, nebula, rock, lava, island, noise] = await Promise.all([
    loadTexture(albedoSrc, true, "repeat"),
    loadTexture(maskSrc, true, "repeat"),
    loadTexture(cloudsSrc, true, "repeat"),
    loadTexture(nebulaSrc, true, "mirror"),
    loadTexture(rockSrc, true, "mirror"),
    loadTexture(lavaSrc, true, "mirror"),
    loadTexture(islandSrc, true, "clamp"),
    loadNoise(),
  ]);
  island.premultiplyAlpha = true;

  const blastRotation = planetRotation(1e4);
  const epicentre = EPICENTRE_WORLD.clone().applyQuaternion(blastRotation.clone().invert());
  const guardDir = new Vector3(0, 0, 1).applyQuaternion(blastRotation.clone().invert());

  const shared = {
    uTime: { value: 0 },
    uDetonate: { value: TIMELINE.detonate },
    uEpicentre: { value: epicentre },
    uGuardDir: { value: guardDir },
    uCrackStart: { value: TIMELINE.crackStart },
    uInhale: { value: TIMELINE.inhale },
    uSunDirection: { value: SUN_DIRECTION },
    uCameraPosition: { value: new Vector3() },
    uCoreLight: { value: 0 },
    uFireLight: { value: 0 },
    uResolution: { value: new Vector2(1, 1) },
    uInverseViewProjection: { value: new Matrix4() },
    tNoise: { value: noise },
    tRock: { value: rock },
  };

  const camera = new PerspectiveCamera(28, 1, 0.1, 400);
  const scene = new Scene();
  const background = new Scene();
  const fullscreen = createFullscreenGeometry();
  const fullscreenCamera = new Camera();

  const backgroundMesh = new Mesh(fullscreen, createRawMaterial(BACKGROUND_VERTEX, BACKGROUND_FRAGMENT, {
    tNebula: { value: nebula },
    uInverseViewProjection: shared.uInverseViewProjection,
    uCameraPosition: shared.uCameraPosition,
    uTime: shared.uTime,
  }));
  backgroundMesh.frustumCulled = false;
  background.add(backgroundMesh);

  const planetMaterial = new RawShaderMaterial({
    glslVersion: GLSL3,
    vertexShader: PLANET_VERTEX,
    fragmentShader: PLANET_FRAGMENT,
    uniforms: {
      ...shared,
      tAlbedo: { value: albedo },
      tMask: { value: mask },
      tClouds: { value: clouds },
      tLava: { value: lava },
    },
  });
  const planet = new Mesh(createPlanetGeometry([epicentre.x, epicentre.y, epicentre.z]), planetMaterial);
  planet.frustumCulled = false;
  scene.add(planet);

  const rocks = new Mesh(
    createInstances(createRockShape(), ROCK_COUNT, 77),
    new RawShaderMaterial({ glslVersion: GLSL3, vertexShader: ROCK_VERTEX, fragmentShader: ROCK_FRAGMENT, uniforms: shared }),
  );
  rocks.frustumCulled = false;
  scene.add(rocks);

  const quad = new PlaneGeometry(2, 2);
  const sparks = new Mesh(
    createInstances(quad, SPARK_COUNT, 91),
    new RawShaderMaterial({
      glslVersion: GLSL3,
      vertexShader: SPARK_VERTEX,
      fragmentShader: SPARK_FRAGMENT,
      uniforms: shared,
      transparent: true,
      blending: AdditiveBlending,
      depthWrite: false,
    }),
  );
  sparks.frustumCulled = false;
  sparks.renderOrder = 3;
  scene.add(sparks);

  const emberUniforms = { uEmbers: { value: 0 } };
  const embers = new Mesh(
    createInstances(quad, EMBER_COUNT, 113),
    new RawShaderMaterial({
      glslVersion: GLSL3,
      vertexShader: EMBER_VERTEX,
      fragmentShader: EMBER_FRAGMENT,
      uniforms: { ...shared, ...emberUniforms },
      transparent: true,
      blending: AdditiveBlending,
      depthWrite: false,
    }),
  );
  embers.frustumCulled = false;
  embers.renderOrder = 3;
  scene.add(embers);

  // The island is a matte painting on a card that faces the camera. It writes depth,
  // so the gas passes behind it, and only the core light wraps around its edges.
  const islandUniforms = {
    tIsland: { value: island },
    uIslandLight: { value: 0 },
    uIslandReveal: { value: 0 },
  };
  const islandMaterial = new RawShaderMaterial({
    glslVersion: GLSL3,
    vertexShader: HEADER + /* glsl */ `
      in vec3 position;
      in vec2 uv;
      uniform mat4 modelViewMatrix;
      uniform mat4 projectionMatrix;
      out vec2 vUv;
      void main() {
        vUv = uv;
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }
    `,
    fragmentShader: HEADER + /* glsl */ `
      in vec2 vUv;
      out vec4 outColor;
      uniform sampler2D tIsland;
      uniform float uIslandLight;
      uniform float uIslandReveal;
      void main() {
        vec4 image = texture(tIsland, vUv);
        float alpha = image.a * uIslandReveal;
        if (alpha < 0.03) {
          discard;
        }
        vec3 colour = pow(image.rgb / max(image.a, 0.01), vec3(2.2));
        float luma = dot(colour, vec3(0.3, 0.6, 0.1));
        vec3 shadow = colour * vec3(0.5, 0.6, 0.86) * 0.7;
        vec3 lit = colour * vec3(1.15, 0.92, 0.72) * (1.0 + smoothstep(0.1, 0.5, luma) * 1.2);
        vec3 shaded = mix(shadow, lit, smoothstep(0.03, 0.34, luma));
        outColor = vec4(shaded * uIslandLight * alpha, alpha);
      }
    `,
    uniforms: islandUniforms,
    transparent: true,
    depthWrite: true,
    blending: CustomBlending,
    blendSrc: OneFactor,
    blendDst: OneMinusSrcAlphaFactor,
  });
  const islandImage = island.image as HTMLImageElement;
  const islandAspect = islandImage.width / islandImage.height;
  const islandCard = new Mesh(new PlaneGeometry(islandAspect, 1), islandMaterial);
  islandCard.frustumCulled = false;
  islandCard.renderOrder = 2;
  scene.add(islandCard);

  const volumeUniforms = {
    tDepth: { value: null as Texture | null },
    tNoise: shared.tNoise,
    uInverseViewProjection: shared.uInverseViewProjection,
    uCameraPosition: shared.uCameraPosition,
    uSunDirection: shared.uSunDirection,
    uRingAxis: { value: SPIN_AXIS },
    uTime: shared.uTime,
    uTau: { value: 0 },
    uAir: { value: 1 },
    uCoreRadius: { value: 0 },
    uCoreGlow: { value: 0 },
  };
  const volumeMaterial = createRawMaterial(VOLUME_VERTEX, VOLUME_FRAGMENT, volumeUniforms);

  const mergeUniforms = { tScene: { value: null as Texture | null }, tVolume: { value: null as Texture | null } };
  const mergeMaterial = createRawMaterial(POST_VERTEX, MERGE_FRAGMENT, mergeUniforms);

  const downUniforms = {
    tSource: { value: null as Texture | null },
    uTexel: { value: new Vector2() },
    uFirst: { value: 0 },
  };
  const downMaterial = createRawMaterial(POST_VERTEX, DOWN_FRAGMENT, downUniforms);
  const upUniforms = {
    tSource: { value: null as Texture | null },
    uTexel: { value: new Vector2() },
    uRadius: { value: 1.4 },
  };
  const upMaterial = createRawMaterial(POST_VERTEX, UP_FRAGMENT, upUniforms);
  upMaterial.blending = CustomBlending;
  upMaterial.blendSrc = OneFactor;
  upMaterial.blendDst = OneFactor;

  const brightUniforms = { tSource: { value: null as Texture | null } };
  const brightMaterial = createRawMaterial(POST_VERTEX, BRIGHT_FRAGMENT, brightUniforms);
  const shaftUniforms = {
    tSource: { value: null as Texture | null },
    uLight: { value: new Vector2(0.5, 0.5) },
    uSpread: { value: 0.92 },
  };
  const shaftMaterial = createRawMaterial(POST_VERTEX, SHAFT_FRAGMENT, shaftUniforms);
  const streakUniforms = {
    tSource: { value: null as Texture | null },
    uTexel: { value: new Vector2() },
    uStride: { value: 1 },
  };
  const streakMaterial = createRawMaterial(POST_VERTEX, STREAK_FRAGMENT, streakUniforms);

  const finalUniforms = {
    tScene: { value: null as Texture | null },
    tBloom: { value: null as Texture | null },
    tShafts: { value: null as Texture | null },
    tStreak: { value: null as Texture | null },
    uLight: shaftUniforms.uLight,
    uResolution: shared.uResolution,
    uAspect: { value: 1 },
    uTime: shared.uTime,
    uFade: { value: 0 },
    uExposure: { value: 1 },
    uFlash: { value: 0 },
    uAberration: { value: 1 },
    uShockRadius: { value: 0 },
    uShockStrength: { value: 0 },
    uShafts: { value: 0 },
  };
  const finalMaterial = createRawMaterial(POST_VERTEX, FINAL_FRAGMENT, finalUniforms);

  const passMesh = new Mesh(fullscreen, mergeMaterial);
  passMesh.frustumCulled = false;
  const passScene = new Scene();
  passScene.add(passMesh);

  const runPass = (material: RawShaderMaterial, target: WebGLRenderTarget | null): void => {
    passMesh.material = material;
    renderer.setRenderTarget(target);
    renderer.render(passScene, fullscreenCamera);
  };

  type TTargets = {
    readonly scene: WebGLRenderTarget;
    readonly volume: WebGLRenderTarget;
    readonly merged: WebGLRenderTarget;
    readonly down: WebGLRenderTarget[];
    readonly bright: WebGLRenderTarget;
    readonly shaftA: WebGLRenderTarget;
    readonly shaftB: WebGLRenderTarget;
    readonly streakA: WebGLRenderTarget;
    readonly streakB: WebGLRenderTarget;
  };

  let targets: TTargets | null = null;
  let renderWidth = 0;
  let renderHeight = 0;
  let renderScale = 1;

  const disposeTargets = (): void => {
    if (!targets) {
      return;
    }

    targets.scene.depthTexture?.dispose();
    targets.scene.dispose();
    targets.volume.dispose();
    targets.merged.dispose();
    targets.down.forEach((target) => target.dispose());
    targets.bright.dispose();
    targets.shaftA.dispose();
    targets.shaftB.dispose();
    targets.streakA.dispose();
    targets.streakB.dispose();
  };

  const createTargets = (width: number, height: number): TTargets => {
    const main = new WebGLRenderTarget(width, height, {
      type: HalfFloatType,
      format: RGBAFormat,
      minFilter: LinearFilter,
      magFilter: LinearFilter,
      samples: 4,
      depthBuffer: true,
    });
    main.depthTexture = new DepthTexture(width, height, FloatType);
    main.depthTexture.minFilter = NearestFilter;
    main.depthTexture.magFilter = NearestFilter;

    const half = (value: number): number => Math.max(1, Math.floor(value / 2));
    const quarter = (value: number): number => Math.max(1, Math.floor(value / 4));
    const down: WebGLRenderTarget[] = [];
    let levelWidth = width;
    let levelHeight = height;
    for (let level = 0; level < BLOOM_LEVELS; level += 1) {
      levelWidth = half(levelWidth);
      levelHeight = half(levelHeight);
      down.push(hdrTarget(levelWidth, levelHeight));
    }

    return {
      scene: main,
      volume: hdrTarget(Math.floor(width * 0.6), Math.floor(height * 0.6)),
      merged: hdrTarget(width, height),
      down,
      bright: hdrTarget(half(width), half(height)),
      shaftA: hdrTarget(quarter(width), quarter(height)),
      shaftB: hdrTarget(quarter(width), quarter(height)),
      streakA: hdrTarget(quarter(width), Math.max(1, Math.floor(height / 8))),
      streakB: hdrTarget(quarter(width), Math.max(1, Math.floor(height / 8))),
    };
  };

  const fit = (): void => {
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    if (width === 0 || height === 0) {
      return;
    }

    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const scale = Math.min(dpr, Math.sqrt(MAX_PIXELS / (width * height))) * renderScale;
    const nextWidth = Math.round(width * scale);
    const nextHeight = Math.round(height * scale);
    if (nextWidth === renderWidth && nextHeight === renderHeight) {
      return;
    }

    renderWidth = nextWidth;
    renderHeight = nextHeight;
    renderer.setPixelRatio(1);
    renderer.setSize(nextWidth, nextHeight, false);
    disposeTargets();
    targets = createTargets(nextWidth, nextHeight);
    shared.uResolution.value.set(nextWidth, nextHeight);
  };

  const pointer = { x: 0, y: 0, targetX: 0, targetY: 0 };
  const lightScreen = new Vector3();
  const islandForward = new Vector3();
  const islandRight = new Vector3();
  const islandUp = new Vector3();
  let time = 0;

  const placeIsland = (seconds: number, aspect: number, shotPosition: TVec3, shotTarget: TVec3): void => {
    const shift = menuShift(aspect) / 2.3;
    const reveal = smooth(TIMELINE.islandReveal, TIMELINE.explosionEnd + 0.6, seconds);
    const position = new Vector3(...shotPosition);
    const target = new Vector3(...shotTarget);
    islandForward.subVectors(target, position).normalize();
    islandRight.crossVectors(islandForward, new Vector3(0, 1, 0)).normalize();
    islandUp.crossVectors(islandRight, islandForward).normalize();

    // The island rises out of the dust into its place under the star.
    const distance = 7.1;
    const rise = (1 - reveal) * -0.5;
    const bob = Math.sin(seconds * 0.36) * 0.022 + Math.sin(seconds * 0.17 + 1) * 0.03;
    const across = 0.28 * shift + 0.03;
    const tall = 1 - smooth(0.8, 1.1, aspect);
    const down = -0.68 + tall * 1.14 + bob + rise;
    const size = 1.04 + smooth(0.7, 1.5, aspect) * 0.68;

    islandCard.position.copy(position)
      .addScaledVector(islandForward, distance)
      .addScaledVector(islandRight, across)
      .addScaledVector(islandUp, down);
    islandCard.quaternion.setFromRotationMatrix(new Matrix4().makeBasis(islandRight, islandUp, islandForward.clone().negate()));
    islandCard.rotateZ(Math.sin(seconds * 0.11) * 0.012 - 0.02);
    islandCard.scale.setScalar(size);
    const opacity = smooth(0, 0.7, reveal);
    islandUniforms.uIslandReveal.value = opacity;
    // While it is half transparent the island lets the gas pass in front of it.
    islandMaterial.depthWrite = opacity >= 1;
  };

  const renderFrame = (seconds: number): void => {
    fit();
    if (!targets) {
      return;
    }

    pointer.x += (pointer.targetX - pointer.x) * 0.05;
    pointer.y += (pointer.targetY - pointer.y) * 0.05;

    const aspect = renderWidth / renderHeight;
    const state = sceneAt(seconds, aspect, pointer.x, pointer.y);
    const tau = seconds - TIMELINE.detonate;
    const sinceBlast = Math.max(tau, 0);

    camera.aspect = aspect;
    camera.fov = state.fov;
    camera.position.set(...state.cameraPosition);
    camera.up.set(Math.sin(state.roll), Math.cos(state.roll), 0);
    camera.lookAt(...state.cameraTarget);
    camera.updateProjectionMatrix();
    camera.updateMatrixWorld();

    const shotState = sceneAt(1e4, aspect, 0, 0);
    placeIsland(seconds, aspect, shotState.cameraPosition, shotState.cameraTarget);

    planet.quaternion.copy(planetRotation(seconds));
    planet.updateMatrixWorld();
    rocks.quaternion.copy(planet.quaternion);
    sparks.quaternion.copy(planet.quaternion);

    shared.uTime.value = seconds;
    shared.uCameraPosition.value.copy(camera.position);
    shared.uInverseViewProjection.value
      .copy(camera.projectionMatrixInverse)
      .premultiply(camera.matrixWorld);

    const coreLight = tau > 0 ? 8 + 40 * Math.exp(-sinceBlast * 0.9) : 0;
    shared.uCoreLight.value = coreLight;
    shared.uFireLight.value = tau > 0 ? 6 * Math.exp(-sinceBlast * 1.6) : 0;

    volumeUniforms.uTau.value = tau;
    volumeUniforms.uAir.value = tau > 0 ? Math.exp(-sinceBlast * 6) : 1;
    volumeUniforms.uCoreRadius.value = tau > 0 ? 0.2 + 0.34 * Math.exp(-sinceBlast * 0.7) : 0;
    volumeUniforms.uCoreGlow.value = tau > 0 ? 1 + 3 * Math.exp(-sinceBlast * 1.15) : 0;

    islandUniforms.uIslandLight.value = 1;
    emberUniforms.uEmbers.value = smooth(TIMELINE.detonate + 1.5, TIMELINE.explosionEnd + 2, seconds);

    const main = targets;

    // 1. Opaque scene.
    renderer.setRenderTarget(main.scene);
    renderer.clear(true, true, false);
    renderer.render(background, fullscreenCamera);
    renderer.render(scene, camera);

    // 2. Gas.
    volumeUniforms.tDepth.value = main.scene.depthTexture;
    runPass(volumeMaterial, main.volume);

    mergeUniforms.tScene.value = main.scene.texture;
    mergeUniforms.tVolume.value = main.volume.texture;
    runPass(mergeMaterial, main.merged);

    // 3. Bloom pyramid.
    let source = main.merged;
    for (let level = 0; level < BLOOM_LEVELS; level += 1) {
      const target = main.down[level]!;
      downUniforms.tSource.value = source.texture;
      downUniforms.uTexel.value.set(1 / source.width, 1 / source.height);
      downUniforms.uFirst.value = level === 0 ? 1 : 0;
      runPass(downMaterial, target);
      source = target;
    }
    for (let level = BLOOM_LEVELS - 1; level > 0; level -= 1) {
      const from = main.down[level]!;
      upUniforms.tSource.value = from.texture;
      upUniforms.uTexel.value.set(1 / from.width, 1 / from.height);
      runPass(upMaterial, main.down[level - 1]!);
    }

    // 4. Light shafts from the core, through the gaps between the shards.
    const coreOnScreen = lightScreen.set(0, 0, 0).project(camera);
    shaftUniforms.uLight.value.set(coreOnScreen.x * 0.5 + 0.5, coreOnScreen.y * 0.5 + 0.5);
    brightUniforms.tSource.value = main.merged.texture;
    runPass(brightMaterial, main.bright);

    shaftUniforms.uSpread.value = 0.88;
    shaftUniforms.tSource.value = main.bright.texture;
    runPass(shaftMaterial, main.shaftA);
    shaftUniforms.uSpread.value = 0.42;
    shaftUniforms.tSource.value = main.shaftA.texture;
    runPass(shaftMaterial, main.shaftB);
    shaftUniforms.uSpread.value = 0.2;
    shaftUniforms.tSource.value = main.shaftB.texture;
    runPass(shaftMaterial, main.shaftA);

    // 5. Streak of the anamorphic lens.
    streakUniforms.uTexel.value.set(1 / main.streakA.width, 1 / main.streakA.height);
    streakUniforms.tSource.value = main.bright.texture;
    streakUniforms.uStride.value = 1;
    runPass(streakMaterial, main.streakA);
    streakUniforms.tSource.value = main.streakA.texture;
    streakUniforms.uStride.value = 4;
    runPass(streakMaterial, main.streakB);
    streakUniforms.tSource.value = main.streakB.texture;
    streakUniforms.uStride.value = 16;
    runPass(streakMaterial, main.streakA);

    // 6. Grade.
    finalUniforms.tScene.value = main.merged.texture;
    finalUniforms.tBloom.value = main.down[0]!.texture;
    finalUniforms.tShafts.value = main.shaftA.texture;
    finalUniforms.tStreak.value = main.streakA.texture;
    finalUniforms.uAspect.value = aspect;
    finalUniforms.uFade.value = state.fade;
    finalUniforms.uExposure.value = state.exposure;
    finalUniforms.uFlash.value = isCalm ? 0 : state.flash;
    finalUniforms.uAberration.value = state.aberration;
    finalUniforms.uShafts.value = tau > 0 ? state.shafts : 0;
    finalUniforms.uShockRadius.value = sinceBlast * 1.55;
    finalUniforms.uShockStrength.value = tau > 0 ? 0.03 * Math.exp(-sinceBlast * 1.1) : 0;
    runPass(finalMaterial, null);
  };

  // All programs compile on the first frame, while the screen is still black.
  renderFrame(0);
  renderFrame(TIMELINE.detonate + 1);

  let frameId = 0;
  let isRunning = false;
  let isFrozen = false;
  let lastNow = 0;
  let slowFrames = 0;

  const tick = (now: number): void => {
    frameId = requestAnimationFrame(tick);
    const delta = Math.min((now - lastNow) / 1000, 0.1);
    lastNow = now;
    if (!isFrozen) {
      time += delta;
    }

    // Keeps the frame rate on weak GPUs: the picture gets softer, never slower.
    slowFrames = delta > 1 / 40 ? slowFrames + 1 : Math.max(slowFrames - 0.25, 0);
    if (slowFrames > 40 && renderScale > 0.5) {
      renderScale = Math.max(renderScale * 0.85, 0.5);
      slowFrames = 0;
    }

    renderFrame(time);
  };

  const onHidden = (): void => {
    lastNow = performance.now();
  };
  document.addEventListener("visibilitychange", onHidden);

  return {
    start: () => {
      if (isRunning) {
        return;
      }

      isRunning = true;
      lastNow = performance.now();
      frameId = requestAnimationFrame(tick);
    },
    setTime: (seconds) => {
      time = seconds;
      isFrozen = false;
    },
    freeze: (seconds) => {
      time = seconds;
      isFrozen = true;
      renderFrame(seconds);
    },
    getTime: () => time,
    setPointer: (x, y) => {
      pointer.targetX = x;
      pointer.targetY = y;
    },
    dispose: () => {
      cancelAnimationFrame(frameId);
      document.removeEventListener("visibilitychange", onHidden);
      disposeTargets();
      renderer.dispose();
    },
  };
};

export type { TEngine };
export { createEngine };
