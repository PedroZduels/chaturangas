Shader "Chaturanga/ScanlinesOverlay"
{
    // Subtle CRT scanlines overlay – a nearly-transparent darkening grid
    // placed over the board and pieces.
    Properties
    {
        _TintColor      ("Tint Color",          Color)       = (0, 0, 0, 1)
        _BandAlpha      ("Scanline Alpha",      Range(0,1))  = 0.08
        _BandFrequency  ("Scanline Frequency",  Float)       = 140.0
        _BandWidth      ("Scanline Width",      Range(0,1))  = 0.45
        _ScrollSpeed    ("Scroll Speed",        Float)       = 0.18
        _FlickerSpeed   ("Flicker Speed",       Float)       = 8.0
        _FlickerAmount  ("Flicker Amount",      Range(0,1))  = 0.12
        _VignetteStr    ("Vignette Strength",   Range(0,1))  = 0.10
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ScanlinesOverlay"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   VertMain
            #pragma fragment FragMain
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float  _BandAlpha;
                float  _BandFrequency;
                float  _BandWidth;
                float  _ScrollSpeed;
                float  _FlickerSpeed;
                float  _FlickerAmount;
                float  _VignetteStr;
            CBUFFER_END

            Varyings VertMain(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = IN.uv;
                return OUT;
            }

            float4 FragMain(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float  t  = _Time.y;

                // Downward scroll – offset UV.y by time
                float scrolledY = uv.y + t * _ScrollSpeed;

                // Horizontal dark bands scrolling downward
                float bandPos  = frac(scrolledY * _BandFrequency);
                float bandMask = step(1.0 - _BandWidth, bandPos);

                // Flicker – cheap high-freq hash on time to jitter alpha
                float flicker = frac(sin(floor(t * _FlickerSpeed) * 127.1 + 311.7) * 43758.5453);
                float flickerMod = 1.0 - flicker * _FlickerAmount;

                // Soft vignette darkening toward corners
                float2 centred   = uv * 2.0 - 1.0;
                float  vigEdge   = saturate(1.0 - dot(centred, centred) * 0.5);
                float  vigDark   = (1.0 - vigEdge) * _VignetteStr;

                // Final alpha: animated scanlines + vignette
                float alpha = saturate(bandMask * _BandAlpha * flickerMod + vigDark);

                return float4(_TintColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
