// A cell of the globe, drawn the way the prototype draws its planet
// (ui/components/globe-shaders.ts): as an antique map. The sea carries inked
// wave strokes, unexplored cells are hatched mist, every border is a sepia
// line, and hover, selection, reachable cells, owners, the toxic stain and the
// boss's lair are bands and blots of ink. Aged paper lies over everything and
// the drawing burns dark toward the limb.
//
// The prototype paints this on a smooth sphere. Here each cell is a raised
// slab, so the mesh carries what the prototype's did per cell: the place in
// the cell's own plane (uv0) and how near the border it is (uv1.x). uv1.y
// marks a wall of the slab. What a cell is comes in a property block.
Shader "Hexwex/Globe"
{
    Properties
    {
        _BaseColor ("Ground", Color) = (0.17, 0.21, 0.23, 1)
        _Paper ("Paper", 2D) = "white" {}
        // x: 0 unexplored, 0.5 frontier, 1 revealed. y: toxic 0..1. z: a number of the cell's own. w: 0 sea, 1 land, 2 lair.
        _Cell ("Cell", Vector) = (1, 0, 0, 0)
        // rgb: the owner's colour. a: 0 nobody, 0.5 a player, 1 the human's home.
        _Owner ("Owner", Color) = (1, 1, 1, 0)
        // x: hovered. y: selected. z: reachable.
        _Mark ("Mark", Vector) = (0, 0, 0, 0)
        _Center ("Centre of the globe", Vector) = (0, 0, 0, 0)
        _Grid ("Grid line", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Map"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Paper);
            SAMPLER(sampler_Paper);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _Paper_ST;
                float4 _Cell;
                half4 _Owner;
                float4 _Mark;
                float4 _Center;
                float _Grid;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 local : TEXCOORD0;
                float2 edge : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 local : TEXCOORD1;
                float2 edge : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.local = input.local;
                output.edge = input.edge;

                return output;
            }

            // The colours below are written as they look, like the prototype's.
            half3 FromSrgb(half3 c)
            {
                return pow(c, 2.2);
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);

                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(lerp(Hash13(i), Hash13(i + float3(1, 0, 0)), f.x), lerp(Hash13(i + float3(0, 1, 0)), Hash13(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash13(i + float3(0, 0, 1)), Hash13(i + float3(1, 0, 1)), f.x), lerp(Hash13(i + float3(0, 1, 1)), Hash13(i + float3(1, 1, 1)), f.x), f.y),
                    f.z);
            }

            // A thin ink line along the zero of a field, about a pixel wide.
            float InkLine(float field, float width)
            {
                float fw = fwidth(field);

                return 1.0 - smoothstep(width - fw, width + fw, abs(field));
            }

            // Short wavy strokes over the sea, as an engraver draws water.
            float SeaWaves(float2 local, float3 p)
            {
                float rows = local.y * 5.0 + ValueNoise(p * 12.0) * 1.2;
                float stroke = frac(rows + sin(local.x * 13.0) * 0.12) - 0.5;
                float dash = smoothstep(0.5, 0.6, ValueNoise(float3(local * 4.0, floor(rows))));

                return InkLine(stroke, 0.07) * dash;
            }

            // Diagonal ink hatching, for unexplored mist.
            float Hatching(float2 local, float density)
            {
                return InkLine(frac((local.x + local.y) * density) - 0.5, 0.14);
            }

            // A band just inside the cell border.
            float EdgeBand(float edge, float width)
            {
                float fw = fwidth(edge);

                return smoothstep(1.0 - width - fw, 1.0 - width + fw, edge);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 p = normalize(input.positionWS - _Center.xyz);
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float2 local = input.local;
                float edge = input.edge.x;
                bool isWall = input.edge.y > 0.5;
                float visibility = _Cell.x;
                float toxic = _Cell.y;
                float seed = _Cell.z;
                bool isSea = _Cell.w < 0.5;
                bool isLair = _Cell.w > 1.5;
                float time = _Time.y;
                float pulse = 0.5 + 0.5 * sin(time * 3.0);

                half3 ink = FromSrgb(half3(0.16, 0.12, 0.08));
                half3 cream = FromSrgb(half3(0.91, 0.85, 0.71));
                half3 ground = _BaseColor.rgb;
                half3 color = ground * (0.9 + 0.2 * ValueNoise(p * 6.0));

                if (visibility > 0.75)
                {
                    if (isSea)
                    {
                        color = lerp(color, FromSrgb(half3(0.62, 0.68, 0.7)), SeaWaves(local, p) * 0.5);
                    }
                    else
                    {
                        // Land is washed in, darker in patches, as with a wide brush.
                        color *= 0.9 + 0.18 * ValueNoise(p * 26.0 + seed);
                    }

                    // The toxic trail: an inked stain that soaks the cell from the middle.
                    if (toxic > 0.001)
                    {
                        float blot = ValueNoise(p * 28.0) * 0.6 + ValueNoise(p * 70.0) * 0.4 + toxic * 0.55 - edge * 0.35;
                        float stain = smoothstep(0.55, 0.6, blot);
                        float rim = smoothstep(0.5, 0.55, blot) - stain;
                        color = lerp(color, color * FromSrgb(half3(0.74, 0.42, 0.86)), stain * 0.85);
                        color = lerp(color, FromSrgb(half3(0.24, 0.07, 0.3)), rim * 0.8);
                    }
                }
                else
                {
                    // Unexplored: mist drawn in hatching. The frontier is lighter and hatched wider.
                    bool isFrontier = visibility > 0.25;
                    color = ground * (0.88 + 0.24 * ValueNoise(p * 14.0 + time * 0.03));
                    color = lerp(color, isFrontier ? cream * 0.5 : ground * 0.35, Hatching(local, isFrontier ? 4.0 : 6.0) * (isFrontier ? 0.35 : 0.5));
                }

                // The grid: a faint sepia line on every border.
                color = lerp(color, FromSrgb(half3(0.72, 0.63, 0.45)), EdgeBand(edge, 0.03) * _Grid * (0.45 + 0.55 * visibility));

                // The boss's lair: a smouldering crimson glow and a pulsing blood-red rim.
                if (isLair && visibility > 0.75)
                {
                    float ember = ValueNoise(p * 40.0 + float3(0.0, time * 0.6, 0.0));
                    float glow = 0.3 + 0.35 * ember + 0.25 * edge;
                    color = lerp(color, FromSrgb(half3(0.5, 0.05, 0.03)), glow * (0.65 + 0.35 * pulse));
                    color = lerp(color, FromSrgb(half3(0.95, 0.35, 0.12)), EdgeBand(edge, 0.14) * (0.6 + 0.4 * pulse));
                    color = lerp(color, ink, EdgeBand(edge, 0.03) * 0.9);
                }

                if (_Mark.z > 0.5)
                {
                    // Where the island can fly: a dashed gold band that crawls round the cell.
                    float dash = step(0.5, frac(atan2(local.y, local.x) / TWO_PI * 18.0 + time * 0.25));
                    color = lerp(color, FromSrgb(half3(0.95, 0.83, 0.5)), EdgeBand(edge, 0.1) * dash * (0.7 + 0.3 * pulse));
                }

                if (_Owner.a > 0.25)
                {
                    bool isHome = _Owner.a > 0.75;
                    float band = EdgeBand(edge, isHome ? 0.16 : 0.12);
                    color = lerp(color, ink, EdgeBand(edge, 0.03) * 0.9);
                    color = lerp(color, _Owner.rgb, band * (1.0 - EdgeBand(edge, 0.03)) * (isHome ? 0.75 + 0.25 * pulse : 0.85));
                }

                if (_Mark.x > 0.5)
                {
                    color = lerp(color, cream, 0.14);
                    color = lerp(color, cream, EdgeBand(edge, 0.05) * 0.9);
                }

                if (_Mark.y > 0.5)
                {
                    color = lerp(color, FromSrgb(half3(0.72, 0.12, 0.09)), EdgeBand(edge, 0.16));
                    color = lerp(color, cream, EdgeBand(edge, 0.04) * 0.85);
                }

                // The wall of a slab is the same ground in shade.
                if (isWall)
                {
                    color = ground * 0.42 * (0.85 + 0.3 * ValueNoise(p * 30.0));
                }

                // What stands on the cells throws its shadow on the map.
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                color *= lerp(0.62, 1.0, light.shadowAttenuation);

                // Aged paper over the whole drawing, and a burnt edge toward the limb.
                float2 paperUv = float2(atan2(p.z, p.x) / TWO_PI * 6.0, asin(clamp(p.y, -1.0, 1.0)) / PI * 3.0);
                half3 paper = SAMPLE_TEXTURE2D(_Paper, sampler_Paper, paperUv).rgb;
                color *= lerp(half3(1, 1, 1), paper / max(Luminance(paper), 0.05) * 0.95, 0.4);

                float facing = max(dot(p, viewDir), 0.0);
                float burn = 1.0 - smoothstep(0.05, 0.75, facing);
                color = lerp(color, FromSrgb(half3(0.25, 0.16, 0.08)) * color * 2.0, burn * 0.55);
                color *= 0.45 + 0.55 * smoothstep(0.0, 0.35, facing);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
