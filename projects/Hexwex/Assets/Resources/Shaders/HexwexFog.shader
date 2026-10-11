// The fog round the battle. A sheet of it is a flat mesh whose vertex alpha says
// how thick the fog is there; this shader breaks the thin fog into drifting
// wisps and leaves the thick fog whole. It is unlit and writes no depth, so
// sheets laid one over another add up. The view loads it by name from Resources.
Shader "Hexwex/Fog"
{
    Properties
    {
        _Color ("Lit fog", Color) = (0.9, 0.93, 0.98, 1)
        _Shade ("Shaded fog", Color) = (0.66, 0.74, 0.87, 1)
        _Scale ("Wisps per world unit", Float) = 0.085
        _Drift ("Drift, world units per second", Vector) = (0.35, 0.16, 0, 0)
        _Wisp ("How far thin fog breaks up", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Fog"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Shade;
                float _Scale;
                float4 _Drift;
                float _Wisp;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                // x: which sheet this is, so no two sheets drift alike.
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float3 plane : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                output.plane = float3(positionWS.xz, input.uv.x);

                return output;
            }

            float Hash(float2 cell)
            {
                cell = frac(cell * float2(0.1031, 0.1030));
                cell += dot(cell, cell.yx + 33.33);

                return frac((cell.x + cell.y) * cell.y);
            }

            // Smooth value noise, 0..1.
            float Noise(float2 at)
            {
                float2 cell = floor(at);
                float2 inside = frac(at);
                inside = inside * inside * (3.0 - 2.0 * inside);
                float low = lerp(Hash(cell), Hash(cell + float2(1, 0)), inside.x);
                float high = lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), inside.x);

                return lerp(low, high, inside.y);
            }

            float Billow(float2 at)
            {
                return Noise(at) * 0.55 + Noise(at * 2.1 + 7.3) * 0.3 + Noise(at * 4.3 + 19.1) * 0.15;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float sheet = input.plane.z;
                float2 drift = _Time.y * _Drift.xy * (1.0 + sheet * 0.45);
                float2 at = (input.plane.xy + drift) * _Scale * (1.0 + sheet * 0.2) + sheet * 17.3;
                float billow = Billow(at);

                float thick = input.color.a;
                // Thin fog shows only where the billows are; thick fog is whole.
                float wisps = saturate((billow - 0.28) * 2.6);
                float alpha = saturate(thick * lerp(1.0, wisps * 1.5, _Wisp * (1.0 - thick)));
                half3 color = lerp(_Shade.rgb, _Color.rgb, saturate(billow * 1.25)) * input.color.rgb;

                return half4(color, alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
