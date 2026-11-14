import { FULLSCREEN_VERTEX, HEADER } from "./common";

const POST_VERTEX = HEADER + FULLSCREEN_VERTEX;

/** Adds the volume pass over the scene. */
const MERGE_FRAGMENT = HEADER + /* glsl */ `
in vec2 vUv;
out vec4 outColor;
uniform sampler2D tScene;
uniform sampler2D tVolume;
void main() {
  vec4 volume = texture(tVolume, vUv);
  vec3 scene = texture(tScene, vUv).rgb;
  outColor = vec4(min(scene * volume.a + volume.rgb, vec3(600.0)), 1.0);
}
`;

/** 13 taps of the Call of Duty bloom. The first level clamps fireflies with a Karis average. */
const DOWN_FRAGMENT = HEADER + /* glsl */ `
in vec2 vUv;
out vec4 outColor;
uniform sampler2D tSource;
uniform vec2 uTexel;
uniform float uFirst;

float karis(vec3 c) {
  return 1.0 / (1.0 + max(c.r, max(c.g, c.b)) * 0.25);
}

void main() {
  vec2 t = uTexel;
  vec3 a = texture(tSource, vUv + t * vec2(-2.0, 2.0)).rgb;
  vec3 b = texture(tSource, vUv + t * vec2(0.0, 2.0)).rgb;
  vec3 c = texture(tSource, vUv + t * vec2(2.0, 2.0)).rgb;
  vec3 d = texture(tSource, vUv + t * vec2(-2.0, 0.0)).rgb;
  vec3 e = texture(tSource, vUv).rgb;
  vec3 f = texture(tSource, vUv + t * vec2(2.0, 0.0)).rgb;
  vec3 g = texture(tSource, vUv + t * vec2(-2.0, -2.0)).rgb;
  vec3 h = texture(tSource, vUv + t * vec2(0.0, -2.0)).rgb;
  vec3 i = texture(tSource, vUv + t * vec2(2.0, -2.0)).rgb;
  vec3 j = texture(tSource, vUv + t * vec2(-1.0, 1.0)).rgb;
  vec3 k = texture(tSource, vUv + t * vec2(1.0, 1.0)).rgb;
  vec3 l = texture(tSource, vUv + t * vec2(-1.0, -1.0)).rgb;
  vec3 m = texture(tSource, vUv + t * vec2(1.0, -1.0)).rgb;

  vec3 result;
  if (uFirst > 0.5) {
    vec3 g0 = (a + b + d + e) * 0.25;
    vec3 g1 = (b + c + e + f) * 0.25;
    vec3 g2 = (d + e + g + h) * 0.25;
    vec3 g3 = (e + f + h + i) * 0.25;
    vec3 g4 = (j + k + l + m) * 0.25;
    float w0 = karis(g0);
    float w1 = karis(g1);
    float w2 = karis(g2);
    float w3 = karis(g3);
    float w4 = karis(g4);
    result = (g0 * w0 * 0.125 + g1 * w1 * 0.125 + g2 * w2 * 0.125 + g3 * w3 * 0.125 + g4 * w4 * 0.5)
      / (w0 * 0.125 + w1 * 0.125 + w2 * 0.125 + w3 * 0.125 + w4 * 0.5);
  } else {
    result = e * 0.125 + (a + c + g + i) * 0.03125 + (b + d + f + h) * 0.0625 + (j + k + l + m) * 0.125;
  }
  outColor = vec4(result, 1.0);
}
`;

const UP_FRAGMENT = HEADER + /* glsl */ `
in vec2 vUv;
out vec4 outColor;
uniform sampler2D tSource;
uniform vec2 uTexel;
uniform float uRadius;
void main() {
  vec2 t = uTexel * uRadius;
  vec3 c = texture(tSource, vUv).rgb * 4.0;
  c += (texture(tSource, vUv + vec2(t.x, 0.0)).rgb + texture(tSource, vUv - vec2(t.x, 0.0)).rgb
    + texture(tSource, vUv + vec2(0.0, t.y)).rgb + texture(tSource, vUv - vec2(0.0, t.y)).rgb) * 2.0;
  c += texture(tSource, vUv + t).rgb + texture(tSource, vUv - t).rgb
    + texture(tSource, vUv + vec2(t.x, -t.y)).rgb + texture(tSource, vUv + vec2(-t.x, t.y)).rgb;
  outColor = vec4(c / 16.0, 1.0);
}
`;

/** Keeps only what is bright enough to throw a shaft of light. */
const BRIGHT_FRAGMENT = HEADER + /* glsl */ `
in vec2 vUv;
out vec4 outColor;
uniform sampler2D tSource;
void main() {
  vec3 c = texture(tSource, vUv).rgb;
  float level = max(c.r, max(c.g, c.b));
  outColor = vec4(c * smoothstep(1.4, 4.0, level) / (1.0 + level * 0.06), 1.0);
}
`;

/** Each pass multiplies the samples of the one before, three passes give 216 samples. */
const SHAFT_FRAGMENT = HEADER + /* glsl */ `
in vec2 vUv;
out vec4 outColor;
uniform sampler2D tSource;
uniform vec2 uLight;
uniform float uSpread;
void main() {
  vec2 delta = (uLight - vUv) * uSpread / 6.0;
  float dither = fract(52.9829189 * fract(dot(gl_FragCoord.xy, vec2(0.06711056, 0.00583715))));
  vec2 uv = vUv + delta * dither;
  vec3 sum = vec3(0.0);
  for (int i = 0; i < 6; i++) {
    sum += texture(tSource, uv).rgb;
    uv += delta;
  }
  outColor = vec4(sum / 6.0, 1.0);
}
`;

