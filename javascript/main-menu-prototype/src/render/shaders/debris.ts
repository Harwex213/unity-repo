import { DEBRIS_GLSL, FIRE_GLSL, HEADER, TRIPLANAR_GLSL } from "./common";

/** Thousands of small rocks. They make the sparse field of big shards read as a real blast. */
const ROCK_VERTEX = HEADER + DEBRIS_GLSL + /* glsl */ `
in vec3 position;
in vec3 iDirection;
in vec4 iRand;

uniform mat4 modelMatrix;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;

out vec3 vObject;
out vec3 vWorld;
out float vTau;
out float vRand;

void main() {
  float tau = uTime - uDetonate - blastDelay(iDirection) - 0.03;
  float flightTau = max(tau, 0.0);
  float size = (0.005 + pow(iRand.x, 8.0) * 0.07) * smoothstep(0.0, 0.05, tau);
  vec3 axis = normalize(iRand.yzw * 2.0 - 1.0);
  float angle = (1.0 + iRand.y * 5.0) * (brakedTime(flightTau) * 2.0 + flightTau * 0.05);
  vec3 local = rotateAround(position * size * vec3(1.0, 0.6 + iRand.z * 0.6, 0.7 + iRand.w), axis, angle);
  vec3 centre = iDirection * (0.97 + iRand.w * 0.03) + debrisFlight(iDirection, 2.4 + iRand.y * iRand.y * 9.0, flightTau);

  vObject = position + iRand.xyz * 7.0;
  vTau = tau;
  vRand = iRand.w;
  vec4 world = modelMatrix * vec4(centre + local, 1.0);
  vWorld = world.xyz;
  gl_Position = projectionMatrix * viewMatrix * world;
}
`;

const ROCK_FRAGMENT = HEADER + FIRE_GLSL + TRIPLANAR_GLSL + /* glsl */ `
in vec3 vObject;
in vec3 vWorld;
in float vTau;
in float vRand;

out vec4 outColor;

uniform sampler2D tRock;
uniform vec3 uSunDirection;
uniform vec3 uCameraPosition;
uniform float uCoreLight;

void main() {
  vec3 n = normalize(cross(dFdx(vWorld), dFdy(vWorld)));
  if (dot(n, uCameraPosition - vWorld) < 0.0) {
    n = -n;
  }
  vec3 rock = pow(triplanar(tRock, vObject, n).rgb, vec3(2.6)) * 2.4;
  vec3 color = rock * 2.2 * max(dot(n, uSunDirection), 0.0) + rock * vec3(0.02, 0.025, 0.04);

  vec3 toCore = -vWorld;
  float distance2 = dot(toCore, toCore);
  color += rock * vec3(1.0, 0.6, 0.3) * uCoreLight * max(dot(n, toCore / sqrt(distance2)), 0.0) / (0.6 + distance2 * 0.1);

  float cooling = exp(-max(vTau, 0.0) * (1.3 + vRand * 1.4)) * 1.1;
  color += fireRamp(cooling) * 1.1;
  outColor = vec4(color, 1.0);
}
`;

