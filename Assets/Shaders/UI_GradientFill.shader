Shader "UI/GradientFillVR"
{
    Properties
    {
        _ColorA ("Start Color", Color) = (0, 0.83, 1, 1)
        _ColorB ("End Color", Color) = (0, 1, 0.6, 1)
        _GlowStrength ("Glow Strength", Range(0, 1)) = 0.25
        _FlowSpeed ("Flow Speed", Range(0, 5)) = 0.5
        _Brightness ("Brightness", Range(0.5, 2)) = 1
        [HideInInspector] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _StencilComp ("Stencil Comparison", Float) = 8
        [PerRendererData] _Stencil ("Stencil ID", Float) = 0
        [PerRendererData] _StencilOp ("Stencil Operation", Float) = 0
        [PerRendererData] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [PerRendererData] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [PerRendererData] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off

        Pass
        {
            Name "UIGradientFill"
            Tags { "LightMode"="UniversalForward" }

            Stencil
            {
                Ref [_Stencil]
                Comp [_StencilComp]
                Pass [_StencilOp]
                ReadMask [_StencilReadMask]
                WriteMask [_StencilWriteMask]
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float4 _MainTex_ST;

            fixed4 _ColorA;
            fixed4 _ColorB;
            float _GlowStrength;
            float _FlowSpeed;
            float _Brightness;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Horizontal gradient
                float t = saturate(i.uv.x);

                // Base gradient between _ColorA and _ColorB
                fixed4 col = lerp(_ColorA, _ColorB, t);

                // Subtle moving glow/flux (optional)
                float flow = sin((_Time.y * _FlowSpeed) + (i.uv.x * 6.2831));
                col.rgb += flow * _GlowStrength;

                // Brightness control
                col.rgb *= _Brightness;

                // Alpha from main texture (for Image mask)
                fixed4 tex = tex2D(_MainTex, i.uv);
                col.a *= tex.a;

                return col;
            }
            ENDCG
        }
    }

    FallBack "UI/Default"
}
