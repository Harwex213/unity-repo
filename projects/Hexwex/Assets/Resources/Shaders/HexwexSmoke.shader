// A puff of smoke, ash or dust: a ball drawn soft. It is see-through at its
// edge and thicker in its middle, lit a little from above, and its colour's
// alpha says how much of it is left. BuildingLife grows a puff and fades it.
Shader "Hexwex/Smoke"
{
    Properties
    {
        _BaseColor ("Colour, alpha is how thick", Color) = (0.8, 0.8, 0.8, 0.6)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Smoke"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(input.normalWS);
                float3 view = normalize(_WorldSpaceCameraPos - input.positionWS);
                // Thick where the ball faces the eye, gone at its rim: no hard edge.
                float facing = saturate(dot(normal, view));
                float soft = pow(facing, 1.6);
                // Lit from the sun a little, never darker than the shade of a cloud.
                Light light = GetMainLight();
                float lit = 0.72 + 0.28 * saturate(dot(normal, light.direction));
                half3 color = _BaseColor.rgb * lit * (0.75 + 0.25 * light.color);

                return half4(color, _BaseColor.a * soft);
            }
            ENDHLSL
        }
    }
}
