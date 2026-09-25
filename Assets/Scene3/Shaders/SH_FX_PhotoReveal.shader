Shader "BOM/FX/Photo Reconstruction Reveal"
{
    Properties
    {
        [MainTexture]
        _BaseMap(
            "Base Map",
            2D
        ) = "white" {}

        [MainColor]
        _BaseColor(
            "Base Color",
            Color
        ) = (1, 1, 1, 1)

        _Reveal(
            "Reveal",
            Range(0, 1)
        ) = 0

        _NoiseScale(
            "Reveal Noise Scale",
            Range(0.1, 50)
        ) = 8

        _AmbientStrength(
            "Ambient Strength",
            Range(0, 1)
        ) = 0.35

        _LightStrength(
            "Main Light Strength",
            Range(0, 2)
        ) = 1

        _EdgeWidth(
            "Reconstruction Edge Width",
            Range(0, 0.2)
        ) = 0.035

        [HDR]
        _EdgeColor(
            "Reconstruction Edge Color",
            Color
        ) = (0.45, 0.75, 1.0, 1.0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "PhotoRevealForward"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS    : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;

                float _Reveal;
                float _NoiseScale;
                float _AmbientStrength;
                float _LightStrength;
                float _EdgeWidth;

                half4 _EdgeColor;
            CBUFFER_END

            float Hash31(float3 position)
            {
                position = frac(
                    position * 0.1031
                );

                position += dot(
                    position,
                    position.yzx + 33.33
                );

                return frac(
                    (position.x + position.y) *
                    position.z
                );
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        input.positionOS.xyz
                    );

                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(
                        input.normalOS
                    );

                output.positionCS =
                    positionInputs.positionCS;

                output.positionWS =
                    positionInputs.positionWS;

                output.normalWS =
                    normalInputs.normalWS;

                output.uv =
                    TRANSFORM_TEX(
                        input.uv,
                        _BaseMap
                    );

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 quantizedWorldPosition =
                    floor(
                        input.positionWS *
                        _NoiseScale
                    );

                float revealNoise =
                    Hash31(
                        quantizedWorldPosition
                    );

                clip(
                    _Reveal -
                    revealNoise
                );

                half4 baseSample =
                    SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        input.uv
                    );

                half3 normalWS =
                    normalize(
                        input.normalWS
                    );

                float4 shadowCoord =
                    TransformWorldToShadowCoord(
                        input.positionWS
                    );

                Light mainLight =
                    GetMainLight(
                        shadowCoord
                    );

                half normalDotLight =
                    saturate(
                        dot(
                            normalWS,
                            mainLight.direction
                        )
                    );

                half directLighting =
                    normalDotLight *
                    mainLight.shadowAttenuation *
                    mainLight.distanceAttenuation *
                    _LightStrength;

                half lighting =
                    _AmbientStrength +
                    directLighting;

                half3 surfaceColor =
                    baseSample.rgb *
                    _BaseColor.rgb *
                    lighting;

                float edgeMask = 0;

                if (_EdgeWidth > 0.0001)
                {
                    edgeMask =
                        1.0 -
                        smoothstep(
                            0.0,
                            _EdgeWidth,
                            _Reveal -
                            revealNoise
                        );
                }

                surfaceColor =
                    lerp(
                        surfaceColor,
                        _EdgeColor.rgb,
                        saturate(edgeMask)
                    );

                return half4(
                    surfaceColor,
                    1
                );
            }

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"

            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM

            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;

                float _Reveal;
                float _NoiseScale;
                float _AmbientStrength;
                float _LightStrength;
                float _EdgeWidth;

                half4 _EdgeColor;
            CBUFFER_END

            float ShadowHash31(float3 position)
            {
                position = frac(
                    position * 0.1031
                );

                position += dot(
                    position,
                    position.yzx + 33.33
                );

                return frac(
                    (position.x + position.y) *
                    position.z
                );
            }

            ShadowVaryings ShadowVert(
                ShadowAttributes input
            )
            {
                ShadowVaryings output;

                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(
                        input.positionOS.xyz
                    );

                output.positionCS =
                    positionInputs.positionCS;

                output.positionWS =
                    positionInputs.positionWS;

                return output;
            }

            half4 ShadowFrag(
                ShadowVaryings input
            ) : SV_Target
            {
                float3 quantizedWorldPosition =
                    floor(
                        input.positionWS *
                        _NoiseScale
                    );

                float revealNoise =
                    ShadowHash31(
                        quantizedWorldPosition
                    );

                clip(
                    _Reveal -
                    revealNoise
                );

                return 0;
            }

            ENDHLSL
        }
    }

    FallBack Off
}