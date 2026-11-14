import { FULLSCREEN_VERTEX, HEADER } from "./common";

const BACKGROUND_VERTEX = HEADER + FULLSCREEN_VERTEX;

/** The nebula is a painting. The stars are drawn here, so they stay sharp at any resolution. */
const BACKGROUND_FRAGMENT = HEADER + /* glsl */ `
in vec2 vUv;
out vec4 outColor;

uniform sampler2D tNebula;
uniform mat4 uInverseViewProjection;
uniform vec3 uCameraPosition;
uniform float uTime;

vec3 hash32(vec2 p) {
  vec3 q = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
  q += dot(q, q.yxz + 33.33);
  return fract((q.xxy + q.yzz) * q.zyx);
}

vec3 starLayer(vec2 p, float cells, float chance, float sharpness) {
  vec2 scaled = p * cells;
  vec2 cell = floor(scaled);
  vec3 h = hash32(cell);
  if (h.z > chance) {
    return vec3(0.0);
  }
  vec3 h2 = hash32(cell + 71.7);
  vec2 centre = (h.xy - 0.5) * 0.76;
  vec2 delta = fract(scaled) - 0.5 - centre;
  float power = pow(h2.x, 12.0) * 9.0 + 0.14;
  float twinkle = 0.82 + 0.18 * sin(uTime * (0.7 + h2.y * 2.4) + h2.z * 40.0);
  float glow = exp(-dot(delta, delta) * sharpness) * power * twinkle;
  vec3 tint = mix(vec3(1.0, 0.72, 0.5), vec3(0.62, 0.78, 1.0), h2.z);
  return tint * glow;
}

void main() {
  vec4 far = uInverseViewProjection * vec4(vUv * 2.0 - 1.0, 1.0, 1.0);
  vec3 rd = normalize(far.xyz / far.w - uCameraPosition);
  vec2 plane = rd.xy / max(-rd.z, 0.05);

  vec2 uv = 0.5 + plane * vec2(0.46, 0.68) + vec2(0.03, 0.0);
  vec3 nebula = texture(tNebula, uv).rgb;
  nebula = pow(nebula, vec3(2.2));

  vec3 stars = starLayer(plane, 310.0, 0.05, 110.0)
    + starLayer(plane + 3.1, 150.0, 0.035, 150.0) * 1.1
    + starLayer(plane + 7.7, 62.0, 0.012, 210.0) * 1.8;

  outColor = vec4(nebula * 1.9 + stars * 0.42, 1.0);
}
`;

export { BACKGROUND_FRAGMENT, BACKGROUND_VERTEX };
