Shader "Chaturanga/WaterSurface"
{
    // Animated water plane with ripples, Fresnel reflection and a horizon fade.
    // Drop it on a flat quad / plane mesh (the CheckerFloor object).
    Properties
    {
        _ShallowColor  ("Shallow Color",      Color)  = (0.15, 0.35, 0.55, 1)
        _DeepColor     ("Deep Color",         Color)  = (0.04, 0.10, 0.22, 1)
        _HorizonColor  ("Horizon Fog Color",  Color)  = (0.08, 0.18, 0.32, 1)
        _FadeStart     ("Fade Start Dist",    Float)  = 18.0
        _FadeEnd       ("Fade End Dist",      Float)  = 60.0

        // ── Wave params ────────────────────────────────────────────────────────
        _WaveSpeed     ("Wave Speed",         Float)  = 0.55
        _WaveScale     ("Wave Scale",         Float)  = 2.8
        _WaveHeight    ("Wave Height (UV)",   Float)  = 0.04

        // ── Ripple normal FBM ──────────────────────────────────────────────────
        _RippleScale   ("Ripple Scale",       Float)  = 5.0
        _RippleSpeed   ("Ripple Speed",       Float)  = 0.30

        // ── Specular ───────────────────────────────────────────────────────────
        _Smoothness    ("Smoothness",         Range(0,1)) = 0.92
        _SpecStrength  ("Specular Strength",  Float)  = 2.5

        // ── Fresnel reflection ─────────────────────────────────────────────────
        _FresnelPow    ("Fresnel Power",      Range(0.5,4)) = 1.5
        _ReflBlend     ("Reflection Blend",   Range(0,1))   = 0.55

        // ── Sky colours used for fake reflection ───────────────────────────────
        _SkyTop        ("Sky Top",    Color) = (0.04, 0.00, 0.14, 1)
        _SkyMid        ("Sky Mid",    Color) = (0.44, 0.04, 0.60, 1)
        _SkyBottom     ("Sky Bottom", Color) = (0.78, 0.06, 0.50, 1)
        _SkyHorizon    ("Sky Horizon",Range(0,1)) = 0.618
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "WaterSurface"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor, _DeepColor, _HorizonColor;
                float  _FadeStart, _FadeEnd;
                float  _WaveSpeed, _WaveScale, _WaveHeight;
                float  _RippleScale, _RippleSpeed;
                float  _Smoothness, _SpecStrength;
                float  _FresnelPow, _ReflBlend;
                float4 _SkyTop, _SkyMid, _SkyBottom;
                float  _SkyHorizon;
            CBUFFER_END

            // ── Helpers ──────────────────────────────────────────────────────────

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float SmoothNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Multi-octave noise for ripple normals
            float Fbm(float2 uv)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; ++k)
                {
                    v  += a * SmoothNoise(uv);
                    uv  = uv * 2.13 + float2(1.7, 3.3);
                    a  *= 0.5;
                }
                return v;
            }

            float3 SampleSkyRefl(float3 reflDir)
            {
                float v = saturate(reflDir.y * 0.5 + 0.5);
                float3 sky;
                if (v < _SkyHorizon)
                    sky = lerp(_SkyBottom.rgb, _SkyMid.rgb, v / max(_SkyHorizon, 0.001));
                else
                    sky = lerp(_SkyMid.rgb, _SkyTop.rgb,
                               saturate((v - _SkyHorizon) / max(1.0 - _SkyHorizon, 0.001)));
                return sky;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vn = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = vp.positionCS;
                OUT.positionWS = vp.positionWS;
                OUT.normalWS   = vn.normalWS;
                OUT.viewDirWS  = GetWorldSpaceViewDir(vp.positionWS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.positionWS.xz;
                float  t  = _Time.y;

                // ── Animated ripple normals (two scrolling layers) ──────────────
                float2 uv1 = uv * _RippleScale + float2(t * _RippleSpeed, t * _RippleSpeed * 0.7);
                float2 uv2 = uv * _RippleScale * 0.7 - float2(t * _RippleSpeed * 0.8, t * _RippleSpeed * 0.5);

                float h1 = Fbm(uv1);
                float h2 = Fbm(uv2);

                // Derive a perturbed normal from the height field gradient
                float eps = 0.05;
                float3 rippleN = normalize(float3(
                    Fbm(uv1 + float2(eps,0)) - h1 + Fbm(uv2 + float2(eps,0)) - h2,
                    1.0,
                    Fbm(uv1 + float2(0,eps)) - h1 + Fbm(uv2 + float2(0,eps)) - h2
                ));

                // Blend ripple normal with geometric (flat) normal
                float3 geoN  = normalize(IN.normalWS);
                float3 N     = normalize(lerp(geoN, rippleN, 0.45));
                float3 V     = normalize(IN.viewDirWS);
                float  NdotV = saturate(dot(N, V));

                // ── Water depth colour ─────────────────────────────────────────
                // Use large-scale wave to modulate shallow/deep blend
                float waveMix = SmoothNoise(uv * _WaveScale * 0.3 + float2(t * _WaveSpeed * 0.2, 0));
                float3 waterCol = lerp(_DeepColor.rgb, _ShallowColor.rgb, waveMix * 0.5 + 0.25);

                // ── Fresnel sky reflection ─────────────────────────────────────
                float3 reflDir   = reflect(-V, N);
                float3 skyRefl   = SampleSkyRefl(reflDir);
                float  fresnel   = pow(1.0 - NdotV, _FresnelPow);
                float3 reflColor = skyRefl * fresnel * _ReflBlend;

                // ── Directional specular ───────────────────────────────────────
                float3 lightDir = normalize(float3(-0.6, 0.8, 0.5));
                float3 H        = normalize(V + lightDir);
                float  NdotH    = saturate(dot(N, H));
                float  specExp  = exp2(_Smoothness * 10.0 + 2.0);
                float3 specCol  = float3(1.0, 1.0, 1.0) * pow(NdotH, specExp) * _SpecStrength;

                float3 col = waterCol + reflColor + specCol;

                // ── Horizon distance fade ──────────────────────────────────────
                float dist = length(IN.positionWS.xz - _WorldSpaceCameraPos.xz);
                float fade = saturate((dist - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
                col = lerp(col, _HorizonColor.rgb, fade);

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
