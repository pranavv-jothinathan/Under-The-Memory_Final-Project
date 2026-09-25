Shader "Scene3/MatrixRain"
{
    Properties
    {
        [Header(Glyphs)]
        [NoScaleOffset] _GlyphAtlas ("Glyph Atlas", 2D) = "white" {}
        _AtlasColumns ("Atlas Columns", Float) = 16
        _AtlasRows ("Atlas Rows", Float) = 16
        _GridColumns ("Grid Columns", Float) = 24
        _GridRows ("Grid Rows", Float) = 32
        _GlyphChangeRate ("Glyph Change Rate", Range(0, 30)) = 6

        [Header(Rain)]
        _FallSpeed ("Fall Speed", Range(0, 10)) = 0.25
        _SpeedVariance ("Speed Variance", Range(0, 1)) = 0.6
        _TrailLength ("Trail Length", Range(0.02, 2)) = 0.45
        _TrailFalloff ("Trail Falloff", Range(0.2, 8)) = 2
        _ColumnDensity ("Column Density", Range(0, 1)) = 0.8
        _Flicker ("Flicker", Range(0, 1)) = 0.2
        _Seed ("Seed", Float) = 0

        [Header(Color)]
        [HDR] _MatrixColor ("Matrix Color", Color) = (0, 1, 0.18, 1)
        [HDR] _HeadColor ("Head Color", Color) = (0.7, 1, 0.8, 1)
        _HeadSharpness ("Head Sharpness", Range(1, 32)) = 10
        _Brightness ("Brightness", Range(0, 8)) = 1.5
        _Alpha ("Alpha", Range(0, 1)) = 1

        [Header(Fade)]
        _NearFadeStart ("Near Fade Start", Float) = 0
        _NearFadeEnd ("Near Fade End", Float) = 0
        _FogAmount ("Fog Amount", Range(0, 1)) = 0

        [Header(Blending)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "MatrixRainForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_GlyphAtlas);
            SAMPLER(sampler_GlyphAtlas);

            CBUFFER_START(UnityPerMaterial)
                float _AtlasColumns;
                float _AtlasRows;
                float _GridColumns;
                float _GridRows;
                float _GlyphChangeRate;
                float _FallSpeed;
                float _SpeedVariance;
                float _TrailLength;
                float _TrailFalloff;
                float _ColumnDensity;
                float _Flicker;
                float _Seed;
                float4 _MatrixColor;
                float4 _HeadColor;
                float _HeadSharpness;
                float _Brightness;
                float _Alpha;
                float _NearFadeStart;
                float _NearFadeEnd;
                float _FogAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Dave Hoskins hash, cheap and stable across GPUs.
            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions =
                    GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 grid = float2(max(_GridColumns, 1.0), max(_GridRows, 1.0));
                float2 atlas = float2(max(_AtlasColumns, 1.0), max(_AtlasRows, 1.0));

                float2 cell = input.uv * grid;
                float columnId = floor(cell.x);
                float rowId = floor(cell.y);

                float randSpeed = Hash11(columnId + _Seed * 7.0);
                float randPhase = Hash11(columnId * 3.7 + _Seed * 13.0 + 91.3);
                float randOn = Hash11(columnId * 9.1 + _Seed * 23.0 + 17.7);

                float active = step(1.0 - _ColumnDensity, randOn);

                float speed = _FallSpeed *
                    lerp(1.0 - _SpeedVariance, 1.0 + _SpeedVariance, randSpeed);

                float trail = max(_TrailLength, 1e-3);

                // Row centre measured downward from the top edge, so the whole
                // cell shares one brightness instead of a smooth pixel gradient.
                float rowCentre = 1.0 - (rowId + 0.5) / grid.y;

                // The head runs from one trail-length above the top edge to one
                // trail-length below the bottom edge. Both ends of that range are
                // fully dark, so the wrap-around is invisible instead of blinking
                // the whole column off.
                float head = frac(_Time.y * speed + randPhase) * (1.0 + 2.0 * trail) - trail;
                float behindHead = head - rowCentre;

                float fade = saturate(1.0 - behindHead / trail);
                fade = behindHead < 0.0 ? 0.0 : pow(fade, _TrailFalloff);

                float headMask = saturate(1.0 - behindHead * grid.y * 0.5);
                headMask = behindHead < 0.0 ? 0.0 : pow(headMask, _HeadSharpness);

                float2 cellId = float2(columnId, rowId);
                float cellRand = Hash21(cellId + _Seed * 3.3);
                float glyphStep = floor(
                    _Time.y * _GlyphChangeRate * (0.5 + cellRand) + cellRand * 23.0
                );
                float glyphRand = Hash21(cellId * 1.31 + glyphStep * 7.77 + _Seed);

                float glyphCount = atlas.x * atlas.y;
                float glyphIndex = min(floor(glyphRand * glyphCount), glyphCount - 1.0);
                float glyphX = fmod(glyphIndex, atlas.x);
                float glyphY = floor(glyphIndex / atlas.x);

                // Atlas cell 0 is the top-left one, matching the baker's layout.
                float2 inCell = frac(cell);
                float2 atlasUV = float2(
                    (glyphX + inCell.x) / atlas.x,
                    1.0 - (glyphY + 1.0 - inCell.y) / atlas.y
                );

                // frac() breaks automatic mip selection at every cell border, so
                // derive the gradients from the continuous coordinate instead.
                float2 continuousUV = input.uv * grid / atlas;
                half4 glyph = SAMPLE_TEXTURE2D_GRAD(
                    _GlyphAtlas,
                    sampler_GlyphAtlas,
                    atlasUV,
                    ddx(continuousUV),
                    ddy(continuousUV)
                );

                float flickerRand = Hash21(cellId + floor(_Time.y * 11.0) * 1.7);
                float flicker = 1.0 - _Flicker * flickerRand;

                float nearFade = 1.0;
                if (_NearFadeEnd > _NearFadeStart)
                {
                    float viewDistance = distance(input.positionWS, GetCameraPositionWS());
                    nearFade = saturate(
                        (viewDistance - _NearFadeStart) /
                        (_NearFadeEnd - _NearFadeStart)
                    );
                }

                float coverage = glyph.a * fade * active * flicker * nearFade;

                float fogAtten = 1.0;
                #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
                    // Additive blending has no fog colour to blend toward, so fog
                    // has to attenuate the emission instead.
                    fogAtten = lerp(1.0, saturate(input.fogFactor), _FogAmount);
                #endif

                float3 tint = lerp(_MatrixColor.rgb, _HeadColor.rgb, headMask);
                float alpha = coverage * _Alpha;
                float3 rgb = tint * coverage * _Brightness * fogAtten * _Alpha;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
