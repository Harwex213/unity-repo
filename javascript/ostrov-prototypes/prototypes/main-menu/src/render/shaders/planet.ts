import { DEBRIS_GLSL, FIRE_GLSL, HEADER, TRIPLANAR_GLSL } from "./common";

const PLANET_VERTEX = HEADER + DEBRIS_GLSL + /* glsl */ `
in vec3 position;
in vec4 aCenter;
in vec4 aRand;
in vec2 aSurface;

uniform mat4 modelMatrix;
uniform mat4 viewMatrix;
uniform mat4 projectionMatrix;
uniform float uCrackStart;
uniform float uInhale;

out vec3 vObject;
out vec3 vWorld;
out vec3 vNormal;
out vec2 vSurface;
out float vTau;
out float vShard;

void main() {
  vec3 centre = aCenter.xyz;
  float tau = uTime - uDetonate - blastDelay(centre);
  float flightTau = max(tau, 0.0);

  // Lighter shards fly further. A shard of a continent barely leaves its place.
  float mass = clamp(sqrt(aCenter.w) * 7.5, 0.0, 1.0);
  float speed = mix(mix(3.6, 10.5, aRand.x * aRand.x), 3.0, mass);

  vec3 local = position - centre;
  vec3 axis = normalize(aRand.xyz * 2.0 - 1.0 + 1e-3);
  float spin = mix(mix(0.9, 3.4, aRand.w), 0.18, mass);
  float angle = spin * (brakedTime(flightTau) * 1.5 + 0.03 * flightTau);
  local = rotateAround(local, axis, angle);
  // The walls are narrow strips: a flat normal would turn them into ribs.
  vec3 restNormal = aSurface.x > 1.5 ? position - centre * dot(position, centre) : position;
  vec3 turned = rotateAround(normalize(restNormal), axis, angle);

  // Before the blast the crust breathes: it swells along the cracks, then sinks.
  float swell = smoothstep(uCrackStart, uInhale, uTime) * (1.0 - smoothstep(uInhale, uDetonate, uTime));
  float sink = smoothstep(uInhale, uDetonate, uTime);
  float jitter = sin(uTime * 41.0 + aRand.y * 90.0) * sin(uTime * 27.0 + aRand.z * 70.0);
  float rest = 1.0 + (swell * (0.008 + 0.012 * aRand.y) + swell * jitter * 0.0035) - sink * (0.03 + 0.012 * aRand.y);
  if (tau > 0.0) {
    rest = 1.0;
  }

  vec3 moved = centre * rest + local + debrisFlight(normalize(centre), speed, flightTau);

  vObject = position;
  vSurface = aSurface;
  vTau = tau;
  vShard = aRand.y;
  vec4 world = modelMatrix * vec4(moved, 1.0);
  vWorld = world.xyz;
  vNormal = mat3(modelMatrix) * turned;
  gl_Position = projectionMatrix * viewMatrix * world;
}
`;

