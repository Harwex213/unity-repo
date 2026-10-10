import { createProgram, NOISE, QUAD_VERTEX, uniform } from "../cleanup/sky-gl";

/**
 * The main menu background in WebGL2: one full-screen pass over the hero art.
 * The art is cover-fitted. A grayscale mask marks the sky (white). The pass
 * adds the life on top of the still picture:
 * - slow drifting clouds and fog over the sky;
 * - soft god rays from the light in the upper left corner;
 * - a sway on the banners of the arch and a heat shimmer in the toxic haze;
 * - dust motes, embers and a few green spores;
 * - a slow "Ken Burns" zoom and a mouse parallax of a few pixels;
 * - a colour grade toward the navy and gold palette, and a vignette.
 * `uMotion` scales every movement. Reduced motion sets it to 0, and the frame
 * is drawn once.
 */

const FRAGMENT = /* glsl */ `#version 300 es
precision highp float;

uniform sampler2D uArt;
uniform sampler2D uMask;
uniform vec2 uResolution;
uniform float uArtAspect;
uniform float uTime;
uniform vec2 uMouse;
uniform float uMotion;

out vec4 outColor;

${NOISE}

// Places in the art, in its uv (origin top left). The sun sets in the upper
// left of the arch opening. A banner and a torch hang on each pillar. The
// toxic glow rises from the lands in the lower right of the opening.
const vec2 LIGHT = vec2(0.11, 0.17);
const vec2 LEFT_TORCH = vec2(0.035, 0.5);
const vec2 RIGHT_TORCH = vec2(0.732, 0.555);
const vec2 TOXIC_GLOW = vec2(0.6, 0.8);

/** The screen point in the art, with the art covering the whole screen. */
vec2 coverUv(vec2 screen) {
  float screenAspect = uResolution.x / uResolution.y;
  vec2 uv = screen - 0.5;
  if (screenAspect > uArtAspect) {
    uv.y *= uArtAspect / screenAspect;
  } else {
    uv.x *= screenAspect / uArtAspect;
  }
  return uv + 0.5;
}

/** Three layers of rising motes in screen space. */
vec3 motes(vec2 screen, float t) {
  vec3 sum = vec3(0.0);
  float aspect = uResolution.x / uResolution.y;

  for (int layer = 0; layer < 3; layer += 1) {
    float fl = float(layer);
    float cells = 9.0 + fl * 7.0;
    vec2 q = vec2(screen.x * aspect, screen.y) * cells;
    q += vec2(sin(t * 0.13 + fl * 2.1) * 0.6, t * (0.22 + fl * 0.1));

    vec2 cell = floor(q);
    vec2 inCell = fract(q);
    float seed = hash12(cell + fl * 31.7);
    if (seed > 0.42) {
      continue;
    }

    vec2 centre = 0.25 + 0.5 * vec2(hash12(cell + 3.1), hash12(cell + 7.9));
    centre.x += 0.18 * sin(t * (0.7 + seed * 1.3) + seed * 40.0);
    float radiusPx = (0.9 + 2.2 * hash12(cell + 11.3)) * (1.0 + fl * 0.35);
    float radius = radiusPx * cells / uResolution.y;
    vec2 delta = (inCell - centre) / radius;
    float glow = exp(-dot(delta, delta));
    float twinkle = 0.55 + 0.45 * sin(t * (1.1 + seed * 3.0) + seed * 60.0);

    float kind = hash12(cell + 17.0);
    vec3 tint = kind < 0.14
      ? vec3(0.55, 1.0, 0.35)
      : kind < 0.5 ? vec3(1.0, 0.62, 0.28) : vec3(1.0, 0.93, 0.8);
    float lower = kind < 0.5 ? 0.35 + 0.65 * smoothstep(0.15, 0.95, screen.y) : 0.75;

    sum += tint * glow * twinkle * lower * (0.5 + 0.2 * fl);
  }

  return sum;
}

void main() {
  float t = uTime;
  float motion = uMotion;
  // The screen with the origin in the top left corner, like the art.
  vec2 screen = vec2(gl_FragCoord.x / uResolution.x, 1.0 - gl_FragCoord.y / uResolution.y);
  vec2 uv = coverUv(screen);

  // Ken Burns: a slow breathing zoom and a slow pan.
  float zoom = 1.065 + 0.018 * sin(t * 0.06) * motion;
  vec2 pan = vec2(sin(t * 0.031), cos(t * 0.023)) * 0.005 * motion;
  uv = (uv - 0.5) / zoom + 0.5 + pan;

  // Parallax: the near stone moves more than the far sky.
  float skyNear = textureLod(uMask, uv, 3.0).r;
  uv += uMouse * mix(0.009, 0.003, skyNear) * motion;

  // The banners hang from the top of the pillars, so the sway grows toward
  // their lower ends.
  float leftBanner = smoothstep(0.02, 0.035, uv.x) * (1.0 - smoothstep(0.095, 0.11, uv.x));
  float rightBanner = smoothstep(0.682, 0.695, uv.x) * (1.0 - smoothstep(0.77, 0.785, uv.x));
  float banner = (leftBanner + rightBanner) * (1.0 - smoothstep(0.44, 0.5, uv.y)) * smoothstep(0.04, 0.42, uv.y) * (1.0 - skyNear);
  float sway = sin(uv.y * 26.0 - t * 1.6 + uv.x * 5.0) * 0.6 + sin(t * 0.8 + uv.y * 8.0) * 0.4;
  uv.x += sway * 0.0022 * banner * motion;

  // The heat shimmer over the toxic haze in the lower right of the opening.
  vec2 toGlow = (uv - TOXIC_GLOW) * vec2(uArtAspect * 0.8, 1.6);
  float haze = smoothstep(0.55, 0.0, length(toGlow));
  vec2 shimmer = vec2(
    valueNoise(uv * vec2(42.0, 16.0) + vec2(0.0, t * 1.1)),
    valueNoise(uv * vec2(36.0, 22.0) + vec2(5.3, t * 0.9))
  ) - 0.5;
  uv += shimmer * 0.003 * haze * motion;

  uv = clamp(uv, vec2(0.001), vec2(0.999));
  vec3 color = texture(uArt, uv).rgb;
  float sky = textureLod(uMask, uv, 1.0).r;

  // Two layers of clouds and fog drift over the sky.
  vec2 cloudPoint = vec2(uv.x * uArtAspect, uv.y) * 2.4;
  float far = fbm(cloudPoint * 1.2 + vec2(t * 0.010, t * 0.002));
  float near = fbm(cloudPoint * 2.7 + vec2(t * 0.024, -t * 0.003) + 3.7);
  float cloud = smoothstep(0.42, 0.86, far * 0.62 + near * 0.48);
  vec2 toLight = (uv - LIGHT) * vec2(uArtAspect, 1.0);
  float lightDistance = length(toLight);
  vec3 cloudColor = mix(vec3(0.58, 0.64, 0.72), vec3(1.0, 0.9, 0.72), exp(-lightDistance * 1.6));
  color = mix(color, cloudColor, cloud * sky * 0.38);
  // A thin mist hangs over the whole scene too.
  color = mix(color, cloudColor * 0.8, near * 0.06 * (1.0 - sky));

  // God rays: noisy streaks fanning out from the light.
  float angle = atan(toLight.y, toLight.x);
  float streaks = valueNoise(vec2(angle * 13.0, t * 0.07)) * valueNoise(vec2(angle * 29.0 + 4.0, -t * 0.05));
  float rays = pow(streaks, 1.4) * smoothstep(1.45, 0.05, lightDistance) * (0.35 + 0.65 * sky);
  color += vec3(1.0, 0.84, 0.58) * (rays * 0.28 + exp(-lightDistance * 2.6) * 0.1);

  // The toxic haze glows, pulses and drifts.
  float toxic = haze * fbm(vec2(uv.x * 5.0 + t * 0.03, uv.y * 3.0 - t * 0.012));
  color += vec3(0.32, 0.85, 0.22) * toxic * (0.12 + 0.04 * sin(t * 0.7)) * (0.4 + 0.6 * sky);

  // The torches flicker.
  vec2 toLeftTorch = (uv - LEFT_TORCH) * vec2(uArtAspect, 1.0);
  vec2 toRightTorch = (uv - RIGHT_TORCH) * vec2(uArtAspect, 1.0);
  float flickerLeft = 0.7 + 0.3 * valueNoise(vec2(t * 6.0, 1.3)) * motion;
  float flickerRight = 0.7 + 0.3 * valueNoise(vec2(t * 6.4, 7.9)) * motion;
  color += vec3(1.0, 0.55, 0.2) * (
    exp(-dot(toLeftTorch, toLeftTorch) * 260.0) * flickerLeft
    + exp(-dot(toRightTorch, toRightTorch) * 260.0) * flickerRight
  ) * 0.32;

  color += motes(screen, t);

  // The grade: navy shadows, warm gold highlights, a little more contrast.
  float luma = dot(color, vec3(0.299, 0.587, 0.114));
  color *= mix(vec3(0.8, 0.9, 1.1), vec3(1.07, 1.0, 0.88), smoothstep(0.15, 0.8, luma));
  color = mix(vec3(luma), color, 1.06);
  color = (color - 0.5) * 1.05 + 0.5;

  // The vignette.
  vec2 fromCentre = (screen - 0.5) * vec2(1.05, 1.25);
  color *= mix(0.5, 1.0, smoothstep(0.85, 0.25, length(fromCentre)));

  // Dither, against banding in the dark gradients.
  color += (hash12(gl_FragCoord.xy) - 0.5) / 255.0;

  outColor = vec4(max(color, 0.0), 1.0);
}
`;

