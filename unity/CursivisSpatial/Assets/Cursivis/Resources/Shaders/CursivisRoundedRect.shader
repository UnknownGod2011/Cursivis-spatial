// SDF rounded rectangle for Cursivis Spatial world-space UI.
// Vertex data (from RoundedRect.cs):
//   color = top fill, uv0 = (localX, localY, halfW, halfH), uv1 = (radius, border, shadowSize, shadowAlpha)
//   uv2 = bottom fill RGBA, uv3 = border RGBA, tangent.x = rim highlight
// Alpha channel is blended with (One, OneMinusSrcAlpha) so translucent panels composite correctly over passthrough.
Shader "Cursivis/UI/RoundedRect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
                float4 tangent : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 colTop : COLOR;
                float4 local : TEXCOORD0;
                float4 shape : TEXCOORD1;
                float4 colBottom : TEXCOORD2;
                float4 colBorder : TEXCOORD3;
                float4 worldPosition : TEXCOORD4;
                float rim : TEXCOORD5;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.colTop = v.color;
                o.local = v.uv0;
                o.shape = v.uv1;
                o.colBottom = v.uv2;
                o.colBorder = v.uv3;
                o.rim = v.tangent.x;
                return o;
            }

            float sdRoundRect(float2 p, float2 halfSize, float r)
            {
                float2 q = abs(p) - (halfSize - r);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.local.xy;
                float2 halfSize = i.local.zw;
                float radius = i.shape.x;
                float borderW = i.shape.y;
                float shadowSize = i.shape.z;
                float shadowAlpha = i.shape.w;

                float d = sdRoundRect(p, halfSize, radius);
                float aa = max(fwidth(d), 1e-4) * 0.75;
                float inside = saturate(0.5 - d / aa);

                // vertical gradient, top -> bottom
                float t = saturate(0.5 - 0.5 * (p.y / max(halfSize.y, 1e-3)));
                float4 fill = lerp(i.colTop, i.colBottom, t);

                // border band with a lighter top rim (glass sheen)
                float innerEdge = saturate(0.5 - (d + borderW) / aa);
                float borderMask = saturate(inside - innerEdge);
                float rimBoost = lerp(1.0 + i.rim, 1.0 - 0.5 * i.rim, t);
                float4 border = i.colBorder;
                border.a = saturate(border.a * rimBoost);

                float4 c = fill;
                c.rgb = lerp(c.rgb, border.rgb, borderMask * border.a / max(border.a + c.a * (1 - borderMask), 1e-4));
                c.a = max(fill.a * inside, border.a * borderMask);

                // soft outer shadow (no blur pass)
                if (shadowSize > 0.0 && shadowAlpha > 0.0)
                {
                    float s = 1.0 - smoothstep(0.0, shadowSize, max(d, 0.0));
                    float sa = s * s * shadowAlpha * (1.0 - inside);
                    c.rgb = lerp(float3(0, 0, 0), c.rgb, saturate(c.a / max(c.a + sa, 1e-4)));
                    c.a = saturate(c.a + sa);
                }

                // subtle dither against banding in dark gradients
                float n = frac(sin(dot(i.pos.xy, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
                c.rgb += n / 255.0;

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