const PLANET_FRAGMENT = HEADER + FIRE_GLSL + TRIPLANAR_GLSL + /* glsl */ `
in vec3 vObject;
in vec3 vWorld;
in vec3 vNormal;
in vec2 vSurface;
in float vTau;
in float vShard;

out vec4 outColor;

uniform sampler2D tAlbedo;
uniform sampler2D tMask;
uniform sampler2D tClouds;
uniform sampler2D tRock;
uniform sampler2D tLava;
uniform sampler3D tNoise;
uniform vec3 uEpicentre;
uniform vec3 uSunDirection;
uniform vec3 uCameraPosition;
uniform float uTime;
uniform float uCrackStart;
uniform float uDetonate;
uniform float uInhale;
uniform float uCoreLight;
uniform float uFireLight;

const vec3 SUN = vec3(1.0, 0.95, 0.88) * 2.0;

vec2 sphereUv(vec3 n) {
  return vec2(atan(n.x, -n.z) / (2.0 * PI) + 0.5, acos(clamp(n.y, -1.0, 1.0)) / PI);
}

/** Reads a map without the mip seam of atan at the back of the sphere. */
vec4 sphereSample(sampler2D map, vec3 n, float shift) {
  vec2 uv = sphereUv(n);
  vec2 dx = dFdx(uv);
  vec2 dy = dFdy(uv);
  if (abs(dx.x) > 0.5) dx.x -= sign(dx.x);
  if (abs(dy.x) > 0.5) dy.x -= sign(dy.x);
  return textureGrad(map, uv + vec2(shift, 0.0), dx, dy);
}

vec3 flatNormal() {
  vec3 n = normalize(cross(dFdx(vWorld), dFdy(vWorld)));
  return dot(n, uCameraPosition - vWorld) < 0.0 ? -n : n;
}

/** The light of the core on a surface with the normal n. */
vec3 coreLighting(vec3 n, vec3 albedo) {
  vec3 toCore = -vWorld;
  float distance2 = dot(toCore, toCore);
  float facing = max(dot(n, toCore / sqrt(distance2)), 0.0);
  return albedo * vec3(1.0, 0.62, 0.3) * uCoreLight * facing / (0.6 + distance2 * 0.12);
}

vec3 molten(float heat, float crack) {
  return fireRamp(heat * crack * 1.6) * 2.1;
}

vec3 shadeCrust(vec3 objectNormal, vec3 n, vec3 viewDir) {
  vec3 albedo = pow(sphereSample(tAlbedo, objectNormal, 0.0).rgb, vec3(2.2));
  vec4 mask = sphereSample(tMask, objectNormal, 0.0);
  float ocean = mask.r;

  // Fine detail the 1536 pixel map does not hold.
  float fine = texture(tNoise, objectNormal * 2.3).r * 0.65 + texture(tNoise, objectNormal * 6.1).g * 0.35;
  albedo *= 0.72 + fine * 0.62 * (1.0 - ocean * 0.7);

  float heightDx = dFdx(mask.g + fine * 0.25);
  float heightDy = dFdy(mask.g + fine * 0.25);
  vec3 tangentX = normalize(dFdx(vWorld));
  vec3 tangentY = normalize(dFdy(vWorld));
  vec3 bumped = normalize(n - (tangentX * heightDx + tangentY * heightDy) * 1.6 * (1.0 - ocean));

  float sun = dot(bumped, uSunDirection);
  float sunSmooth = dot(n, uSunDirection);
  float day = smoothstep(-0.12, 0.25, sunSmooth);
  float diffuse = max(sun, 0.0);

  vec3 lit = albedo * SUN * diffuse;
  vec3 terminator = vec3(1.0, 0.42, 0.16) * smoothstep(-0.14, 0.05, sunSmooth) * (1.0 - smoothstep(0.05, 0.3, sunSmooth));
  lit += albedo * terminator * 0.7;
  lit += albedo * vec3(0.03, 0.05, 0.09);

  vec3 halfway = normalize(uSunDirection + viewDir);
  float gloss = pow(max(dot(n, halfway), 0.0), 240.0) * 6.0 + pow(max(dot(n, halfway), 0.0), 18.0) * 0.22;
  float fresnel = pow(1.0 - max(dot(n, viewDir), 0.0), 5.0);
  lit += ocean * day * (gloss * SUN * vec3(1.0, 0.86, 0.68) + fresnel * vec3(0.09, 0.2, 0.34));

  vec2 cloudSample = vec2(sphereSample(tClouds, objectNormal, uTime * 0.0012).r,
    sphereSample(tClouds, normalize(objectNormal + uSunDirection * 0.008), uTime * 0.0012).r);
  float cloud = smoothstep(0.1, 0.85, cloudSample.x);
  float cloudShadow = smoothstep(0.15, 0.8, cloudSample.y);
  lit *= 1.0 - cloudShadow * 0.55;

  // Night side: the cities of the planet, in the last seconds of their life.
  float city = mask.b * smoothstep(0.6, 0.86, texture(tNoise, objectNormal * 3.1).r * 0.55 + texture(tNoise, objectNormal * 17.0).r * 0.62);
  float night = 1.0 - smoothstep(-0.15, 0.12, sunSmooth);
  vec3 cities = vec3(1.0, 0.62, 0.28) * city * 1.1 * night * (1.0 - cloud * 0.85);

  vec3 cloudColor = vec3(1.0, 0.98, 0.95) * (SUN * max(sunSmooth * 0.92 + 0.08, 0.0) + vec3(0.012, 0.02, 0.035));
  cloudColor += terminator * 1.4;
  vec3 color = mix(lit + cities, cloudColor * 0.62, cloud * 0.8);

  // The fissures open along the real cuts. Their front runs out of the epicentre.
  float fromEpicentre = acos(clamp(dot(objectNormal, uEpicentre), -1.0, 1.0));
  float progress = (uTime - uCrackStart) / (uDetonate - uCrackStart);
  float front = pow(clamp(progress, 0.0, 1.2), 2.1) * 5.4;
  float reached = smoothstep(front, front - 0.5, fromEpicentre + fine * 0.3 - 0.15);
  float flicker = 0.78 + 0.22 * sin(uTime * 34.0 + vShard * 40.0 + fromEpicentre * 9.0);

  float edge = vSurface.y;
  float thin = 1.0 - smoothstep(0.0, 0.1 + fine * 0.09, edge);
  float crackNoise = smoothstep(0.34, 0.66, texture(tNoise, objectNormal * 1.4 + 0.3).r);
  float halo = 1.0 - smoothstep(0.1, 1.0, edge);
  float beforeBlast = 1.0 - smoothstep(0.0, 0.25, vTau);
  float inhaleDrop = 1.0 - 0.8 * smoothstep(uInhale, uDetonate - 0.05, uTime);

  // The ground around a fissure burns: first charred, then a hot halo.
  float burn = reached * halo * halo * smoothstep(0.0, 1.0, progress);
  color *= 1.0 - burn * 0.72;
  color += reached * beforeBlast * flicker * inhaleDrop * (
    fireRamp(0.92) * thin * (0.35 + crackNoise * 0.65) * 2.7
    + fireRamp(0.34) * halo * halo * (0.25 + crackNoise) * 0.7 * progress
  );

  // After the blast the crust is scorched rock with the fissures still alive.
  float scorched = smoothstep(0.0, 0.3, vTau);
  if (scorched > 0.0) {
    vec3 rock = pow(triplanar(tRock, vObject * 5.5, objectNormal).rgb, vec3(2.6)) * 1.5;
    vec3 charred = mix(rock, albedo * 0.3, 0.3) * (SUN * max(dot(n, uSunDirection), 0.0) * 0.5 + vec3(0.02, 0.025, 0.04));
    float lava = triplanar(tLava, vObject * 2.1, objectNormal).r;
    float cooling = exp(-max(vTau, 0.0) * 0.6) * 1.1 + 0.34 + 0.05 * sin(uTime * 0.9 + vShard * 30.0);
    vec3 glow = molten(cooling, thin * mix(0.7, 0.16, smoothstep(0.5, 4.0, vTau)) + pow(lava, 3.0) * 0.8);
    color = mix(color, charred + coreLighting(n, rock * 1.6) + glow, scorched * mix(0.72, 1.0, smoothstep(0.3, 2.2, vTau)));
  }

  return color;
}

vec3 shadeRock(vec3 n) {
  vec3 objectNormal = normalize(vObject);
  float depth = vSurface.y;
  vec3 rock = pow(triplanar(tRock, vObject * 5.5, n).rgb, vec3(2.9)) * 2.2;
  float lava = triplanar(tLava, vObject * 3.1, n).r;

  vec3 color = rock * SUN * 0.75 * max(dot(n, uSunDirection), 0.0);
  color += rock * vec3(0.02, 0.025, 0.04);
  color += coreLighting(n, rock);

  float tau = max(vTau, 0.0);
  float cooling = exp(-tau * 0.7) * 1.4 + 0.34 + 0.06 * sin(uTime * 1.1 + objectNormal.x * 9.0 + vShard * 20.0);
  float glowMask = pow(lava, 2.5) * (0.6 + depth * 0.9) + (1.0 - smoothstep(0.0, 0.12, depth)) * 0.5;
  color += molten(cooling, glowMask);
  return color;
}

void main() {
  vec3 viewDir = normalize(uCameraPosition - vWorld);
  vec3 color;
  if (vSurface.x < 0.5) {
    color = shadeCrust(normalize(vObject), normalize(vNormal), viewDir);
  } else if (vSurface.x < 1.5) {
    color = shadeRock(flatNormal());
  } else {
    color = shadeRock(normalize(vNormal));
  }

  // Everything drowns in the light of the fireball for a moment.
  color += vec3(1.0, 0.5, 0.22) * uFireLight * 0.02;
  outColor = vec4(color, 1.0);
}
`;

export { PLANET_FRAGMENT, PLANET_VERTEX };
