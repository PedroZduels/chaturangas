Shader "Chaturanga/CheckerWithWater"
{
    // Checkerboard tiles visible through shallow, highly-reflective animated water.
    // Single opaque SRPDefaultUnlit pass — compatible with the BackgroundCamera 2D renderer.
    Properties
    {
        // ── Tiles ──────────────────────────────────────────────────────────────
        _LightColor     ("Light Square",          Color)  = (0.72, 0.72, 0.90, 1)
        _DarkColor      ("Dark Square",           Color)  = (0.03, 0.02, 0.10, 1)
        _TileSize       ("Tile Size",             Float)  = 0.7

        // ── Water tint ─────────────────────────────────────────────────────────
        _WaterColor     ("Water Tint",            Color)  = (0.08, 0.25, 0.50, 1)
        _WaterAlpha     ("Water Opacity",         Range(0,1)) = 0.38

        // ── Wave layers (4 independent sine waves summed) ──────────────────────
        _WaveAmp        ("Wave Amplitude",        Float)  = 1.8
        _WaveSpeed      ("Wave Speed",            Float)  = 0.65
        _WaveScale      ("Large Wave Scale",      Float)  = 2.2
        _WaveScale2     ("Detail Wave Scale",     Float)  = 6.5

        // ── Ripple FBM for fine surface detail ────────────────────────────────
        _RippleScale    ("Ripple FBM Scale",      Float)  = 6.0
        _RippleSpeed    ("Ripple Speed",          Float)  = 0.38
        _RippleStrength ("Ripple Strength",       Range(0,1)) = 0.80

        // ── Specular / reflection ──────────────────────────────────────────────
        _Smoothness     ("Smoothness",            Range(0,1)) = 0.97
        _SpecStrength   ("Specular Strength",     Float)  = 4.5
        _ReflStrength   ("Fake Reflection Blend", Range(0,1)) = 0.65

        // ── Fake sky reflection gradient ───────────────────────────────────────
        _SkyHigh        ("Sky High",  Color)  = (0.20, 0.05, 0.55, 1)
        _SkyLow         ("Sky Low",   Color)  = (0.75, 0.08, 0.45, 1)

        // ── Light sources baked into the shader ────────────────────────────────
        _LightDir       ("Light Direction",       Vector) = (-0.42, 0.75, 0.51, 0)
        _LightColor0    ("Light Color 0",         Color)  = (0.95, 0.82, 1.00, 1)
        _LightDir1      ("Fill Light Direction",  Vector) = (0.7, 0.4, -0.3, 0)
        _LightColor1    ("Fill Light Color",      Color)  = (0.30, 0.55, 0.85, 1)
        _FillStrength   ("Fill Strength",         Float)  = 0.35

        // ── Caustic shimmer ────────────────────────────────────────────────────
        _CausticScale   ("Caustic Scale",         Float)  = 12.0
        _CausticSpeed   ("Caustic Speed",         Float)  = 0.18
        _CausticStrength("Caustic Strength",      Float)  = 0.22

        // ── Horizon fade ───────────────────────────────────────────────────────
        _FadeStart      ("Fade Start",            Float)  = 20.0
        _FadeEnd        ("Fade End",              Float)  = 70.0
        _HorizonColor   ("Horizon Color",         Color)  = (0.14, 0.08, 0.28, 1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "CheckerWithWater"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 viewDirWS  : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _LightColor, _DarkColor, _WaterColor, _HorizonColor;
                float4 _SkyHigh, _SkyLow;
                float4 _LightDir, _LightColor0, _LightDir1, _LightColor1;
                float  _TileSize, _WaterAlpha;
                float  _WaveAmp, _WaveSpeed, _WaveScale, _WaveScale2;
                float  _RippleScale, _RippleSpeed, _RippleStrength;
                float  _Smoothness, _SpecStrength, _ReflStrength, _FillStrength;
                float  _CausticScale, _CausticSpeed, _CausticStrength;
                float  _FadeStart, _FadeEnd;
            CBUFFER_END

            // ── Noise ───────────────────────────────────────────────────────────

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float SmoothedNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash(i),               Hash(i + float2(1,0)), u.x),
                    lerp(Hash(i + float2(0,1)), Hash(i + float2(1,1)), u.x), u.y);
            }

            float Fbm(float2 uv)
            {
                float v = 0.0, a = 0.5;
                float2 shift = float2(100.0, 100.0);
                for (int k = 0; k < 5; ++k)
                {
                    v  += a * SmoothedNoise(uv);
                    uv  = uv * 2.13 + shift;
                    a  *= 0.48;
                }
                return v;
            }

            // ── Gerstner-style wave normal sum ──────────────────────────────────
            float3 WaveNormal(float2 uv, float t)
            {
                float3 N = float3(0, 1, 0);

                float2 dirs[4]  = { float2(1.0,  0.6), float2(-0.7, 1.0),
                                    float2(0.4, -0.9),  float2(-1.0, -0.4) };
                float  freqs[4] = { _WaveScale, _WaveScale * 1.7,
                                    _WaveScale2, _WaveScale2 * 1.4 };
                float  speeds[4]= { _WaveSpeed, _WaveSpeed * 1.3,
                                    _WaveSpeed * 0.8, _WaveSpeed * 1.6 };
                float  amps[4]  = { _WaveAmp, _WaveAmp*0.7, _WaveAmp*0.5, _WaveAmp*0.35 };

                for (int i = 0; i < 4; ++i)
                {
                    float2 d  = normalize(dirs[i]);
                    float  ph = dot(d, uv) * freqs[i] + t * speeds[i];
                    float  c  = cos(ph) * amps[i];
                    N.x -= d.x * c * freqs[i];
                    N.z -= d.y * c * freqs[i];
                }
                return normalize(N);
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
                float  t  = _Time.y;
                float2 uv = IN.positionWS.xz;

                // ── Checkerboard ─────────────────────────────────────────────────
                float2 tile    = floor(uv / _TileSize);
                float  checker = fmod(abs(tile.x + tile.y), 2.0);
                float3 tileCol = lerp(_DarkColor.rgb, _LightColor.rgb, step(0.5, checker));

                // ── Combined wave normal ──────────────────────────────────────────
                float3 gerstnerN = WaveNormal(uv, t);

                float2 uv1 = uv * _RippleScale + float2( t * _RippleSpeed, t * _RippleSpeed * 0.71);
                float2 uv2 = uv * _RippleScale * 0.63 - float2(t * _RippleSpeed * 0.88, t * _RippleSpeed * 0.53);
                float  h1  = Fbm(uv1), h2 = Fbm(uv2);
                float  eps = 0.05;
                float3 fbmN = normalize(float3(
                    (Fbm(uv1 + float2(eps,0)) - h1) + (Fbm(uv2 + float2(eps,0)) - h2),
                    2.0 / max(_RippleStrength, 0.01),
                    (Fbm(uv1 + float2(0,eps)) - h1) + (Fbm(uv2 + float2(0,eps)) - h2)
                ));

                float3 geoN = normalize(IN.normalWS);
                float3 N    = normalize(gerstnerN * 0.5 + fbmN * _RippleStrength);
                N = normalize(lerp(geoN, N, _RippleStrength));

                float3 V     = normalize(IN.viewDirWS);
                float  NdotV = saturate(dot(N, V));

                // ── Fake sky reflection ───────────────────────────────────────────
                float3 reflDir  = reflect(-V, N);
                float  skyT     = saturate(reflDir.y * 0.5 + 0.5);
                float3 skyColor = lerp(_SkyLow.rgb, _SkyHigh.rgb, skyT);
                float  fresnel  = pow(1.0 - NdotV, 1.5);
                float3 reflCol  = skyColor * fresnel * _ReflStrength;

                // ── Multi-light specular ──────────────────────────────────────────
                float3 L0    = normalize(_LightDir.xyz);
                float3 H0    = normalize(V + L0);
                float  spec0 = pow(saturate(dot(N, H0)), exp2(_Smoothness * 12.0 + 3.0));
                float3 specA = _LightColor0.rgb * spec0 * _SpecStrength;

                float3 L1    = normalize(_LightDir1.xyz);
                float3 H1    = normalize(V + L1);
                float  spec1 = pow(saturate(dot(N, H1)), exp2(_Smoothness * 8.0 + 2.0));
                float3 specB = _LightColor1.rgb * spec1 * _SpecStrength * _FillStrength;

                float3 specTotal = specA + specB;

                // ── Caustics on tiles ─────────────────────────────────────────────
                float2 cu1  = uv * _CausticScale + float2(t * _CausticSpeed,  t * _CausticSpeed * 0.7);
                float2 cu2  = uv * _CausticScale * 0.8 - float2(t * _CausticSpeed * 0.9, t * _CausticSpeed * 1.1);
                float  caus = saturate(SmoothedNoise(cu1) * SmoothedNoise(cu2) * 3.5 - 0.8);
                float3 causticCol = caus * _CausticStrength * _LightColor0.rgb;

                // ── Blend water over tiles ────────────────────────────────────────
                float waterBlend = saturate(_WaterAlpha + fresnel * 0.40);
                float3 litTile   = tileCol + causticCol;
                float3 waterSurf = _WaterColor.rgb + reflCol + specTotal;
                float3 col       = lerp(litTile, waterSurf, waterBlend);

                // ── Horizon fade ──────────────────────────────────────────────────
                float dist = length(uv - _WorldSpaceCameraPos.xz);
                float fade = saturate((dist - _FadeStart) / max(_FadeEnd - _FadeStart, 0.001));
                col = lerp(col, _HorizonColor.rgb, fade);

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