/** The wide anamorphic streak of a cinema lens. */
const STREAK_FRAGMENT = HEADER + /* glsl */ `
in vec2 vUv;
out vec4 outColor;
uniform sampler2D tSource;
uniform vec2 uTexel;
uniform float uStride;
void main() {
  vec3 sum = texture(tSource, vUv).rgb * 0.26;
  float weight = 0.26;
  for (int i = 1; i <= 4; i++) {
    float w = 0.26 * exp(-float(i * i) * 0.26);
    vec2 offset = vec2(uTexel.x * uStride * float(i), 0.0);
    sum += (texture(tSource, vUv + offset).rgb + texture(tSource, vUv - offset).rgb) * w;
    weight += 2.0 * w;
  }
  outColor = vec4(sum / weight, 1.0);
}
`;

const FINAL_FRAGMENT = HEADER + /* glsl */ `
in vec2 vUv;
out vec4 outColor;

uniform sampler2D tScene;
uniform sampler2D tBloom;
uniform sampler2D tShafts;
uniform sampler2D tStreak;
uniform vec2 uLight;
uniform vec2 uResolution;
uniform float uAspect;
uniform float uTime;
uniform float uFade;
uniform float uExposure;
uniform float uFlash;
uniform float uAberration;
uniform float uShockRadius;
uniform float uShockStrength;
uniform float uShafts;

vec3 aces(vec3 x) {
  // The fit by Stephen Hill: it keeps the hue of the fire better than the short fit.
  const mat3 inputMatrix = mat3(
    0.59719, 0.07600, 0.02840,
    0.35458, 0.90834, 0.13383,
    0.04823, 0.01566, 0.83777);
  const mat3 outputMatrix = mat3(
    1.60475, -0.10208, -0.00327,
    -0.53108, 1.10813, -0.07276,
    -0.07367, -0.00605, 1.07602);
  x = inputMatrix * x;
  vec3 a = x * (x + 0.0245786) - 0.000090537;
  vec3 b = x * (0.983729 * x + 0.4329510) + 0.238081;
  return clamp(outputMatrix * (a / b), 0.0, 1.0);
}

float grain(vec2 uv) {
  vec3 p = fract(vec3(uv.xyx * uResolution.xyx + fract(uTime * 7.31) * 317.0) * 0.1031);
  p += dot(p, p.yzx + 33.33);
  return fract((p.x + p.y) * p.z);
}

void main() {
  vec2 uv = vUv;
  vec2 fromLight = (uv - uLight) * vec2(uAspect, 1.0);
  float radius = length(fromLight);

  // The shock wave bends the picture as it passes.
  if (uShockStrength > 0.0) {
    float band = exp(-pow((radius - uShockRadius) / 0.075, 2.0));
    uv -= fromLight / max(radius, 1e-3) * band * uShockStrength * vec2(1.0 / uAspect, 1.0);
  }

  vec2 centred = uv - 0.5;
  vec2 shift = centred * dot(centred, centred) * 0.0085 * uAberration;
  vec3 scene = vec3(
    texture(tScene, uv + shift).r,
    texture(tScene, uv).g,
    texture(tScene, uv - shift).b);
  vec3 bloom = vec3(
    texture(tBloom, uv + shift).r,
    texture(tBloom, uv).g,
    texture(tBloom, uv - shift).b);

  vec3 color = mix(scene, bloom, 0.09);
  color += texture(tShafts, uv).rgb * vec3(1.0, 0.68, 0.4) * uShafts * 0.34;
  color += texture(tStreak, uv).rgb * vec3(0.42, 0.56, 1.0) * 0.14;
  color += vec3(1.0, 0.86, 0.68) * uFlash * (0.14 + exp(-radius * radius * 7.0) * 3.4);

  float vignette = 1.0 - dot(centred * vec2(1.05, 1.2), centred * vec2(1.05, 1.2)) * 1.05;
  color *= smoothstep(-0.05, 0.85, vignette);
  color *= uExposure;

  vec3 mapped = aces(color * 0.92);

  // Cinema grade: cold shadows, warm light.
  float luma = dot(mapped, vec3(0.2126, 0.7152, 0.0722));
  vec3 shadows = vec3(0.92, 0.98, 1.08);
  vec3 lights = vec3(1.04, 1.0, 0.92);
  mapped *= mix(shadows, lights, smoothstep(0.05, 0.6, luma));
  mapped = pow(mapped, vec3(1.0 / 2.2));
  mapped = mix(vec3(dot(mapped, vec3(0.3, 0.6, 0.1))), mapped, 1.08);

  float noise = grain(uv) - 0.5;
  mapped += noise * (0.034 - luma * 0.02);

  outColor = vec4(mapped * uFade, 1.0);
}
`;

export {
  BRIGHT_FRAGMENT,
  DOWN_FRAGMENT,
  FINAL_FRAGMENT,
  MERGE_FRAGMENT,
  POST_VERTEX,
  SHAFT_FRAGMENT,
  STREAK_FRAGMENT,
  UP_FRAGMENT,
};
