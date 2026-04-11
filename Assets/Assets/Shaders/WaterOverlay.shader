Shader "Chaturanga/WaterOverlay"
{
    // Shallow transparent water sitting just above the checkerboard.
    // Uses alpha-blend so the tiles are visible through the surface.
    Properties
    {
        _WaterColor    ("Water Tint",          Color)  = (0.10, 0.30, 0.55, 0.38)
        _HorizonColor  ("Horizon Tint",        Color)  = (0.05, 0.15, 0.35, 0.65)
        _FadeStart     ("Fade Start",          Float)  = 18.0
        _FadeEnd       ("Fade End",            Float)  = 50.0

        // Ripple normals
        _RippleScale   ("Ripple Scale",        Float)  = 4.0
        _RippleSpeed   ("Ripple Speed",        Float)  = 0.28
        _RippleStrength("Ripple Strength",     Range(0,1)) = 0.35

        // Specular
        _Smoothness    ("Smoothness",          Range(0,1)) = 0.94
        _SpecStrength  ("Specular Strength",   Float)  = 1.8
        _SpecColor2    ("Specular Color",      Color)  = (0.85, 0.92, 1.0, 1)

        // Fresnel
        _FresnelPow    ("Fresnel Power",       Range(0.5,4)) = 2.0
    }

    SubShader
    {
        Tags
        {
            // Draw after the checkerboard (Geometry) but before transparent sky (Transparent)
            // SRPDefaultUnlit is the same pass used by InfiniteCheckerboard, so it renders
            // on the BackgroundCamera. The higher queue sorts this plane on top of the tiles.
            "RenderType"     = "Transparent"
            "Queue"          = "Geometry+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WaterOverlay"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // Render both sides so the plane is visible regardless of normal direction
            Cull Off

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
                float4 _WaterColor, _HorizonColor;
                float  _FadeStart, _FadeEnd;
                float  _RippleScale, _RippleSpeed, _RippleStrength;
                float  _Smoothness, _SpecStrength;
                float4 _SpecColor2;
                float  _FresnelPow;
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
                return lerp(
                    lerp(Hash(i),               Hash(i + float2(1,0)), u.x),
                    lerp(Hash(i + float2(0,1)), Hash(i + float2(1,1)), u.x),
                    u.y);
            }

            float Fbm(float2 uv)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; ++k)
                {
                    v  += a * SmoothNoise(uv);
                    uv  = uv * 2.1 + float2(1.7, 3.3);
                    a  *= 0.5;
                }
                return v;
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

                // Two counter-scrolling FBM layers for ripple normals
                float2 uv1 = uv * _RippleScale + float2( t * _RippleSpeed,       t * _RippleSpeed * 0.7);
                float2 uv2 = uv * _RippleScale * 0.65 - float2(t * _RippleSpeed * 0.9, t * _RippleSpeed * 0.5);

                float h1 = Fbm(uv1);
                float h2 = Fbm(uv2);
                float eps = 0.06;

                float3 rippleN = normalize(float3(
                    (Fbm(uv1 + float2(eps,0)) - h1) + (Fbm(uv2 + float2(eps,0)) - h2),
                    1.0 / max(_RippleStrength, 0.001),
                    (Fbm(uv1 + float2(0,eps)) - h1) + (Fbm(uv2 + float2(0,eps)) - h2)
                ));

                float3 geoN = normalize(IN.normalWS);
                float3 N    = normalize(lerp(geoN, rippleN, _RippleStrength));
                float3 V    = normalize(IN.viewDirWS);
                float  NdotV = saturate(dot(N, V));

                // Fresnel — more opaque at grazing angles (edges)
                float fresnel = pow(1.0 - NdotV, _FresnelPow);

                // Base alpha: tint alpha + fresnel boost
                float alpha = saturate(_WaterColor.a + fresnel * 0.35);

                // Specular highlight
                float3 lightDir = normalize(float3(-0.5, 0.9, 0.4));
                float3 H        = normalize(V + lightDir);
                float  NdotH    = saturate(dot(N, H));
                float  specExp  = exp2(_Smoothness * 10.0 + 2.0);
                float3 specCol  = _SpecColor2.rgb * pow(NdotH, specExp) * _SpecStrength;

                float3 col = _WaterColor.rgb + specCol;

                // Horizon fade — increase opacity and shift colour with distance
                float dist  = length(IN.positionWS.xz - _WorldSpaceCameraPos.xz);
                float fade  = saturate((dist - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
                col         = lerp(col, _HorizonColor.rgb, fade);
                alpha       = lerp(alpha, _HorizonColor.a, fade);

                return float4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
