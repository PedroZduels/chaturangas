Shader "Chaturanga/PurpleSky"
{
    Properties
    {
        _TopColor    ("Top Color",    Color)      = (0.06, 0.00, 0.18, 1)
        _MidColor    ("Mid Color",    Color)      = (0.50, 0.05, 0.65, 1)
        _BottomColor ("Bottom Color", Color)      = (0.80, 0.10, 0.55, 1)
        _CloudColor  ("Cloud Color",  Color)      = (0.70, 0.20, 0.85, 1)
        _CloudDark   ("Cloud Dark",   Color)      = (0.15, 0.00, 0.30, 1)
        _CloudSpeed  ("Cloud Speed",  Float)      = 0.22
        _CloudScale  ("Cloud Scale",  Float)      = 2.5
        _CloudDensity("Cloud Density",Range(0,1)) = 0.32
        _Horizon     ("Horizon",      Range(0,1)) = 0.30
        _PerspPow    ("Persp Power",  Range(0.1,1)) = 0.35
        _Opacity     ("Opacity",      Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "PurpleSky"
            Tags { "LightMode"="Universal2D" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor, _MidColor, _BottomColor, _CloudColor, _CloudDark;
                float  _CloudSpeed, _CloudScale, _CloudDensity, _Horizon, _PerspPow, _Opacity;
            CBUFFER_END

            // ── Value noise ────────────────────────────────────────────────────

            float hash(float2 p)
            {
                p  = frac(p * float2(443.897, 441.423));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float vnoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash(i),               hash(i + float2(1,0)), u.x),
                    lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), u.x),
                    u.y);
            }

            // ── FBM — 5 octaves ────────────────────────────────────────────────

            float fbm(float2 uv)
            {
                float v = 0.0;
                float a = 0.5;
                uv += float2(1.7, 9.2);
                for (int k = 0; k < 5; k++)
                {
                    v  += a * vnoise(uv);
                    uv  = uv * 2.0 + float2(3.1, 1.7);
                    a  *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv         = i.uv;
                o.screenPos  = ComputeScreenPos(o.positionCS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 sc = i.screenPos.xy / i.screenPos.w;
                float  v  = sc.y;
                float  u  = sc.x;

                // ── Sky gradient ─────────────────────────────────────────────
                float4 sky;
                if (v < _Horizon)
                    sky = lerp(_BottomColor, _MidColor, v / max(_Horizon, 0.001));
                else
                {
                    float t = (v - _Horizon) / max(1.0 - _Horizon, 0.001);
                    sky = lerp(_MidColor, _TopColor, saturate(t));
                }

                // Below horizon: no clouds
                if (v <= _Horizon)
                {
                    sky.a = _Opacity;
                    return sky;
                }

                // ── Ceiling-projection vanishing-point convergence ───────────
                float normFrac = max(v - _Horizon, 0.001) / max(1.0 - _Horizon, 0.001);
                float pt       = pow(normFrac, _PerspPow);           // 0=horizon, 1=top
                float worldX   = (u - 0.5) / max(pt, 0.008);        // perspective X

                float t0 = _Time.y * _CloudSpeed;

                float2 uvA = float2(worldX,        pt + t0) * _CloudScale;
                float2 uvB = float2(worldX * 1.3 + 1.7, (pt + t0) * 1.4 + 2.5) * (_CloudScale * 0.6);

                float nA = fbm(uvA);
                float nB = fbm(uvB) * 0.55;
                float n  = nA + nB;

                float threshold = 1.0 - _CloudDensity;
                float cloud = smoothstep(threshold, threshold + 0.40, n);

                // Fade right at the horizon seam and at the very top
                float fade = smoothstep(_Horizon, _Horizon + 0.07, v)
                           * smoothstep(1.0, 0.78, v);
                cloud *= fade;

                float4 cloudCol = lerp(_CloudDark, _CloudColor, saturate(cloud * 0.65 + 0.20));
                float4 result   = saturate(lerp(sky, cloudCol, cloud));
                result.a        = _Opacity;
                return result;
            }
            ENDHLSL
        }
    }
}
