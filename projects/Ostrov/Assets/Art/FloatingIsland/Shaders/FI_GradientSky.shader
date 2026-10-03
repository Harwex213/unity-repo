// A flat storm-sky gradient for the low-poly Floating Island level.
// Below the horizon the colour runs from the horizon mist down to a dark abyss.
// Above the horizon it runs from the horizon mist up to dark storm clouds.
Shader "Ostrov/FloatingIsland/GradientSky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.12, 0.13, 0.15, 1)
        _HorizonColor ("Horizon", Color) = (0.42, 0.45, 0.5, 1)
        _MistColor ("Mist (just below horizon)", Color) = (0.36, 0.39, 0.44, 1)
        _AbyssColor ("Abyss", Color) = (0.1, 0.11, 0.13, 1)
        _UpPower ("Up falloff", Range(0.2, 4)) = 0.6
        _MistEnd ("Mist end (sin of angle below horizon)", Range(0.05, 1)) = 0.35
        _AbyssStart ("Abyss start", Range(0.05, 1)) = 0.95
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor;
                half4 _HorizonColor;
                half4 _MistColor;
                half4 _AbyssColor;
                half _UpPower;
                half _MistEnd;
                half _AbyssStart;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float y = normalize(i.dir).y;
                half3 up = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(saturate(y), _UpPower));
                float d = saturate(-y);
                half3 down = lerp(_HorizonColor.rgb, _MistColor.rgb, smoothstep(0.0, _MistEnd, d));
                down = lerp(down, _AbyssColor.rgb, smoothstep(_MistEnd, _AbyssStart, d));
                return half4(y >= 0 ? up : down, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
