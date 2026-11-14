import { HEADER } from "./common";

/**
 * The studio logo, drawn into the HDR picture before the bloom. The logo gets the
 * same lens as the explosion: the band of light goes far above 1, so the bloom,
 * the shafts and the anamorphic streak all catch it.
 */
const STUDIO_FRAGMENT = HEADER + /* glsl */ `
in vec2 vUv;
out vec4 outColor;

uniform sampler2D tLogo;
uniform highp sampler3D tNoise;
uniform float uTime;
uniform float uAspect;
uniform float uLogoAspect;
uniform float uLogoWidth;
uniform float uScale;
uniform float uLight;
uniform float uSweep;
uniform float uFlare;

vec3 hash(vec2 cell) {
  vec3 p = fract(vec3(cell.xyx) * vec3(0.1031, 0.103, 0.0973));
  p += dot(p, p.yxz + 33.33);
  return fract((p.xxy + p.yzz) * p.zyx);
}

/** Slow specks of dust. The light of the accent wakes them up. */
float dust(vec2 p, float density, float seed) {
  vec2 grid = p * density + vec2(uTime * 0.09, -uTime * 0.16) + seed;
  vec2 cell = floor(grid);
  vec3 random = hash(cell + seed);
  if (random.z < 0.8) {
    return 0.0;
  }
  float distanceToSpeck = length(fract(grid) - 0.5 - (random.xy - 0.5) * 0.7);
  float blink = 0.6 + 0.4 * sin(uTime * (0.8 + random.x * 2.0) + random.y * 40.0);
  return smoothstep(0.05 + random.x * 0.03, 0.0, distanceToSpeck) * blink;
}

void main() {
  vec2 p = (vUv - 0.5) * vec2(uAspect, 1.0);
  vec2 logoPosition = p / (uLogoWidth * uScale);
  vec2 logoUv = vec2(logoPosition.x, logoPosition.y * uLogoAspect) + 0.5;
  vec3 logo = pow(texture(tLogo, logoUv).rgb, vec3(2.2));
  float luma = dot(logo, vec3(0.3, 0.6, 0.1));

  // The slant makes the band look like light on metal, not like a wipe.
  float across = logoUv.x + (logoUv.y - 0.5) * 0.3;
  float band = exp(-pow((across - uSweep) / 0.05, 2.0));
  float halo = exp(-pow((across - uSweep) / 0.24, 2.0));

  vec3 color = logo * uLight * 1.5;
  color += logo * (1.5 + luma * 4.0) * vec3(1.0, 0.82, 0.56) * (band * 5.5 + halo * 0.7);

  // The dark air behind the logo gives the black screen some depth.
  float radius = length(p * vec2(0.62, 1.0));
  float smoke = texture(tNoise, vec3(p * 0.42 + vec2(uTime * 0.006, 0.0), uTime * 0.008)).r;
  smoke = smoothstep(0.3, 0.85, smoke);
  color += vec3(0.75, 0.42, 0.2) * smoke * exp(-radius * radius * 5.5) * (0.012 + uFlare * 0.05);

  float specks = dust(p, 15.0, 3.0) + dust(p, 26.0, 11.0) * 0.6;
  color += vec3(1.0, 0.74, 0.42) * specks * exp(-radius * radius * 3.0) * (0.05 + uFlare * 0.5);

  outColor = vec4(color, 1.0);
}
`;

export { STUDIO_FRAGMENT };
