// The ground of a hex: the top of the tile, its sides, and the rock under it.
// One colour comes in for each (the biome's, or the toxic tint over it) and the
// shader makes land of it. The top is mottled like turf or sand and darkens
// toward its rim. A side is cut through the island: a lip of the top's own
// cover, then soil, then layered rock.
//
// The surface of the top follows the kind of ground (_Ground): 0 turf, 1 sand
// in wind ripples, 2 snow, 3 bare cracked rock, 4 mud with standing water,
// 5 forest floor of needles and leaves, 6 sunbaked cracked earth, 7 volcanic
// ash whose cracks glow. All of it is drawn here from noise; there are no
// texture files.
//
// The meshes of HexMeshes say what is what in uv0: x is how near the rim a
// point of the top is (0 in the middle, 1 on the rim); y is 0 on the top, 1 to
// 2 down a side of the tile (1 at the top of it), and 2 to 3 down the rock
// under the tile.
Shader "Hexwex/Ground"
{
    Properties
    {
        _BaseColor ("Ground", Color) = (0.4, 0.6, 0.3, 1)
        _Ground ("Kind of ground", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Ground"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Ground;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                // x: nearness to the rim. y: what part this is. z: how tall the mesh stands in the world.
                float3 part : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                float tall = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                output.part = float3(input.uv, tall);

                return output;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);

                return frac((p.x + p.y) * p.z);
            }

            float Noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(Hash13(i), Hash13(i + float3(1, 0, 0)), f.x), lerp(Hash13(i + float3(0, 1, 0)), Hash13(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash13(i + float3(0, 0, 1)), Hash13(i + float3(1, 0, 1)), f.x), lerp(Hash13(i + float3(0, 1, 1)), Hash13(i + float3(1, 1, 1)), f.x), f.y),
                    f.z);
            }

            float2 Hash22(float2 p)
            {
                float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                q += dot(q, q.yzx + 33.33);

                return frac((q.xx + q.yz) * q.zy);
            }

            // Cells round scattered points: x is the distance to the nearest edge between
            // two cells, y a number of the cell's own. Cracks run along the edges.
            float2 Cells(float2 p)
            {
                float2 cell = floor(p);
                float2 inside = frac(p);
                float nearest = 8.0;
                float second = 8.0;
                float id = 0.0;
                for (int y = -1; y <= 1; y += 1)
                {
                    for (int x = -1; x <= 1; x += 1)
                    {
                        float2 offset = float2(x, y);
                        float2 seed = Hash22(cell + offset);
                        float d = length(offset + seed - inside);
                        if (d < nearest)
                        {
                            second = nearest;
                            nearest = d;
                            id = seed.x;
                        }
                        else if (d < second)
                        {
                            second = d;
                        }
                    }
                }

                return float2(second - nearest, id);
            }

            /// The top of a hex, by its kind of ground. `glow` is light the ground gives off itself.
            half3 Surface(float3 at, half3 ground, float kind, out half3 glow)
            {
                glow = 0;
                float2 p = at.xz;
                float grain = Noise(at * 11.0) * 0.5 + Noise(at * 27.0) * 0.5;
                float patches = Noise(at * 1.7 + 13.0);
                half3 albedo = ground * (0.86 + 0.2 * grain);
                albedo = lerp(albedo, ground * 1.16 + 0.015, smoothstep(0.52, 0.8, patches) * 0.6);
                albedo = lerp(albedo, ground * 0.78, smoothstep(0.5, 0.2, patches) * 0.45);

                if (kind < 0.5)
                {
                    // Turf: blades too small to model, as fine streaks of light and dark, and clumps.
                    float blades = Noise(float3(p.x * 90.0, 0.0, p.y * 22.0)) * Noise(float3(p.x * 25.0, 3.0, p.y * 95.0));
                    albedo *= 0.88 + 0.32 * blades;
                    float clumps = Noise(at * 6.0 + 7.0);
                    albedo = lerp(albedo, ground * half3(0.82, 0.95, 0.7), smoothstep(0.62, 0.8, clumps) * 0.5);
                    // Here and there a bare spot of earth.
                    albedo = lerp(albedo, half3(0.36, 0.28, 0.18), smoothstep(0.78, 0.86, Noise(at * 3.1 + 21.0)) * 0.55);
                }
                else if (kind < 1.5)
                {
                    // Sand: ripples the wind combed across it, bending with the dunes.
                    float bend = Noise(at * 2.2) * 9.0 + Noise(at * 5.0) * 2.0;
                    float ripple = sin((p.x * 0.8 + p.y * 0.6) * 55.0 + bend);
                    // A ripple is a soft slope and a sharp shaded crest, and it fades out in patches.
                    float strength = smoothstep(0.25, 0.6, Noise(at * 1.5 + 4.0));
                    albedo = ground * (0.95 + 0.06 * grain + strength * (0.05 * ripple - 0.07 * smoothstep(0.85, 1.0, ripple)));
                }
                else if (kind < 2.5)
                {
                    // Snow: near white, blue in the hollows, a glitter of ice crystals.
                    float drifts = Noise(at * 3.0 + 2.0) * 0.6 + Noise(at * 8.0) * 0.4;
                    half3 snow = lerp(half3(0.7, 0.8, 0.93), half3(0.97, 0.98, 1.0), smoothstep(0.3, 0.7, drifts));
                    albedo = snow * (0.96 + 0.06 * grain);
                    float sparkle = step(0.992, Hash13(floor(at * 140.0)));
                    glow = sparkle * 0.35;
                }
                else if (kind < 3.5)
                {
                    // Bare rock: slabs split by cracks, each slab its own shade, lichen in spots.
                    float2 cells = Cells(p * 5.0 + float2(Noise(at * 3.0), Noise(at * 3.0 + 5.0)) * 0.8);
                    albedo = ground * (0.86 + 0.18 * cells.y + 0.12 * grain);
                    // Cracks wander: the point is pushed about by noise before the cells are found.
                    float crack = (1.0 - smoothstep(0.0, 0.035, cells.x)) * smoothstep(0.25, 0.55, Noise(at * 4.0 + 11.0));
                    albedo = lerp(albedo, ground * 0.45, crack * 0.75);
                    albedo *= 0.92 + 0.12 * smoothstep(0.0, 0.25, cells.x);
                    albedo = lerp(albedo, half3(0.55, 0.6, 0.35), smoothstep(0.8, 0.86, Noise(at * 7.0 + 9.0)) * 0.22);
                }
                else if (kind < 4.5)
                {
                    // Mud: dark wet ground with pools of standing water that catch the sky.
                    float wet = smoothstep(0.55, 0.62, Noise(at * 2.6 + 3.0));
                    albedo = lerp(albedo * 0.85, half3(0.06, 0.08, 0.07), wet);
                    // A darker rim of wet earth round each pool.
                    albedo *= 1.0 - smoothstep(0.48, 0.55, Noise(at * 2.6 + 3.0)) * (1.0 - wet) * 0.3;
                    float reeds = Noise(float3(p.x * 70.0, 0.0, p.y * 70.0));
                    albedo *= 1.0 - (1.0 - wet) * smoothstep(0.7, 0.9, reeds) * 0.25;
                }
                else if (kind < 5.5)
                {
                    // Forest floor: needles and fallen leaves over dark earth, moss in the light patches.
                    // Fallen leaves: small rounded cells, only some of them, scattered thicker in drifts.
                    float2 leaves = Cells(p * 34.0 + float2(Noise(at * 9.0), Noise(at * 9.0 + 3.0)) * 0.6);
                    half3 leaf = lerp(half3(0.4, 0.25, 0.1), half3(0.55, 0.42, 0.16), frac(leaves.y * 7.3));
                    float drift = smoothstep(0.35, 0.7, Noise(at * 2.5 + 8.0));
                    float isLeaf = step(1.0 - drift * 0.45, frac(leaves.y * 13.7));
                    albedo = lerp(albedo * 0.82, leaf, isLeaf * smoothstep(0.08, 0.2, leaves.x) * 0.4);
                    albedo = lerp(albedo, ground * half3(0.9, 1.15, 0.8), smoothstep(0.6, 0.75, Noise(at * 3.0 + 5.0)) * 0.4);
                }
                else if (kind < 6.5)
                {
                    // Sunbaked earth: plates curled up between deep dry cracks.
                    float2 cells = Cells(p * 5.5 + float2(Noise(at * 4.0), Noise(at * 4.0 + 6.0)) * 0.5);
                    albedo = ground * (0.9 + 0.14 * cells.y + 0.1 * grain);
                    float crack = (1.0 - smoothstep(0.0, 0.05, cells.x)) * smoothstep(0.2, 0.45, Noise(at * 3.0 + 1.0));
                    albedo = lerp(albedo, ground * 0.45, crack * 0.8);
                    // A plate curls up at its edges and catches the light there.
                    albedo *= 0.96 + 0.1 * smoothstep(0.04, 0.0, cells.x) * (1.0 - crack);
                }
                else
                {
                    // Ash: grey-black, soft, and the rock under it glows red through the cracks.
                    float2 cells = Cells(p * 5.0);
                    albedo = lerp(half3(0.16, 0.15, 0.15), half3(0.3, 0.29, 0.28), grain) * (0.85 + 0.25 * patches);
                    float crack = 1.0 - smoothstep(0.0, 0.035, cells.x);
                    float pulse = 0.75 + 0.25 * sin(_Time.y * 1.4 + cells.y * 6.0);
                    albedo = lerp(albedo, half3(0.25, 0.08, 0.02), crack);
                    glow = crack * half3(1.0, 0.32, 0.06) * 1.6 * pulse * step(0.4, cells.y);
                }

                return albedo;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 at = input.positionWS;
                half3 ground = _BaseColor.rgb;
                half3 albedo;
                half3 glow = 0;

                if (input.part.y < 0.5)
                {
                    // The top: its kind of ground, and a worn rim.
                    albedo = Surface(at, ground, _Ground, glow);
                    albedo *= 1.0 - 0.2 * smoothstep(0.72, 1.0, input.part.x);
                }
                else
                {
                    // A side: how far below the top of the tile this point is, in world units.
                    bool isRoot = input.part.y >= 2.0;
                    float down = (input.part.y - (isRoot ? 2.0 : 1.0)) * input.part.z;
                    float ragged = (Noise(float3(at.x * 5.0, 0.0, at.z * 5.0)) - 0.5) * 0.07;
                    half3 rock = lerp(ground * 0.5, half3(0.2, 0.185, 0.18), 0.55);
                    // Rock lies in beds: thin bands that run level, a little crooked.
                    float beds = Noise(float3(at.x * 1.5, at.y * 13.0 + Noise(at * 2.0) * 1.6, at.z * 1.5));
                    rock *= 0.72 + 0.5 * beds;
                    rock *= 0.9 + 0.2 * Noise(at * 9.0);

                    if (isRoot)
                    {
                        albedo = rock * (1.0 - 0.35 * saturate(down / 1.6));
                    }
                    else
                    {
                        half3 soil = ground * (0.82 + 0.24 * Noise(at * 14.0));
                        albedo = lerp(soil, rock, smoothstep(0.16, 0.3, down + ragged * 2.0));
                        // The lip: the cover of the top hangs a little way down the side.
                        albedo = lerp(ground * 1.5 + 0.02, albedo, smoothstep(0.035, 0.06, down + ragged));
                    }
                }

                float3 normal = normalize(input.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(at));
                half3 ambient = SampleSH(normal);
                half direct = saturate(dot(normal, light.direction)) * light.shadowAttenuation;

                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor occlusion = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(input.positionCS));
                    ambient *= occlusion.indirectAmbientOcclusion;
                    direct *= occlusion.directAmbientOcclusion;
                #endif

                return half4(albedo * (ambient + light.color * direct) + glow, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
