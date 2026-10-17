// The background of the main menu: the prototype's painting of the castle in
// the clouds through a stone arch, with the life its WebGL pass adds on top
// (ui/components/main-menu/menu-background-gl.ts), ported line for line:
// - slow drifting clouds and fog over the sky, which a mask marks white;
// - soft god rays from the sun in the upper left;
// - a sway on the banners of the arch and a heat shimmer in the toxic haze;
// - flickering torches; dust motes, embers and a few green spores;
// - a slow "Ken Burns" zoom and a mouse parallax of a few pixels;
// - a grade toward navy and gold, and a vignette.
// On top of that it lays the prototype's shade, which darkens the sunset behind
// the title and the pillar behind the list so the text stays readable.
//
// It is drawn on a quad that covers the screen in front of everything else, so
// it works in screen space: the art is cover-fitted to the screen.
Shader "Hexwex/MenuBackdrop"
{
    Properties
    {
        _Art ("Painting", 2D) = "black" {}
        _Mask ("Sky mask", 2D) = "black" {}
        _ArtAspect ("Painting width over height", Float) = 1.7778
        _Pointer ("Pointer, -1..1", Vector) = (0, 0, 0, 0)
        _Motion ("Motion", Range(0, 1)) = 1
        _Fade ("Fade in", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Overlay" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Backdrop"
            Tags { "LightMode" = "UniversalForward" }

            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Art);
            SAMPLER(sampler_Art);
            TEXTURE2D(_Mask);
            SAMPLER(sampler_Mask);

            CBUFFER_START(UnityPerMaterial)
                float _ArtAspect;
                float4 _Pointer;
                float _Motion;
                float _Fade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // Where on the screen, from the quad itself: 0..1 with the origin top left, like the art.
                float2 screen : TEXCOORD0;
            };

            // Places in the art, in its uv with the origin top left.
            static const float2 Light = float2(0.11, 0.17);
            static const float2 LeftTorch = float2(0.035, 0.5);
            static const float2 RightTorch = float2(0.732, 0.555);
            static const float2 ToxicGlow = float2(0.6, 0.8);

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screen = float2(input.positionOS.x + 0.5, 0.5 - input.positionOS.y);

                return output;
            }

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);

                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(Hash12(i), Hash12(i + float2(1, 0)), f.x), lerp(Hash12(i + float2(0, 1)), Hash12(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                for (int octave = 0; octave < 4; octave += 1)
                {
                    sum += ValueNoise(p) * amplitude;
                    p = p * 2.03 + float2(17.1, 9.3);
                    amplitude *= 0.5;
                }

                return sum;
            }

            // The art reads with its origin in the top left, as the prototype's does.
            float4 Art(float2 uv, float lod)
            {
                return SAMPLE_TEXTURE2D_LOD(_Art, sampler_Art, float2(uv.x, 1.0 - uv.y), lod);
            }

            float Sky(float2 uv, float lod)
            {
                return SAMPLE_TEXTURE2D_LOD(_Mask, sampler_Mask, float2(uv.x, 1.0 - uv.y), lod).r;
            }

            float2 CoverUv(float2 screen, float screenAspect)
            {
                float2 uv = screen - 0.5;
                if (screenAspect > _ArtAspect)
                {
                    uv.y *= _ArtAspect / screenAspect;
                }
                else
                {
                    uv.x *= screenAspect / _ArtAspect;
                }

                return uv + 0.5;
            }

            // Three layers of rising motes in screen space.
            float3 Motes(float2 screen, float t, float2 resolution)
            {
                float3 sum = 0.0;
                float aspect = resolution.x / resolution.y;
                for (int layer = 0; layer < 3; layer += 1)
                {
                    float fl = layer;
                    float cells = 9.0 + fl * 7.0;
                    float2 q = float2(screen.x * aspect, screen.y) * cells;
                    q += float2(sin(t * 0.13 + fl * 2.1) * 0.6, t * (0.22 + fl * 0.1));
                    float2 cell = floor(q);
                    float2 inCell = frac(q);
                    float seed = Hash12(cell + fl * 31.7);
                    if (seed > 0.42)
                    {
                        continue;
                    }

                    float2 centre = 0.25 + 0.5 * float2(Hash12(cell + 3.1), Hash12(cell + 7.9));
                    centre.x += 0.18 * sin(t * (0.7 + seed * 1.3) + seed * 40.0);
                    float radiusPx = (0.9 + 2.2 * Hash12(cell + 11.3)) * (1.0 + fl * 0.35);
                    float radius = radiusPx * cells / resolution.y;
                    float2 delta = (inCell - centre) / radius;
                    float glow = exp(-dot(delta, delta));
                    float twinkle = 0.55 + 0.45 * sin(t * (1.1 + seed * 3.0) + seed * 60.0);
                    float kind = Hash12(cell + 17.0);
                    float3 tint = kind < 0.14 ? float3(0.55, 1.0, 0.35) : kind < 0.5 ? float3(1.0, 0.62, 0.28) : float3(1.0, 0.93, 0.8);
                    float lower = kind < 0.5 ? 0.35 + 0.65 * smoothstep(0.15, 0.95, screen.y) : 0.75;
                    sum += tint * glow * twinkle * lower * (0.5 + 0.2 * fl);
                }

                return sum;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float motion = _Motion;
                float2 resolution = _ScreenParams.xy;
                // The quad fills the view, so its own place is the place on the screen. Reading it
                // from the quad, not from the pixel, leaves out which way up the target is.
                float2 pixel = input.positionCS.xy;
                float2 screen = input.screen;
                float2 uv = CoverUv(screen, resolution.x / resolution.y);

                // Ken Burns: a slow breathing zoom and a slow pan.
                float zoom = 1.065 + 0.018 * sin(t * 0.06) * motion;
                float2 pan = float2(sin(t * 0.031), cos(t * 0.023)) * 0.005 * motion;
                uv = (uv - 0.5) / zoom + 0.5 + pan;

                // Parallax: the near stone moves more than the far sky.
                float skyNear = Sky(uv, 3.0);
                uv += _Pointer.xy * lerp(0.009, 0.003, skyNear) * motion;

                // The banners hang from the top of the pillars, so the sway grows toward their lower ends.
                float leftBanner = smoothstep(0.02, 0.035, uv.x) * (1.0 - smoothstep(0.095, 0.11, uv.x));
                float rightBanner = smoothstep(0.682, 0.695, uv.x) * (1.0 - smoothstep(0.77, 0.785, uv.x));
                float banner = (leftBanner + rightBanner) * (1.0 - smoothstep(0.44, 0.5, uv.y)) * smoothstep(0.04, 0.42, uv.y) * (1.0 - skyNear);
                float sway = sin(uv.y * 26.0 - t * 1.6 + uv.x * 5.0) * 0.6 + sin(t * 0.8 + uv.y * 8.0) * 0.4;
                uv.x += sway * 0.0022 * banner * motion;

                // The heat shimmer over the toxic haze in the lower right of the opening.
                float2 toGlow = (uv - ToxicGlow) * float2(_ArtAspect * 0.8, 1.6);
                float haze = smoothstep(0.55, 0.0, length(toGlow));
                float2 shimmer = float2(
                    ValueNoise(uv * float2(42.0, 16.0) + float2(0.0, t * 1.1)),
                    ValueNoise(uv * float2(36.0, 22.0) + float2(5.3, t * 0.9))) - 0.5;
                uv += shimmer * 0.003 * haze * motion;

                uv = clamp(uv, 0.001, 0.999);
                float3 color = Art(uv, 0.0).rgb;
                float sky = Sky(uv, 1.0);

                // Two layers of clouds and fog drift over the sky.
                float2 cloudPoint = float2(uv.x * _ArtAspect, uv.y) * 2.4;
                float far = Fbm(cloudPoint * 1.2 + float2(t * 0.010, t * 0.002));
                float near = Fbm(cloudPoint * 2.7 + float2(t * 0.024, -t * 0.003) + 3.7);
                float cloud = smoothstep(0.42, 0.86, far * 0.62 + near * 0.48);
                float2 toLight = (uv - Light) * float2(_ArtAspect, 1.0);
                float lightDistance = length(toLight);
                float3 cloudColor = lerp(float3(0.58, 0.64, 0.72), float3(1.0, 0.9, 0.72), exp(-lightDistance * 1.6));
                color = lerp(color, cloudColor, cloud * sky * 0.38);
                // A thin mist hangs over the whole scene too.
                color = lerp(color, cloudColor * 0.8, near * 0.06 * (1.0 - sky));

                // God rays: noisy streaks fanning out from the light.
                float angle = atan2(toLight.y, toLight.x);
                float streaks = ValueNoise(float2(angle * 13.0, t * 0.07)) * ValueNoise(float2(angle * 29.0 + 4.0, -t * 0.05));
                float rays = pow(streaks, 1.4) * smoothstep(1.45, 0.05, lightDistance) * (0.35 + 0.65 * sky);
                color += float3(1.0, 0.84, 0.58) * (rays * 0.28 + exp(-lightDistance * 2.6) * 0.1);

                // The toxic haze glows, pulses and drifts.
                float toxic = haze * Fbm(float2(uv.x * 5.0 + t * 0.03, uv.y * 3.0 - t * 0.012));
                color += float3(0.32, 0.85, 0.22) * toxic * (0.12 + 0.04 * sin(t * 0.7)) * (0.4 + 0.6 * sky);

                // The torches flicker.
                float2 toLeftTorch = (uv - LeftTorch) * float2(_ArtAspect, 1.0);
                float2 toRightTorch = (uv - RightTorch) * float2(_ArtAspect, 1.0);
                float flickerLeft = 0.7 + 0.3 * ValueNoise(float2(t * 6.0, 1.3)) * motion;
                float flickerRight = 0.7 + 0.3 * ValueNoise(float2(t * 6.4, 7.9)) * motion;
                color += float3(1.0, 0.55, 0.2) * (exp(-dot(toLeftTorch, toLeftTorch) * 260.0) * flickerLeft + exp(-dot(toRightTorch, toRightTorch) * 260.0) * flickerRight) * 0.32;

                color += Motes(screen, t, resolution);

                // The grade: navy shadows, warm gold highlights, a little more contrast.
                float luma = dot(color, float3(0.299, 0.587, 0.114));
                color *= lerp(float3(0.8, 0.9, 1.1), float3(1.07, 1.0, 0.88), smoothstep(0.15, 0.8, luma));
                color = lerp(luma.xxx, color, 1.06);
                color = (color - 0.5) * 1.05 + 0.5;

                // The vignette.
                float2 fromCentre = (screen - 0.5) * float2(1.05, 1.25);
                color *= lerp(0.5, 1.0, smoothstep(0.85, 0.25, length(fromCentre)));

                // The shade under the menu: dark behind the title, along the right where the list stands, and at the foot.
                float2 titleSpot = (screen - float2(0.24, 0.34)) / float2(0.34, 0.4);
                float shade = 0.5 * (1.0 - smoothstep(0.0, 0.75, length(titleSpot)));
                float across = screen.x;
                float band = across < 0.38 ? lerp(0.5, 0.12, across / 0.38)
                    : across < 0.55 ? lerp(0.12, 0.0, (across - 0.38) / 0.17)
                    : across < 0.7 ? lerp(0.0, 0.35, (across - 0.55) / 0.15)
                    : lerp(0.35, 0.72, (across - 0.7) / 0.3);
                shade = 1.0 - (1.0 - shade) * (1.0 - band) * (1.0 - 0.6 * saturate(1.0 - (1.0 - screen.y) / 0.22));
                color = lerp(color, float3(0.016, 0.027, 0.047), shade);

                // Dither, against banding in the dark gradients.
                color += (Hash12(pixel) - 0.5) / 255.0;

                // The painting is drawn as it is painted: the colours above are the prototype's, in its own space.
                color = pow(max(color, 0.0), 2.2);

                return half4(color, _Fade);
            }
            ENDHLSL
        }
    }
}
