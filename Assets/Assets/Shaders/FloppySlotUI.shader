Shader "Chaturanga/UI/FloppySlotUI"
{
    // Rounded-corner quad with inner bevel for the floppy HUD slots.
    // Designed for UnityEngine.UI.Image (UI render queue, vertex color, stencil).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _FillColor      ("Fill Color",       Color) = (0.10, 0.14, 0.26, 1.0)
        _CornerRadius   ("Corner Radius",    Range(0, 0.5)) = 0.18
        _BevelWidth     ("Bevel Width",      Range(0, 0.3)) = 0.06
        _BevelBright    ("Bevel Bright",     Range(0, 2))   = 0.55
        _BevelDark      ("Bevel Dark",       Range(0, 2))   = 0.25
        _BorderWidth    ("Border Width",     Range(0, 0.1)) = 0.015
        _BorderColor    ("Border Color",     Color)         = (0.30, 0.40, 0.65, 1.0)

        // Required by Unity UI stencil system
        _StencilComp    ("Stencil Comparison", Float) = 8
        _Stencil        ("Stencil ID",         Float) = 0
        _StencilOp      ("Stencil Operation",  Float) = 0
        _StencilWriteMask("Stencil Write Mask",Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask      ("Color Mask",         Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Transparent"
            "IgnoreProjector"= "True"
            "RenderType"     = "Transparent"
            "PreviewType"    = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref   [_Stencil]
            Comp  [_StencilComp]
            Pass  [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "FloppySlotUI"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FillColor;
                float  _CornerRadius;
                float  _BevelWidth;
                float  _BevelBright;
                float  _BevelDark;
                float  _BorderWidth;
                float4 _BorderColor;
                float4 _ClipRect;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 posOS   : POSITION;
                float4 color   : COLOR;
                float2 uv      : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 posCS   : SV_POSITION;
                float4 color   : COLOR;
                float2 uv      : TEXCOORD0;
                float4 worldPos: TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.posCS    = TransformObjectToHClip(i.posOS.xyz);
                o.color    = i.color;
                o.uv       = i.uv;
                o.worldPos = i.posOS;
                return o;
            }

            // Signed-distance to a rounded rectangle centred at (0,0) with half-extents b
            // and corner radius r, in UV space [0,1].
            float RoundedBoxSDF(float2 uv, float r)
            {
                float2 p = uv - 0.5;            // centre at origin
                float2 q = abs(p) - (0.5 - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                // ── Outer shape SDF ─────────────────────────────────────────
                float r   = _CornerRadius;
                float d   = RoundedBoxSDF(uv, r);

                // Anti-alias the outer edge
                float aa  = fwidth(d) * 1.2;
                float outerMask = 1.0 - smoothstep(-aa, aa, d);

                // ── Border ──────────────────────────────────────────────────
                float bw        = _BorderWidth;
                float borderMask= smoothstep(-aa, aa, d + bw) * outerMask;

                // ── Inner bevel ─────────────────────────────────────────────
                // Bevel is computed from the inset shape SDF.
                float inset    = bw;
                float ri       = max(r - inset, 0.0);
                float2 uvInset = 0.5 + (uv - 0.5) * ((0.5 - inset) / 0.5);
                float di       = RoundedBoxSDF(uvInset, ri / ((0.5 - inset) / 0.5));

                float bevelRange = _BevelWidth;
                // Bevel is the inset band from distance [-bevelRange, 0] inside the fill.
                float bevelT  = saturate((-di) / max(bevelRange, 0.001));

                // Directional bevel: bright on top-left, dark on bottom-right.
                // We compute a directional gradient using the UV of the inset shape.
                float2 p      = uv - 0.5;
                float  dirGrad = dot(normalize(p + float2(-0.001, 0.001)), float2(-1, 1)) * 0.5 + 0.5;
                float  bright = lerp(_BevelDark, _BevelBright, dirGrad);

                // ── Compose ─────────────────────────────────────────────────
                float4 fillCol   = _FillColor;
                float4 bevelCol  = float4(fillCol.rgb * bright, fillCol.a);
                float4 borderCol = _BorderColor;

                // Start with fill, apply bevel in the rim, overlay border
                float4 col = lerp(fillCol, bevelCol, bevelT * outerMask);
                col = lerp(col, borderCol, borderMask);
                col.a *= outerMask;

                // Vertex tint (from Button's targetGraphic color / UI Color)
                col *= i.color;

                // Sample texture (used when a sprite is set on the Image)
                float4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                col *= texCol;

                // Unity UI clip rect
                #ifdef UNITY_UI_CLIP_RECT
                    float2 inside = step(float2(_ClipRect.x, _ClipRect.y), i.worldPos.xy)
                                  * step(i.worldPos.xy, float2(_ClipRect.z, _ClipRect.w));
                    col.a *= inside.x * inside.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDHLSL
        }
    }

    FallBack "UI/Default"
}
