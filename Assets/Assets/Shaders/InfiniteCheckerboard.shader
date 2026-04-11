Shader "Chaturanga/InfiniteCheckerboard"
{
    Properties
    {
        _LightColor   ("Light Square Color", Color) = (0.72, 0.72, 0.90, 1)
        _DarkColor    ("Dark Square Color",  Color) = (0.03, 0.02, 0.10, 1)
        _TileSize     ("Tile Size",          Float) = 1.0
        _FadeStart    ("Fade Start Distance",Float) = 18.0
        _FadeEnd      ("Fade End Distance",  Float) = 65.0
        _HorizonColor ("Horizon Color",      Color) = (0.16, 0.10, 0.30, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "InfiniteCheckerboard"
            Tags { "LightMode"="SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _LightColor, _DarkColor, _HorizonColor;
                float  _TileSize, _FadeStart, _FadeEnd;
            CBUFFER_END

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = vp.positionCS;
                o.positionWS = vp.positionWS;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 tile    = floor(i.positionWS.xz / _TileSize);
                float  checker = fmod(abs(tile.x + tile.y), 2.0);
                float4 col     = lerp(_DarkColor, _LightColor, step(0.5, checker));

                float dist = length(i.positionWS.xz - _WorldSpaceCameraPos.xz);
                float fade = saturate((dist - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
                return lerp(col, _HorizonColor, fade);
            }
            ENDHLSL
        }
    }
}
