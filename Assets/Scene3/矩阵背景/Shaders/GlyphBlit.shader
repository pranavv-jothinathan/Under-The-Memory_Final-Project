// 只给 MatrixGlyphAtlasBaker 用：把动态字体图集里的一个字形画进目标格子，
// 输出白色 RGB + 覆盖度 Alpha。字体图集可能是 A8 也可能是 R8，所以覆盖度
// 走哪个通道由 _AutoChannel / _ChannelMask 决定。
Shader "Hidden/Scene3/GlyphBlit"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _ChannelMask ("Channel Mask", Vector) = (0, 0, 0, 1)
        _AutoChannel ("Auto Channel", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" }

        Lighting Off
        ZWrite Off
        ZTest Always
        Cull Off
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _ChannelMask;
            float _AutoChannel;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.pos = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float4 source = tex2D(_MainTex, input.uv);

                float coverage = _AutoChannel > 0.5
                    ? max(source.a, source.r)
                    : dot(source, _ChannelMask);

                return float4(1.0, 1.0, 1.0, saturate(coverage));
            }
            ENDCG
        }
    }
}
