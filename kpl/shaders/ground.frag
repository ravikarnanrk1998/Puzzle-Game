#version 460 core
#include <flutter/runtime_effect.glsl>

precision highp float;

// Ray-casts every pixel onto the ground plane (z = 0) of the cricket ground
// and shades grass, mowing stripes, the pitch, creases, the 30-yard circle,
// the boundary rope and the sky - perspective correct, day or night.

uniform vec2 uSize;
uniform vec3 uCam;      // camera position (m)
uniform vec2 uAng;      // yaw, pitch (rad)
uniform vec3 uProj;     // focal (px), cx, cy
uniform vec2 uRad;      // boundary radii x / y (m)
uniform float uTime;
uniform vec3 uTeam;     // home colour for the rope
uniform float uFragScale;
uniform float uDay;     // 1 = afternoon sunshine, 0 = floodlit night

out vec4 fragColor;

const float MID = 10.06;
// Sun direction (towards the sun): behind the bowler's end, high and to the right.
const vec3 SUN = vec3(0.42, -0.55, 0.72);

float hash(vec2 p) {
  vec3 p3 = fract(vec3(p.xyx) * 0.1031);
  p3 += dot(p3, p3.yzx + 33.33);
  return fract((p3.x + p3.y) * p3.z);
}

float vnoise(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  vec2 u = f * f * (3.0 - 2.0 * f);
  float a = hash(i);
  float b = hash(i + vec2(1.0, 0.0));
  float c = hash(i + vec2(0.0, 1.0));
  float d = hash(i + vec2(1.0, 1.0));
  return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float fbm(vec2 p) {
  float v = 0.0;
  float a = 0.5;
  for (int i = 0; i < 4; i++) {
    v += a * vnoise(p);
    p = p * 2.07 + vec2(17.3, 9.1);
    a *= 0.5;
  }
  return v;
}

float fbm5(vec2 p) {
  float v = 0.0;
  float a = 0.5;
  for (int i = 0; i < 5; i++) {
    v += a * vnoise(p);
    p = p * 2.03 + vec2(11.7, 5.3);
    a *= 0.5;
  }
  return v;
}

float band(float d, float hw, float aa) {
  return 1.0 - smoothstep(hw, hw + aa, abs(d));
}

float segH(vec2 g, float y, float x0, float x1, float hw, float aa) {
  float inside = smoothstep(x0 - aa, x0, g.x) * (1.0 - smoothstep(x1, x1 + aa, g.x));
  return inside * band(g.y - y, hw, aa);
}

float segV(vec2 g, float x, float y0, float y1, float hw, float aa) {
  float inside = smoothstep(y0 - aa, y0, g.y) * (1.0 - smoothstep(y1, y1 + aa, g.y));
  return inside * band(g.x - x, hw, aa);
}

vec3 daySky(vec3 d) {
  vec3 n = normalize(d);
  float h = clamp(n.z, 0.0, 1.0);
  vec3 horizon = vec3(0.74, 0.84, 0.95);
  vec3 zenith = vec3(0.16, 0.42, 0.86);
  vec3 col = mix(horizon, zenith, pow(smoothstep(0.0, 0.55, h), 0.8));
  // Sun glow.
  vec3 sun = normalize(SUN);
  float sd = max(dot(n, sun), 0.0);
  col += vec3(1.0, 0.92, 0.75) * (pow(sd, 8.0) * 0.25 + pow(sd, 120.0) * 0.9);
  // Clouds on a high plane, drifting slowly.
  if (n.z > 0.015) {
    vec2 uv = n.xy / n.z * 0.9 + vec2(uTime * 0.012, uTime * 0.004);
    float c = fbm5(uv * 1.3);
    float cover = smoothstep(0.48, 0.78, c);
    float shade = smoothstep(0.5, 0.9, fbm5(uv * 1.3 + vec2(0.06, 0.08)));
    vec3 cloud = mix(vec3(1.0, 1.0, 0.99), vec3(0.72, 0.77, 0.86), shade * 0.8);
    float fade = smoothstep(0.015, 0.12, n.z);
    col = mix(col, cloud, cover * fade * 0.92);
  }
  return col;
}

vec3 nightSky(vec3 d, vec2 frag) {
  vec3 n = normalize(d);
  float h = clamp(n.z, 0.0, 1.0);
  const vec3 HORIZON = vec3(0.11, 0.075, 0.17);
  vec3 col = mix(HORIZON, vec3(0.15, 0.10, 0.30), smoothstep(0.0, 0.07, h));
  col = mix(col, vec3(0.01, 0.015, 0.05), smoothstep(0.07, 0.42, h));
  float star = step(0.9985, hash(floor(frag * 0.7))) * smoothstep(0.06, 0.2, h);
  return col + vec3(star * 0.85);
}

void main() {
  vec2 frag = FlutterFragCoord().xy * uFragScale;
  float cyw = cos(uAng.x);
  float syw = sin(uAng.x);
  float cp = cos(uAng.y);
  float sp = sin(uAng.y);
  vec3 F = vec3(syw * cp, cyw * cp, -sp);
  vec3 R = vec3(cyw, -syw, 0.0);
  vec3 U = vec3(syw * sp, cyw * sp, cp);
  float dx = (frag.x - uProj.y) / uProj.x;
  float dy = (uProj.z - frag.y) / uProj.x;
  vec3 dir = F + R * dx + U * dy;
  vec3 col;
  bool day = uDay > 0.5;

  if (dir.z > -0.0002) {
    col = day ? daySky(dir) : nightSky(dir, frag);
  } else {
    float t = -uCam.z / dir.z;
    vec2 g = uCam.xy + dir.xy * t;
    float aa = max(0.004, t / uProj.x * 1.4);
    float detail = clamp(1.0 - aa * 2.5, 0.0, 1.0);

    vec2 q = vec2(g.x / uRad.x, (g.y - MID) / uRad.y);
    float r = length(q);
    float ropeD = (r - 1.0) * uRad.x;

    // Grass: layered noise + mowing stripes.
    float n1 = fbm(g * 0.22);
    float n2 = vnoise(g * 2.3);
    float n3 = vnoise(g * 9.0);
    vec3 grassA = day ? vec3(0.17, 0.47, 0.13) : vec3(0.10, 0.36, 0.14);
    vec3 grassB = day ? vec3(0.30, 0.62, 0.20) : vec3(0.21, 0.56, 0.24);
    vec3 grass = mix(grassA, grassB, n1);
    grass *= 1.0 + ((n2 - 0.5) * 0.16 + (n3 - 0.5) * 0.14) * detail;
    float stripe = mod(floor((g.x + 2.5) / 5.0), 2.0);
    float chk = mod(floor((g.y + 3.0) / 6.0), 2.0);
    // Light / dark mowing bands like a broadcast outfield.
    grass *= day ? mix(0.80, 1.14, stripe) * mix(0.97, 1.03, chk) : mix(0.87, 1.1, stripe) * mix(0.96, 1.04, chk);
    float outer = smoothstep(0.0, aa * 2.0, ropeD);
    grass = mix(grass, grass * vec3(0.7, 0.74, 0.68), outer);
    col = grass;

    // 30-yard circle (dashed).
    float ic;
    float s;
    if (g.y > 20.12) {
      vec2 v = g - vec2(0.0, 20.12);
      ic = length(v) - 27.43;
      s = atan(v.y, v.x) * 27.43;
    } else if (g.y < 0.0) {
      ic = length(g) - 27.43;
      s = atan(g.y, g.x) * 27.43;
    } else {
      ic = abs(g.x) - 27.43;
      s = g.y;
    }
    float ph = fract(s / 2.2);
    float dash = smoothstep(0.3, 0.38, ph) * (1.0 - smoothstep(0.86, 0.94, ph));
    col = mix(col, vec3(0.95), band(ic, 0.07, aa) * dash * 0.85);

    // Pitch.
    float pm = (1.0 - smoothstep(1.52, 1.52 + aa, abs(g.x))) * smoothstep(-2.7 - aa, -2.7, g.y) * (1.0 - smoothstep(22.8, 22.8 + aa, g.y));
    if (pm > 0.001) {
      float pn = fbm(g * vec2(1.8, 0.6) + 3.0);
      vec3 pc = day ? mix(vec3(0.72, 0.60, 0.42), vec3(0.88, 0.77, 0.57), pn) : mix(vec3(0.64, 0.53, 0.36), vec3(0.79, 0.68, 0.49), pn);
      pc = mix(pc, day ? vec3(0.93, 0.85, 0.66) : vec3(0.86, 0.77, 0.57), (1.0 - smoothstep(0.15, 0.7, abs(g.x))) * 0.4);
      float near1 = 1.0 - smoothstep(0.4, 2.4, abs(g.y - 1.22));
      float near2 = 1.0 - smoothstep(0.4, 2.4, abs(g.y - 18.9));
      float rough = smoothstep(0.45, 0.8, fbm(g * 3.3 + 7.0)) * (near1 + near2) * smoothstep(0.2, 0.8, abs(g.x));
      pc *= 1.0 - rough * 0.32;
      pc *= 1.0 + (vnoise(g * 18.0) - 0.5) * 0.18 * detail;
      float edge = smoothstep(1.1, 1.52, abs(g.x));
      pc = mix(pc, pc * vec3(0.8, 0.86, 0.7), edge * 0.6);
      col = mix(col, pc, pm);
    }

    // Creases.
    float hw = 0.03;
    float w = 0.0;
    w = max(w, segH(g, 1.22, -1.83, 1.83, hw, aa));
    w = max(w, segH(g, 18.9, -1.83, 1.83, hw, aa));
    w = max(w, segH(g, 0.0, -1.32, 1.32, hw, aa));
    w = max(w, segH(g, 20.12, -1.32, 1.32, hw, aa));
    w = max(w, segV(g, 1.32, -1.22, 1.22, hw, aa));
    w = max(w, segV(g, -1.32, -1.22, 1.22, hw, aa));
    w = max(w, segV(g, 1.32, 18.9, 21.34, hw, aa));
    w = max(w, segV(g, -1.32, 18.9, 21.34, hw, aa));
    w = max(w, segV(g, 0.89, 18.62, 18.9, hw * 0.7, aa));
    w = max(w, segV(g, -0.89, 18.62, 18.9, hw * 0.7, aa));
    w = max(w, segV(g, 0.89, 1.22, 1.5, hw * 0.7, aa));
    w = max(w, segV(g, -0.89, 1.22, 1.5, hw * 0.7, aa));
    col = mix(col, vec3(0.98), w);

    // Boundary rope: white cushion with a team-colour stripe.
    float rope = band(ropeD, 0.16, aa);
    vec3 ropeCol = mix(vec3(0.97), uTeam, band(ropeD, 0.05, aa));
    col = mix(col, ropeCol, rope);

    if (day) {
      // Sunlight with a soft fall-off, plus the shadow cast by the stands on
      // the sun side of the outfield.
      vec2 toEdge = normalize(vec2(g.x, g.y - MID) + 1e-4);
      float sunSide = dot(toEdge, normalize(SUN.xy));
      float standShade = smoothstep(-15.0, -5.0, ropeD) * smoothstep(0.15, 0.6, sunSide);
      col *= 1.04 - standShade * 0.34;
      float centre = 1.0 - smoothstep(0.0, 1.1, r);
      col *= 0.96 + 0.08 * centre;
      float haze = clamp((t - 90.0) / 380.0, 0.0, 0.45);
      col = mix(col, vec3(0.70, 0.79, 0.88), haze);
      float outside = smoothstep(46.0, 52.0, ropeD);
      vec3 town = mix(vec3(0.46, 0.47, 0.45), vec3(0.74, 0.82, 0.92), smoothstep(140.0, 900.0, t));
      col = mix(col, town, outside);
    } else {
      // Floodlight pools and centre lighting.
      float light = 0.0;
      for (int i = 0; i < 4; i++) {
        float a = 0.6632 + float(i) * 1.5708;
        vec2 lp = vec2(cos(a) * (uRad.x + 42.0), MID + sin(a) * (uRad.y + 42.0));
        float d = length(g - lp);
        light += 1.0 / (1.0 + d * d / 8000.0);
      }
      col *= 0.55 + 0.3 * light;
      float centre = 1.0 - smoothstep(0.0, 1.1, r);
      col *= 0.86 + 0.24 * centre;
      col += vec3(0.02, 0.035, 0.03) * pow(centre, 4.0);
      float haze = clamp((t - 70.0) / 300.0, 0.0, 0.4);
      col = mix(col, vec3(0.06, 0.08, 0.14), haze);
      // Outside the stadium bowl: dark city night instead of endless grass.
      float outside = smoothstep(46.0, 52.0, ropeD);
      float city = step(0.996, hash(floor(g * 0.5))) * 0.6;
      vec3 cityCol = vec3(0.018, 0.022, 0.045) + vec3(city, city * 0.8, city * 0.5);
      cityCol = mix(cityCol, vec3(0.11, 0.075, 0.17), smoothstep(140.0, 900.0, t));
      col = mix(col, cityCol, outside);
    }
  }
  fragColor = vec4(col, 1.0);
}
