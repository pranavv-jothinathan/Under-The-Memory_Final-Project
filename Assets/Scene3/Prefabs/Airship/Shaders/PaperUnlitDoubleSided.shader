Shader "PrefabLibrary/PaperUnlitDoubleSided"
{
    Properties
    {
        [MainTexture] _FrontMap ("Front", 2D) = "white" {}
        _BackMap ("Back", 2D) = "white" {}
        [HideInInspector] _BaseMap ("Base", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
        }

        Cull Off
        ZWrite On
        ZTest LEqual

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FrontMap);
            SAMPLER(sampler_FrontMap);
            TEXTURE2D(_BackMap);
            SAMPLER(sampler_BackMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _FrontMap_ST;
                float4 _BackMap_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _FrontMap);
                return output;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                bool isFront = IS_FRONT_VFACE(face, true, false);
                float2 uv = input.uv;
                if (!isFront)
                    uv.x = 1.0 - uv.x;

                half4 color = isFront
                    ? SAMPLE_TEXTURE2D(_FrontMap, sampler_FrontMap, uv)
                    : SAMPLE_TEXTURE2D(_BackMap, sampler_BackMap, uv);
                color *= _BaseColor;
                clip(color.a - _Cutoff);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ColorMask R
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FrontMap);
            SAMPLER(sampler_FrontMap);
            TEXTURE2D(_BackMap);
            SAMPLER(sampler_BackMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _FrontMap_ST;
                float4 _BackMap_ST;
                half4 _BaseColor;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings VertDepth(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _FrontMap);
                return output;
            }

            half FragDepth(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                bool isFront = IS_FRONT_VFACE(face, true, false);
                float2 uv = input.uv;
                if (!isFront)
                    uv.x = 1.0 - uv.x;

                half alpha = isFront
                    ? SAMPLE_TEXTURE2D(_FrontMap, sampler_FrontMap, uv).a
                    : SAMPLE_TEXTURE2D(_BackMap, sampler_BackMap, uv).a;
                clip(alpha * _BaseColor.a - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