/** Streaks of molten rock. A spark is a quad stretched between its current and previous place. */
const SPARK_VERTEX = HEADER + DEBRIS_GLSL + /* glsl */ `
in vec3 position;
in vec3 iDirection;
in vec4 iRand;

uniform mat4 modelMatrix;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;
uniform vec2 uResolution;

out vec2 vLocal;
out float vHeat;

vec3 place(float tau) {
  float speed = 3.0 + iRand.x * iRand.x * 13.0;
  return iDirection * 0.98 + debrisFlight(iDirection, speed, tau);
}

void main() {
  float tau = uTime - uDetonate - blastDelay(iDirection) * 0.6 - iRand.y * 0.5;
  float life = 0.9 + iRand.z * iRand.z * 4.5;
  if (tau <= 0.0 || tau > life) {
    gl_Position = vec4(2.0, 2.0, 2.0, 1.0);
    return;
  }

  vec4 head = projectionMatrix * viewMatrix * modelMatrix * vec4(place(tau), 1.0);
  vec4 tail = projectionMatrix * viewMatrix * modelMatrix * vec4(place(max(tau - 0.035, 0.0)), 1.0);
  vec2 headNdc = head.xy / head.w;
  vec2 tailNdc = tail.xy / tail.w;
  vec2 aspect = vec2(uResolution.x / uResolution.y, 1.0);
  vec2 along = (headNdc - tailNdc) * aspect;
  float len = length(along);
  vec2 forward = len > 1e-5 ? along / len : vec2(1.0, 0.0);
  vec2 side = vec2(-forward.y, forward.x);

  float pixel = 2.0 / uResolution.y;
  float width = pixel * (0.5 + iRand.w * iRand.w * 1.3);
  vec2 offset = side * position.y * width + forward * position.x * (width * 0.5);
  vec4 clip = position.x > 0.0 ? head : tail;
  clip.xy += offset / aspect * clip.w;

  float age = tau / life;
  vLocal = position.xy;
  vHeat = (1.0 - age * age) * (0.8 + iRand.w * 1.2);
  gl_Position = clip;
}
`;

const SPARK_FRAGMENT = HEADER + FIRE_GLSL + /* glsl */ `
in vec2 vLocal;
in float vHeat;
out vec4 outColor;

void main() {
  float fall = 1.0 - smoothstep(0.0, 1.0, abs(vLocal.y));
  outColor = vec4(fireRamp(vHeat * 0.72) * fall * 0.95, 1.0);
}
`;

/**
 * The embers of the menu. They come from the core, cross the frame slowly and
 * die in a loop, so the scene never stops moving.
 */
const EMBER_VERTEX = HEADER + /* glsl */ `
in vec3 position;
in vec3 iDirection;
in vec4 iRand;

uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;
uniform vec2 uResolution;
uniform float uTime;
uniform float uEmbers;

out vec2 vLocal;
out float vHeat;

void main() {
  float cycle = 26.0 + iRand.x * 34.0;
  float age = fract(uTime / cycle + iRand.y);
  vec3 axis = vec3(0.3, 0.9, 0.3);
  float swirl = age * (0.5 + iRand.z * 0.7);
  float c = cos(swirl);
  float s = sin(swirl);
  vec3 dir = iDirection * c + cross(normalize(axis), iDirection) * s;
  vec3 world = dir * (0.8 + age * (6.0 + iRand.w * 9.0));
  world.y += sin(age * 9.0 + iRand.x * 20.0) * 0.14;

  vec4 clip = projectionMatrix * viewMatrix * vec4(world, 1.0);
  float size = (0.9 + iRand.w * iRand.w * 1.5) * (1.0 + 4.0 / clip.w);
  clip.xy += position.xy * size * vec2(uResolution.y / uResolution.x, 1.0) * (2.0 / uResolution.y) * clip.w;

  float blink = 0.72 + 0.28 * sin(uTime * (1.2 + iRand.z * 3.0) + iRand.x * 50.0);
  vLocal = position.xy;
  vHeat = sin(age * PI) * blink * uEmbers * (0.26 + iRand.w * 0.42);
  gl_Position = clip;
}
`;

const EMBER_FRAGMENT = HEADER + FIRE_GLSL + /* glsl */ `
in vec2 vLocal;
in float vHeat;
out vec4 outColor;

void main() {
  float d = dot(vLocal, vLocal);
  float fall = exp(-d * 5.0) - exp(-5.0);
  outColor = vec4(fireRamp(vHeat * 0.95) * max(fall, 0.0) * 2.0, 1.0);
}
`;

export { EMBER_FRAGMENT, EMBER_VERTEX, ROCK_FRAGMENT, ROCK_VERTEX, SPARK_FRAGMENT, SPARK_VERTEX };
