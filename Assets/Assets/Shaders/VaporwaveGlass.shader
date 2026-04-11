Shader "Chaturanga/VaporwaveGlass"
{
    // Solid iridescent surface for floating vaporwave chess pieces.
    // Reflects the animated sky using Fresnel over a solid lit base.
    // Uses SRPDefaultUnlit so it renders on the BackgroundCamera's renderer.
    Properties
    {
        _BaseColor   ("Base Color",       Color)       = (0.55, 0.40, 0.85, 1)
        _Smoothness  ("Smoothness",       Range(0,1))  = 0.95
        _ReflBlend   ("Reflection Blend", Range(0,1))  = 0.75
        _FresnelPow  ("Fresnel Power",    Range(0.5,5))= 2.0
        [HDR] _EdgeGlow ("Edge Glow",     Color)       = (0.85, 0.25, 1.00, 1)
        _GlowPow     ("Glow Width",       Range(1,8))  = 3.5

        // Sky — sync with PurpleSky / MM_SkyBackground
        _SkyTop      ("Sky Top",          Color)       = (0.04, 0.00, 0.14, 1)
        _SkyMid      ("Sky Mid",          Color)       = (0.44, 0.04, 0.60, 1)
        _SkyBottom   ("Sky Bottom",       Color)       = (0.78, 0.06, 0.50, 1)
        _SkyHorizon  ("Sky Horizon",      Range(0,1))  = 0.618
        _CloudColor  ("Cloud Color",      Color)       = (0.60, 0.14, 0.82, 1)
        _CloudDark   ("Cloud Dark",       Color)       = (0.08, 0.00, 0.22, 1)
        _CloudSpeed  ("Cloud Speed",      Float)       = 0.5
        _CloudScale  ("Cloud Scale",      Float)       = 3.5
        _CloudDensity("Cloud Density",    Range(0,1))  = 0.486
        _CloudPow    ("Cloud Persp Power",Range(0.1,2))= 0.668

        // Legacy stubs
        _FloorDark   ("_", Color) = (0,0,0,1)
        _FloorLight  ("_", Color) = (1,1,1,1)
        _Tint        ("_", Color) = (1,1,1,1)
        _ReflStr     ("_", Range(0,1)) = 1
        _SkyHorizonFl("_", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry+2"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "VaporwaveGlass"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 posOS : POSITION; float3 normOS : NORMAL; };
            struct Varyings
            {
                float4 posCS  : SV_POSITION;
                float3 normWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _EdgeGlow;
                float  _Smoothness, _ReflBlend, _FresnelPow, _GlowPow;
                float4 _SkyTop, _SkyMid, _SkyBottom;
                float  _SkyHorizon;
                float4 _CloudColor, _CloudDark;
                float  _CloudSpeed, _CloudScale, _CloudDensity, _CloudPow;
                // Legacy
                float4 _FloorDark, _FloorLight, _Tint;
                float  _ReflStr, _SkyHorizonFl;
            CBUFFER_END

            float VNoise(float2 uv)
            {
                float2 i = floor(uv); float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = frac(sin(dot(i,               float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1,0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0,1), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1,1), float2(127.1, 311.7))) * 43758.5453);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 uv)
            {
                float v = 0.0, a = 0.5; uv += float2(1.7, 9.2);
                UNITY_UNROLL
                for (int k = 0; k < 5; ++k) { v += a * VNoise(uv); uv = uv * 2.0 + float2(3.1, 1.7); a *= 0.5; }
                return v;
            }

            float3 SampleSky(float3 dir)
            {
                float v = saturate(dir.y * 0.5 + 0.5);
                float u = saturate(dir.x * 0.5 + 0.5);
                float3 sky;
                if (v < _SkyHorizon)
                    sky = lerp(_SkyBottom.rgb, _SkyMid.rgb, v / max(_SkyHorizon, 0.001));
                else
                    sky = lerp(_SkyMid.rgb, _SkyTop.rgb,
                               saturate((v - _SkyHorizon) / max(1.0 - _SkyHorizon, 0.001)));
                if (v <= _SkyHorizon) return sky;
                float pt     = pow(max(v - _SkyHorizon, 0.001) / max(1.0 - _SkyHorizon, 0.001), _CloudPow);
                float worldX = (u - 0.5) / max(pt, 0.008);
                float t0     = _Time.y * _CloudSpeed;
                float n      = Fbm(float2(worldX, pt + t0) * _CloudScale)
                             + Fbm(float2(worldX * 1.3 + 1.7, (pt + t0) * 1.4 + 2.5) * (_CloudScale * 0.6)) * 0.55;
                float th     = 1.0 - _CloudDensity;
                float cloud  = smoothstep(th, th + 0.4, n)
                             * smoothstep(_SkyHorizon, _SkyHorizon + 0.07, v)
                             * smoothstep(1.0, 0.78, v);
                return saturate(lerp(sky, lerp(_CloudDark.rgb, _CloudColor.rgb, saturate(cloud * 0.65 + 0.2)), cloud));
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs vp = GetVertexPositionInputs(i.posOS.xyz);
                VertexNormalInputs   vn = GetVertexNormalInputs(i.normOS);
                o.posCS  = vp.positionCS;
                o.normWS = vn.normalWS;
                o.viewWS = GetWorldSpaceViewDir(vp.positionWS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 N     = normalize(i.normWS);
                float3 V     = normalize(i.viewWS);
                float  NdotV = saturate(dot(N, V));

                // Fake directional light matching Scene3DLight direction
                float3 lightDir = normalize(float3(-0.6, 0.8, 0.5));
                float3 lightCol = float3(0.90, 0.75, 1.00);
                float  NdotL    = saturate(dot(N, lightDir));
                float3 ambient  = lerp(_SkyBottom.rgb * 0.25, _SkyTop.rgb * 0.45,
                                       saturate(N.y * 0.5 + 0.5));
                float3 litBase  = _BaseColor.rgb * (lightCol * NdotL + ambient);

                // Specular
                float3 H      = normalize(V + lightDir);
                float  spec   = pow(saturate(dot(N, H)), exp2(_Smoothness * 9.0 + 2.0)) * _Smoothness;
                float3 specCol= lerp(lightCol, _SkyMid.rgb, 0.5) * spec * 3.5;

                // Sky reflection via Fresnel
                float  fresnel  = pow(1.0 - NdotV, _FresnelPow);
                float3 reflCol  = SampleSky(normalize(reflect(-V, N))) * fresnel * _ReflBlend;

                // Edge glow
                float  rim    = pow(1.0 - NdotV, _GlowPow);
                float3 rimCol = _EdgeGlow.rgb * rim * 0.65;

                return float4(litBase + reflCol + specCol + rimCol, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
