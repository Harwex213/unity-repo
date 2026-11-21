/** GLSL shared by the scene shaders. */

const HEADER = /* glsl */ `
precision highp float;
precision highp int;
precision highp sampler2D;
precision highp sampler3D;
#define PI 3.14159265359
`;

/** Black body style ramp: red, orange, yellow, then white, all in HDR. */
const FIRE_GLSL = /* glsl */ `
vec3 fireRamp(float x) {
  x = max(x, 0.0);
  float x2 = x * x;
  return vec3(x * 2.6 + x2 * 0.8, x2 * 1.15 + x2 * x * 0.4, x2 * x2 * 0.5 + x2 * x * 0.05);
}
`;

const TRIPLANAR_GLSL = /* glsl */ `
vec4 triplanar(sampler2D map, vec3 p, vec3 n) {
  vec3 blend = pow(abs(n), vec3(5.0));
  blend /= blend.x + blend.y + blend.z + 1e-4;
  return texture(map, p.zy) * blend.x + texture(map, p.xz) * blend.y + texture(map, p.xy) * blend.z;
}
`;

/**
 * The flight of everything the blast throws out, as a pure function of the time.
 * A piece leaves fast, brakes in the void and ends up nearly at rest. The blast
 * is flattened along the spin axis of the planet, so the debris forms a wide
 * disc and not a ball.
 */
const DEBRIS_GLSL = /* glsl */ `
uniform float uTime;
uniform float uDetonate;
uniform vec3 uEpicentre;
uniform vec3 uGuardDir;

const float BRAKE = 1.2;

float blastDelay(vec3 dir) {
  return acos(clamp(dot(dir, uEpicentre), -1.0, 1.0)) / PI * 0.26;
}

float brakedTime(float tau) {
  return (1.0 - exp(-BRAKE * tau)) / BRAKE;
}

vec3 debrisFlight(vec3 dir, float speed, float tau) {
  // Everything aimed at the lens turns aside, so the star stays in clear view.
  float aim = max(dot(dir, uGuardDir), 0.0);
  vec3 aside = dir - uGuardDir * aim * 0.86;
  float along = aside.y;
  vec3 flat_ = aside - vec3(0.0, along * 0.62, 0.0);
  return flat_ * speed * brakedTime(tau);
}

vec3 rotateAround(vec3 v, vec3 axis, float angle) {
  float s = sin(angle);
  float c = cos(angle);
  return v * c + cross(axis, v) * s + axis * dot(axis, v) * (1.0 - c);
}
`;

const FULLSCREEN_VERTEX = /* glsl */ `
in vec3 position;
out vec2 vUv;
void main() {
  vUv = position.xy * 0.5 + 0.5;
  gl_Position = vec4(position.xy, 0.0, 1.0);
}
`;

export { DEBRIS_GLSL, FIRE_GLSL, FULLSCREEN_VERTEX, HEADER, TRIPLANAR_GLSL };
