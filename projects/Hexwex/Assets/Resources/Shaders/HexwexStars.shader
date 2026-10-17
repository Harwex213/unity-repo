// The muted starfield behind the globe, as in the prototype's globe-scene.ts.
// A star is a small square facing the globe; its vertex colour is its tint and
// brightness, uv0 its place in the square, uv1.x its own phase of twinkling.
Shader "Hexwex/Stars"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Background+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Stars"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 phase : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float twinkle = 0.75 + 0.25 * sin(_Time.y * (0.6 + input.phase.x) + input.phase.x * 17.0);
                output.color = input.color.rgb * twinkle;
                output.uv = input.uv;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float falloff = 1.0 - smoothstep(0.0, 0.5, length(input.uv - 0.5));

                return half4(input.color * falloff * falloff, 1.0);
            }
            ENDHLSL
        }
    }
}