/** A frame of the renderer: what changes between frames. */
type TMenuBackgroundFrame = {
  readonly timeSeconds: number;
  /** The pointer, from -1 to 1 on each axis, smoothed by the caller. */
  readonly mouseX: number;
  readonly mouseY: number;
  /** 1 for full motion, 0 for a still frame. */
  readonly motion: number;
};

type TMenuBackgroundRenderer = {
  /** Resolves once both images are on the GPU. */
  readonly ready: Promise<void>;
  readonly resize: (width: number, height: number) => void;
  readonly render: (frame: TMenuBackgroundFrame) => void;
  readonly dispose: () => void;
};

type TLoadedTexture = {
  readonly texture: WebGLTexture;
  readonly width: number;
  readonly height: number;
};

const loadTexture = (gl: WebGL2RenderingContext, src: string) =>
  new Promise<TLoadedTexture>((resolve, reject) => {
    const image = new Image();
    image.onload = () => {
      const texture = gl.createTexture();
      gl.bindTexture(gl.TEXTURE_2D, texture);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
      gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, image);
      gl.generateMipmap(gl.TEXTURE_2D);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
      resolve({ texture, width: image.naturalWidth, height: image.naturalHeight });
    };
    image.onerror = () => reject(new Error(`Could not load ${src}`));
    image.src = src;
  });

