Shader "Chaturanga/ReflectivePiece"
{
    // Solid colour surface lit by the scene directional light.
    // Sky/cloud reflection layers on top via Fresnel — base colour always visible.
    Properties
    {
        // ── Base surface ──────────────────────────────────────────────────────
        _BaseColor         ("Base Color",          Color)       = (0.8, 0.8, 0.9, 1)
        _Smoothness        ("Smoothness",          Range(0,1))  = 0.85
        _Metallic          ("Metallic",            Range(0,1))  = 0.0
        // ── Reflection blend ──────────────────────────────────────────────────
        _ReflBlend         ("Reflection Strength", Range(0,1))  = 0.7
        _FresnelPow        ("Fresnel Power",       Range(0.5,6))= 2.5
        // ── Fake AO / cavity ──────────────────────────────────────────────────
        _OcclusionStrength ("Occlusion",           Range(0,1))  = 0.45
        _OcclusionColor    ("Occlusion Color",     Color)       = (0.01, 0.0, 0.03, 1)
        // ── Sky params — sync with MM_SkyBackground.mat ───────────────────────
        _SkyTop            ("Sky Top",             Color)       = (0.04, 0.00, 0.14, 1)
        _SkyMid            ("Sky Mid",             Color)       = (0.44, 0.04, 0.60, 1)
        _SkyBottom         ("Sky Bottom",          Color)       = (0.78, 0.06, 0.50, 1)
        _SkyHorizon        ("Sky Horizon",         Range(0,1))  = 0.618
        // ── Cloud FBM — sync with MM_SkyBackground.mat ────────────────────────
        _CloudColor        ("Cloud Color",         Color)       = (0.60, 0.14, 0.82, 1)
        _CloudDark         ("Cloud Dark",          Color)       = (0.08, 0.00, 0.22, 1)
        _CloudSpeed        ("Cloud Speed",         Float)       = 0.5
        _CloudScale        ("Cloud Scale",         Float)       = 3.5
        _CloudDensity      ("Cloud Density",       Range(0,1))  = 0.486
        _CloudPerspPow     ("Cloud Persp Power",   Range(0.1,2))= 0.668
        // ── Rim glow ──────────────────────────────────────────────────────────
        [HDR] _RimColor    ("Rim Glow",            Color)       = (0.5, 0.1, 0.8, 1)
        _RimPow            ("Rim Width",           Range(1,8))  = 4.0
        // Legacy stubs — keeps old .mat data from throwing warnings
        _Tint              ("_", Color)      = (1,1,1,1)
        _TintBlend         ("_", Range(0,1)) = 0
        _Sharpness         ("_", Range(0,1)) = 1
        _EmissionColor     ("_", Color)      = (0,0,0,1)
        _AOColor           ("_", Color)      = (0,0,0,1)
        _AOStrength        ("_", Range(0,1)) = 0
        _CloudHorizon      ("_", Range(0,1)) = 0
        _ReflStrength      ("_", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ReflectivePiece"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float  _Smoothness, _Metallic;
                float  _ReflBlend, _FresnelPow;
                float  _OcclusionStrength;
                float4 _OcclusionColor;
                float4 _SkyTop, _SkyMid, _SkyBottom;
                float  _SkyHorizon;
                float4 _CloudColor, _CloudDark;
                float  _CloudSpeed, _CloudScale, _CloudDensity, _CloudPerspPow;
                float4 _RimColor;
                float  _RimPow;
                // Legacy
                float4 _Tint, _EmissionColor, _AOColor;
                float  _TintBlend, _Sharpness, _AOStrength, _CloudHorizon, _ReflStrength;
            CBUFFER_END

            float VNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = frac(sin(dot(i,               float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1,0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0,1), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1,1), float2(127.1, 311.7))) * 43758.5453);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float Fbm(float2 uv)
            {
                float v = 0.0, a = 0.5;
                uv += float2(1.7, 9.2);
                UNITY_UNROLL
                for (int k = 0; k < 5; ++k)
                {
                    v  += a * VNoise(uv);
                    uv  = uv * 2.0 + float2(3.1, 1.7);
                    a  *= 0.5;
                }
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

                float normFrac = max(v - _SkyHorizon, 0.001) / max(1.0 - _SkyHorizon, 0.001);
                float pt       = pow(normFrac, _CloudPerspPow);
                float worldX   = (u - 0.5) / max(pt, 0.008);
                float t0       = _Time.y * _CloudSpeed;

                float n = Fbm(float2(worldX, pt + t0) * _CloudScale)
                        + Fbm(float2(worldX * 1.3 + 1.7, (pt + t0) * 1.4 + 2.5)
                              * (_CloudScale * 0.6)) * 0.55;

                float th    = 1.0 - _CloudDensity;
                float cloud = smoothstep(th, th + 0.4, n)
                            * smoothstep(_SkyHorizon, _SkyHorizon + 0.07, v)
                            * smoothstep(1.0, 0.78, v);

                return saturate(lerp(sky,
                    lerp(_CloudDark.rgb, _CloudColor.rgb, saturate(cloud * 0.65 + 0.2)),
                    cloud));
            }

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                VertexPositionInputs vp = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   vn = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = vp.positionCS;
                OUT.normalWS   = vn.normalWS;
                OUT.viewDirWS  = GetWorldSpaceViewDir(vp.positionWS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float3 N     = normalize(IN.normalWS);
                float3 V     = normalize(IN.viewDirWS);
                float  NdotV = saturate(dot(N, V));

                // ── Ambient sky light from hemisphere ─────────────────────────
                // Upper hemisphere = sky top tint, lower = sky bottom tint
                float  upness  = saturate(N.y * 0.5 + 0.5);
                float3 ambient = lerp(_SkyBottom.rgb * 0.30, _SkyTop.rgb * 0.50, upness);

                // ── Fake directional light (baked direction, no shadow cost) ──
                // Light comes from upper-right-front to match Scene3DLight transform
                float3 lightDir = normalize(float3(-0.6, 0.8, 0.5));
                float3 lightCol = float3(0.90, 0.75, 1.00);
                float  NdotL    = saturate(dot(N, lightDir));
                float3 diffuse  = lightCol * NdotL;

                // ── Lit base colour ───────────────────────────────────────────
                float3 litBase = _BaseColor.rgb * (diffuse + ambient);

                // ── Cavity AO — darken crevices (low NdotL faces) ────────────
                float  cavity  = saturate(NdotL + NdotV * 0.25);
                litBase        = lerp(lerp(_OcclusionColor.rgb, litBase, cavity),
                                      litBase, 1.0 - _OcclusionStrength);

                // ── Specular highlight ────────────────────────────────────────
                float3 H       = normalize(V + lightDir);
                float  NdotH   = saturate(dot(N, H));
                float  specExp = exp2(_Smoothness * 9.0 + 2.0);
                float  spec    = pow(NdotH, specExp) * _Smoothness;
                float3 specCol = lerp(lightCol, _SkyMid.rgb, 0.5) * spec * 3.0;

                // ── Sky reflection via Fresnel ─────────────────────────────────
                float  fresnel  = pow(1.0 - NdotV, _FresnelPow);
                float3 envColor = SampleSky(normalize(reflect(-V, N)));
                float3 reflCol  = envColor * fresnel * _ReflBlend;

                // ── Rim glow ──────────────────────────────────────────────────
                float  rim    = pow(1.0 - NdotV, _RimPow);
                float3 rimCol = _RimColor.rgb * rim * 0.55;

                return float4(litBase + reflCol + specCol + rimCol, 1.0);
            }
            ENDHLSL
        }

    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
