// The ground of a hex: the top of the tile, its sides, and the rock under it.
// One colour comes in for each (the biome's, or the toxic tint over it) and the
// shader makes land of it. The top is mottled like turf or sand and darkens
// toward its rim. A side is cut through the island: a lip of the top's own
// cover, then soil, then layered rock.
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

            half4 Frag(Varyings input) : SV_Target
            {
                float3 at = input.positionWS;
                half3 ground = _BaseColor.rgb;
                half3 albedo;

                if (input.part.y < 0.5)
                {
                    // The top: fine grain, wider patches of lighter and darker cover, and a worn rim.
                    float grain = Noise(at * 11.0) * 0.5 + Noise(at * 27.0) * 0.5;
                    float patches = Noise(at * 1.7 + 13.0);
                    albedo = ground * (0.86 + 0.2 * grain);
                    albedo = lerp(albedo, ground * 1.16 + 0.015, smoothstep(0.52, 0.8, patches) * 0.6);
                    albedo = lerp(albedo, ground * 0.78, smoothstep(0.5, 0.2, patches) * 0.45);
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

                return half4(albedo * (ambient + light.color * direct), 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