/**
 * Creates the renderer on the canvas, or returns `null` when WebGL2 or the
 * shader is not available. The caller then keeps the CSS background.
 */
const createMenuBackgroundRenderer = (
  canvas: HTMLCanvasElement,
  artSrc: string,
  maskSrc: string,
): TMenuBackgroundRenderer | null => {
  const gl = canvas.getContext("webgl2", { alpha: false, antialias: false, powerPreference: "low-power" });
  if (!gl) {
    return null;
  }

  let program: ReturnType<typeof createProgram>;
  try {
    program = createProgram(gl, QUAD_VERTEX, FRAGMENT);
  } catch (error) {
    console.warn("Main menu background shader failed, the CSS background stays", error);

    return null;
  }

  const quad = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, quad);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 1, -1, -1, 1, 1, 1]), gl.STATIC_DRAW);
  const vao = gl.createVertexArray();
  gl.bindVertexArray(vao);
  gl.enableVertexAttribArray(0);
  gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);

  let art: TLoadedTexture | null = null;
  let mask: TLoadedTexture | null = null;
  let isDisposed = false;

  const ready = Promise.all([loadTexture(gl, artSrc), loadTexture(gl, maskSrc)]).then(([loadedArt, loadedMask]) => {
    // The menu may have closed while the images loaded.
    if (isDisposed) {
      gl.deleteTexture(loadedArt.texture);
      gl.deleteTexture(loadedMask.texture);

      return;
    }

    art = loadedArt;
    mask = loadedMask;
  });

  const resize = (width: number, height: number) => {
    if (canvas.width !== width || canvas.height !== height) {
      canvas.width = width;
      canvas.height = height;
    }
  };

  const render = (frame: TMenuBackgroundFrame) => {
    if (isDisposed || !art || !mask) {
      return;
    }

    gl.viewport(0, 0, canvas.width, canvas.height);
    gl.useProgram(program.program);
    gl.bindVertexArray(vao);

    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, art.texture);
    gl.activeTexture(gl.TEXTURE1);
    gl.bindTexture(gl.TEXTURE_2D, mask.texture);

    gl.uniform1i(uniform(gl, program, "uArt"), 0);
    gl.uniform1i(uniform(gl, program, "uMask"), 1);
    gl.uniform2f(uniform(gl, program, "uResolution"), canvas.width, canvas.height);
    gl.uniform1f(uniform(gl, program, "uArtAspect"), art.width / art.height);
    gl.uniform1f(uniform(gl, program, "uTime"), frame.timeSeconds);
    gl.uniform2f(uniform(gl, program, "uMouse"), frame.mouseX, frame.mouseY);
    gl.uniform1f(uniform(gl, program, "uMotion"), frame.motion);

    gl.drawArrays(gl.TRIANGLE_STRIP, 0, 4);
  };

  const dispose = () => {
    isDisposed = true;
    gl.deleteBuffer(quad);
    gl.deleteVertexArray(vao);
    gl.deleteProgram(program.program);
    if (art) {
      gl.deleteTexture(art.texture);
    }

    if (mask) {
      gl.deleteTexture(mask.texture);
    }
  };

  return { ready, resize, render, dispose };
};

export type { TMenuBackgroundFrame, TMenuBackgroundRenderer };
export { createMenuBackgroundRenderer };
