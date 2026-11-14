import { FIRE_GLSL, FULLSCREEN_VERTEX, HEADER } from "./common";

const VOLUME_VERTEX = HEADER + FULLSCREEN_VERTEX;

/**
 * Everything gaseous, in one half resolution pass: the air of the planet, the
 * fireball, the star left in the core, the shock ring and the ring of dust.
 * The output colour is the light added along the ray, the alpha is how much of
 * the scene behind stays visible.
 */
const VOLUME_FRAGMENT = HEADER + FIRE_GLSL + /* glsl */ `
in vec2 vUv;
out vec4 outColor;

uniform sampler2D tDepth;
uniform sampler3D tNoise;
uniform mat4 uInverseViewProjection;
uniform vec3 uCameraPosition;
uniform vec3 uSunDirection;
uniform vec3 uRingAxis;
uniform float uTime;
uniform float uTau;
uniform float uAir;
uniform float uCoreRadius;
uniform float uCoreGlow;

const int FIRE_STEPS = 34;

vec2 sphereHit(vec3 ro, vec3 rd, float radius) {
  float b = dot(ro, rd);
  float c = dot(ro, ro) - radius * radius;
  float h = b * b - c;
  if (h < 0.0) {
    return vec2(1.0, -1.0);
  }
  h = sqrt(h);
  return vec2(-b - h, -b + h);
}

float jitter() {
  return fract(52.9829189 * fract(dot(gl_FragCoord.xy, vec2(0.06711056, 0.00583715))));
}

/** A thin bright shell with Rayleigh colours and a warm terminator. */
vec3 atmosphere(vec3 ro, vec3 rd, float limit) {
  const float TOP = 1.095;
  vec2 hit = sphereHit(ro, rd, TOP);
  if (hit.y < 0.0 || uAir <= 0.0) {
    return vec3(0.0);
  }

  float from = max(hit.x, 0.0);
  float to = min(hit.y, limit);
  if (to <= from) {
    return vec3(0.0);
  }

  vec3 sum = vec3(0.0);
  const int STEPS = 10;
  float stepSize = (to - from) / float(STEPS);
  float along = from + stepSize * jitter();
  float cosTheta = dot(rd, uSunDirection);
  float forward = 1.0 + 3.2 * pow(max(cosTheta, 0.0), 8.0);

  for (int i = 0; i < STEPS; i++) {
    vec3 p = ro + rd * along;
    float radius = length(p);
    float height = clamp((radius - 1.0) / (TOP - 1.0), 0.0, 1.0);
    float density = exp(-height * 5.2);
    float sunAngle = dot(p / radius, uSunDirection);
    float light = smoothstep(-0.26, 0.26, sunAngle);
    vec3 scatter = mix(vec3(1.0, 0.36, 0.1), vec3(0.2, 0.46, 1.0), smoothstep(-0.1, 0.32, sunAngle));
    sum += scatter * density * light * forward;
    along += stepSize;
  }

  return sum * stepSize * 15.0 * uAir;
}

float saturate(float x) {
  return clamp(x, 0.0, 1.0);
}

/** The radius of the fire front. It leaves fast and brakes in the void. */
float fireRadius(float tau) {
  return 3.0 * (1.0 - exp(-tau * 1.25)) + 0.55 * saturate(tau * 4.0);
}

/**
 * Density and heat of the fire at a point. The noise lives in the coordinates of
 * the front, so the billows grow with the blast and do not slide through it.
 */
vec2 fireField(vec3 p, float tau, float radius) {
  float len = length(p);
  float r = len / radius;
  vec3 dir = p / max(len, 1e-3);
  const vec3 SEED = vec3(0.13, 0.37, 0.71);

  // Big lobes of the silhouette, so the fireball never reads as a disc.
  float lobes = (texture(tNoise, dir * 0.33 + SEED).r - 0.5) * 1.5
    + (0.28 - texture(tNoise, dir * 0.8 + SEED.yzx).g) * 1.1;
  float edge = 1.0 + lobes * (0.35 + 0.45 * smoothstep(0.0, 1.5, tau));

  vec3 q = p / radius * 0.55 + SEED + vec3(0.0, tau * 0.008, tau * 0.012);
  vec3 warp = (texture(tNoise, q * 0.7).rgr - vec3(0.5, 0.72, 0.5)) * 0.3;
  float large = texture(tNoise, q + warp).r;
  float puffs = 1.0 - texture(tNoise, q * 2.3 + warp.yzx).g;
  float small = 1.0 - texture(tNoise, q * 5.9 - warp).g;
  float f = large * 0.5 + puffs * 0.8 + small * 0.4;

  float outer = smoothstep(0.0, 0.18, edge - r + (f - 0.58) * 0.7);
  float hollow = smoothstep(0.05, 1.8, tau);
  float inner = smoothstep(hollow * 0.34, hollow * 0.34 + 0.3, r + (f - 0.58) * 0.5);

  // In the end the fire eats itself: thin parts vanish first and filaments stay.
  float erosion = mix(0.2, 1.0, smoothstep(0.7, 4.9, tau));
  float density = outer * mix(1.0, inner, hollow) * smoothstep(erosion, erosion + 0.22, f) * smoothstep(7.4, 5.2, tau);

  // The heat is an independent field. If it followed the density, the visible
  // skin of the fire would have one flat colour.
  float h = texture(tNoise, q * 1.31 + warp + vec3(0.41, 0.17, 0.59)).r * 0.7
    + (texture(tNoise, q * 3.7 + vec3(0.23)).g - 0.22) * 0.55;
  float hot = smoothstep(0.42, 0.74, h);
  float heat = (1.5 - r * 0.72) * exp(-tau * 0.6) * mix(0.1, 1.7, hot * hot);
  return vec2(density, heat * (0.5 + 0.5 * smoothstep(0.1, 0.9, 1.0 - hollow)));
}

vec4 marchFire(vec3 ro, vec3 rd, float limit) {
  if (uTau <= 0.0 || uTau > 7.5) {
    return vec4(0.0, 0.0, 0.0, 1.0);
  }

  float radius = fireRadius(uTau);
  vec2 hit = sphereHit(ro, rd, radius * 1.5);
  float from = max(hit.x, 0.0);
  float to = min(hit.y, limit);
  if (hit.y < 0.0 || to <= from) {
    return vec4(0.0, 0.0, 0.0, 1.0);
  }

  // The fire dies off in a few seconds and leaves the smoke of a slow remnant.
  float life = exp(-uTau * 0.5);
  float smoke = smoothstep(0.1, 1.3, uTau);
  float sigma = 1.5 + smoke * 1.1;

  vec3 light = vec3(0.0);
  float transmittance = 1.0;
  float stepSize = (to - from) / float(FIRE_STEPS);
  float along = from + stepSize * jitter();

  for (int i = 0; i < FIRE_STEPS; i++) {
    vec3 p = ro + rd * along;
    vec2 field = fireField(p, uTau, radius);
    float density = field.x;
    if (density > 0.002) {
      float optical = density * sigma * stepSize;
      float through = exp(-optical);

      // Smoke is lit from inside by the core, so it stays warm and never turns grey.
      float inner = 1.0 / (1.0 + dot(p, p) * 0.17);
      vec3 emission = fireRamp(field.y * 1.5) * density * (2.2 * life + 0.25);
      vec3 lit = vec3(1.0, 0.36, 0.13) * density * inner * 0.1 * uCoreGlow * smoothstep(7.0, 3.0, uTau);
      lit += vec3(0.42, 0.34, 0.5) * density * 0.01 * smoke * smoothstep(6.5, 3.0, uTau);

      light += transmittance * (emission + lit) * stepSize;
      transmittance *= through;
      if (transmittance < 0.015) {
        transmittance = 0.0;
        break;
      }
    }
    along += stepSize;
  }

  return vec4(light, transmittance);
}

/** The persistent haze of the menu: a slow, huge veil around the core. */
vec3 remnant(vec3 ro, vec3 rd, float limit) {
  float strength = smoothstep(2.0, 5.5, uTau);
  if (strength <= 0.0) {
    return vec3(0.0);
  }

  vec2 hit = sphereHit(ro, rd, 9.5);
  float from = max(hit.x, 0.0);
  float to = min(hit.y, limit);
  if (hit.y < 0.0 || to <= from) {
    return vec3(0.0);
  }

  vec3 sum = vec3(0.0);
  const int STEPS = 9;
  float stepSize = (to - from) / float(STEPS);
  float along = from + stepSize * jitter();
  for (int i = 0; i < STEPS; i++) {
    vec3 p = ro + rd * along;
    float radius2 = dot(p, p);
    float a = texture(tNoise, p * 0.052 + vec3(uTime * 0.0016, 0.0, uTime * 0.001)).r;
    float b = texture(tNoise, p * 0.13 - vec3(0.0, uTime * 0.002, 0.0)).g;
    float wisps = smoothstep(0.46, 0.8, a * 0.75 + b * 0.45);
    float planeFall = exp(-pow(dot(p, uRingAxis) * 0.42, 2.0));
    float falloff = 1.0 / (1.0 + radius2 * 0.09);
    vec3 tint = mix(vec3(1.0, 0.44, 0.16), vec3(0.32, 0.34, 0.75), smoothstep(5.0, 60.0, radius2));
    sum += tint * wisps * planeFall * falloff;
    along += stepSize;
  }

  return sum * stepSize * 0.026 * strength;
}

vec3 ringLight(vec3 ro, vec3 rd, float limit) {
  if (uTau <= 0.0) {
    return vec3(0.0);
  }

  float facing = dot(rd, uRingAxis);
  if (abs(facing) < 1e-3) {
    return vec3(0.0);
  }

  float hit = -dot(ro, uRingAxis) / facing;
  if (hit < 0.0 || hit > limit) {
    return vec3(0.0);
  }

  vec3 p = ro + rd * hit;
  float radius = length(p);
  vec3 tangentA = normalize(cross(uRingAxis, vec3(0.0, 0.0, 1.0)));
  vec3 tangentB = cross(uRingAxis, tangentA);
  vec2 plane = vec2(dot(p, tangentA), dot(p, tangentB));
  float angle = atan(plane.y, plane.x);
  vec2 circle = vec2(cos(angle), sin(angle));

  // The Praxis wave: a razor line that runs out of the planet in the first seconds.
  float front = 24.0 * (1.0 - exp(-uTau * 0.55));
  float width = 0.08 + uTau * 0.34;
  float wave = exp(-pow((radius - front) / width, 2.0));
  float back = exp(-max(front - radius, 0.0) / (0.4 + uTau * 0.8)) * step(radius, front);
  float breakup = texture(tNoise, vec3(circle * 0.34, radius * 0.03 + uTau * 0.02)).r;
  float waveLife = exp(-uTau * 1.7);
  vec3 wavy = (vec3(1.0, 0.82, 0.62) * wave * 13.0 + vec3(1.0, 0.45, 0.16) * back * 0.7)
    * waveLife * mix(0.3, 1.0, smoothstep(0.25, 0.7, breakup));

  // The ring of dust the wave leaves behind. It is what remains of the crust.
  float band = smoothstep(2.0, 3.0, radius) * smoothstep(9.6, 5.2, radius);
  float lanes = texture(tNoise, vec3(radius * 0.11, 0.3, 0.7)).r;
  lanes = mix(0.45, 1.0, smoothstep(0.34, 0.74, lanes * 0.82 + texture(tNoise, vec3(radius * 0.5, 0.1, 0.4)).g * 0.26));
  float clumps = texture(tNoise, vec3(circle * 0.42 + vec2(uTime * 0.0012, 0.0), radius * 0.07)).g;
  float reveal = smoothstep(0.0, 1.6, front - radius) * smoothstep(1.0, 5.0, uTau);
  float falloff = 1.0 / (0.4 + radius * radius * 0.05);
  vec3 dust = vec3(1.0, 0.56, 0.24) * band * lanes * (0.35 + clumps * 0.95) * falloff * reveal * 0.05;

  return wavy + dust;
}

/** The star that burns in place of the planet. */
vec3 coreStar(vec3 ro, vec3 rd, float limit, out float blocked) {
  blocked = 0.0;
  if (uCoreRadius <= 0.0) {
    return vec3(0.0);
  }

  vec2 hit = sphereHit(ro, rd, uCoreRadius);
  vec3 glow = vec3(0.0);
  float closest = -dot(ro, rd);
  float miss = length(ro + rd * closest);
  if (closest < limit) {
    // A wide halo, as if the light scattered in the thin smoke around the star.
    float halo = uCoreRadius / max(miss, uCoreRadius * 0.4);
    vec3 around = normalize(ro + rd * closest);
    float streamers = texture(tNoise, around * 0.62 + vec3(uTime * 0.004)).r;
    streamers = streamers * 0.6 + (1.0 - texture(tNoise, around * 1.7 - vec3(uTime * 0.006)).g) * 0.9;
    glow = vec3(1.0, 0.5, 0.18) * (pow(halo, 2.9) * 0.55 + pow(halo, 1.5) * streamers * streamers * 0.2) * uCoreGlow;
  }

  if (hit.y < 0.0 || hit.x > limit) {
    return glow;
  }

  vec3 p = ro + rd * hit.x;
  vec3 n = p / uCoreRadius;
  float boil = texture(tNoise, n * 0.5 + vec3(uTime * 0.006)).r;
  boil = boil * 0.6 + texture(tNoise, n * 1.3 - vec3(uTime * 0.01)).g * 0.4;
  float limb = pow(max(dot(n, -rd), 0.0), 0.5);
  blocked = 1.0;
  return glow + fireRamp(0.78 + smoothstep(0.34, 0.74, boil) * 0.72) * (0.32 + limb * 1.1) * 0.86 * uCoreGlow;
}

void main() {
  vec4 far = uInverseViewProjection * vec4(vUv * 2.0 - 1.0, 1.0, 1.0);
  vec3 ro = uCameraPosition;
  vec3 rd = normalize(far.xyz / far.w - ro);

  float depth = texture(tDepth, vUv).r;
  float limit = 1e5;
  if (depth < 1.0) {
    vec4 scene = uInverseViewProjection * vec4(vUv * 2.0 - 1.0, depth * 2.0 - 1.0, 1.0);
    limit = length(scene.xyz / scene.w - ro);
  }

  float blocked;
  vec3 star = coreStar(ro, rd, limit, blocked);
  float coreDistance = blocked > 0.5 ? sphereHit(ro, rd, uCoreRadius).x : limit;

  vec4 fire = marchFire(ro, rd, min(limit, coreDistance));
  vec3 air = atmosphere(ro, rd, limit);
  vec3 veil = remnant(ro, rd, limit);
  vec3 ring = ringLight(ro, rd, limit);

  vec3 light = fire.rgb + star * fire.a * fire.a + (air + veil + ring) * mix(0.35, 1.0, fire.a);
  outColor = vec4(light, fire.a);
}
`;

export { VOLUME_FRAGMENT, VOLUME_VERTEX };
