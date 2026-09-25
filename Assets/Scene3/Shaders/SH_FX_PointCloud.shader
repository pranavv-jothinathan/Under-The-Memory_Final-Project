Shader "BOM/FX/Point Cloud"
{
    Properties
    {
        _BaseColor(
            "Point Color",
            Color
        ) = (0.75, 0.85, 1.0, 1.0)

        _Softness(
            "Edge Softness",
            Range(0.001, 0.5)
        ) = 0.08

        _EmissionStrength(
            "Emission Strength",
            Range(0.0, 5.0)
        ) = 1.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "PointCloudForward"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Softness;
                float _EmissionStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS =
                    TransformObjectToHClip(
                        input.positionOS.xyz
                    );

                output.uv = input.uv;
                output.color = input.color;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 centeredUV =
                    input.uv * 2.0 - 1.0;

                float distanceFromCentre =
                    length(centeredUV);

                float alpha =
                    1.0 -
                    smoothstep(
                        1.0 - _Softness,
                        1.0,
                        distanceFromCentre
                    );

                clip(alpha - 0.01);

                half4 finalColor =
                    _BaseColor *
                    input.color;

                finalColor.rgb *= _EmissionStrength;
                finalColor.a *= alpha;

                return finalColor;
            }

            ENDHLSL
        }
    }
}