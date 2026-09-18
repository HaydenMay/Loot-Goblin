Shader "LootGoblin/Room Clear Doorway"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (0.045, 0.026, 0.018, 1)
        _Brightness("Brightness", Float) = 1
        _GradientStrength("Gradient Strength", Range(0, 1)) = 0.9
        _VariationStrength("Variation Strength", Range(0, 0.25)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "RoomClearDoorway"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Brightness;
                half _GradientStrength;
                half _VariationStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Keep the back wall dark, with only a restrained warm threshold cue
                // near the floor so it reads as a passage rather than a portal card.
                half lowerThreshold = pow(saturate(1.0h - (input.positionOS.y + 0.5h)), 1.35h);
                half edgeShade = lerp(0.72h, 1.0h, saturate(1.0h - abs(input.positionOS.x) * 1.8h));
                half gradient = lerp(1.0h, lerp(0.52h, 0.92h, lowerThreshold) * edgeShade, _GradientStrength);

                // A very small world-space value variation breaks up the uniform card
                // without introducing textures, transparency, or extra overdraw.
                half variation = 1.0h + sin(dot(input.positionWS, half3(3.7h, 5.1h, 6.3h))) * _VariationStrength;
                half3 color = _BaseColor.rgb * (_Brightness * gradient * variation);
                return half4(color, _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
