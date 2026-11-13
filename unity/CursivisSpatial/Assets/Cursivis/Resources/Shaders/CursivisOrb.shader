// Cursivis orb: original graphite-glass orb (OrbOverlayWindow.xaml) rendered analytically on one quad.
//  - core radial gradient with focal point (0.30, 0.25), stops 0 / 0.58 / 1, 1px rim (#60FFFFFF dark)
//  - inner disc at 0.898 D with its own gradient + hairline stroke
//  - soft drop shadow (blur 12 equivalent) without a blur pass
//  - spatial feedback: rotating cyan->magenta "thinking" arc (brand icon colors), voice-level ring, error tint
Shader "Cursivis/UI/Orb"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Core0 ("Core 0", Color) = (0.086,0.102,0.122,0.86)
        _Core1 ("Core 1", Color) = (0.059,0.075,0.09,0.8)
        _Core2 ("Core 2", Color) = (0.035,0.051,0.067,0.74)
        _Inner0 ("Inner 0", Color) = (0.078,0.114,0.157,0.96)
        _Inner1 ("Inner 1", Color) = (0.055,0.082,0.122,0.93)
        _Inner2 ("Inner 2", Color) = (0.035,0.055,0.086,0.9)
        _Rim ("Rim", Color) = (1,1,1,0.376)
        _InnerStroke ("Inner Stroke", Color) = (1,1,1,0.133)
        _Shadow ("Shadow", Color) = (0.03,0.075,0.12,0.18)
        _ArcA ("Arc A", Color) = (0.286,0.89,1,1)
        _ArcB ("Arc B", Color) = (1,0.357,0.839,1)
        _Arc ("Arc strength", Range(0,1)) = 0
        _Spin ("Arc angle (rad)", Float) = 0
        _Level ("Voice level", Range(0,1)) = 0
        _Listen ("Listening ring", Range(0,1)) = 0
        _ErrorTint ("Error tint", Range(0,1)) = 0
        _Glow ("Selection glow", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            float4 _Core0, _Core1, _Core2, _Inner0, _Inner1, _Inner2, _Rim, _InnerStroke, _Shadow, _ArcA, _ArcB;
            float _Arc, _Spin, _Level, _Listen, _ErrorTint, _Glow;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 ramp3(float4 a, float4 b, float4 c, float t)
            {
                return t < 0.58 ? lerp(a, b, t / 0.58) : lerp(b, c, (t - 0.58) / 0.42);
            }

            float4 over(float4 dst, float4 src)
            {
                float a = src.a + dst.a * (1.0 - src.a);
                float3 rgb = (src.rgb * src.a + dst.rgb * dst.a * (1.0 - src.a)) / max(a, 1e-5);
                return float4(rgb, a);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // quad covers 1.6 x core diameter; core radius = 0.3125 in uv units
                float2 uv = i.uv;
                float2 p = uv - 0.5;
                float r = length(p);
                const float R = 0.3125;
                float aa = max(fwidth(r), 1e-4);
                float4 col = float4(0, 0, 0, 0);

                // shadow / glow below the orb
                float sh = 1.0 - smoothstep(R * 0.92, R * 1.45, r);
                col = over(col, float4(_Shadow.rgb, _Shadow.a * sh * sh));
                // selection glow (brand cyan) when acknowledging a selection
                float g = (1.0 - smoothstep(R, R * 1.55, r)) * _Glow;
                col = over(col, float4(_ArcA.rgb, 0.35 * g * g));

                // listening ring that breathes with the microphone level
                float ringR = R * (1.12 + 0.18 * _Level);
                float ring = (1.0 - smoothstep(0.0, R * 0.05 + aa, abs(r - ringR))) * _Listen;
                col = over(col, float4(_ArcA.rgb, ring * (0.45 + 0.5 * _Level)));

                // core disc
                float2 cuv = (p / (2.0 * R)) + 0.5;                 // 0..1 across the core
                // WPF GradientOrigin (0.30, 0.25) is y-down; UV space is y-up, so the focal point is (0.30, 0.75).
                float tCore = saturate(length(cuv - float2(0.30, 0.75)) / 0.70);
                float4 core = ramp3(_Core0, _Core1, _Core2, tCore);
                float coreMask = saturate((R - r) / aa + 0.5);
                core.a *= coreMask;
                col = over(col, core);

                // inner disc (0.898 D)
                float Ri = R * 0.898;
                float tInner = saturate(length(cuv - float2(0.30, 0.75)) / 0.70);
                float4 inner = ramp3(_Inner0, _Inner1, _Inner2, tInner);
                float innerMask = saturate((Ri - r) / aa + 0.5);
                inner.a *= innerMask;
                col = over(col, inner);
                float innerStroke = saturate(1.0 - abs(r - Ri) / (aa * 1.2));
                col = over(col, float4(_InnerStroke.rgb, _InnerStroke.a * innerStroke));

                // rim (1px border) with a lighter top-left sheen
                float rim = saturate(1.0 - abs(r - (R - aa)) / (aa * 1.4));
                float sheen = 0.75 + 0.5 * saturate(p.y * 3.0 + 0.5); // brighter upper rim
                col = over(col, float4(_Rim.rgb, saturate(_Rim.a * sheen) * rim));

                // thinking arc: rotating gradient segment just outside the rim
                if (_Arc > 0.001)
                {
                    float ang = atan2(p.y, p.x) + _Spin;
                    float u = frac(ang / 6.2831853);
                    float seg = smoothstep(0.0, 0.08, u) * (1.0 - smoothstep(0.30, 0.42, u));
                    float band = 1.0 - smoothstep(0.0, R * 0.035 + aa, abs(r - R * 1.07));
                    float4 arcCol = lerp(_ArcA, _ArcB, saturate(u / 0.42));
                    col = over(col, float4(arcCol.rgb, band * seg * _Arc * 0.9));
                }

                // error tint (soft warm wash over the core)
                col.rgb = lerp(col.rgb, float3(1.0, 0.45, 0.45), _ErrorTint * coreMask * 0.35);

                col *= float4(1, 1, 1, i.color.a);
                float n = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
                col.rgb += n / 255.0;
                return col;
            }
            ENDCG
        }
    }
}
