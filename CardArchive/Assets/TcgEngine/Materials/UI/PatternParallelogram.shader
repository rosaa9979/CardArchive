Shader "TcgEngine/UI/PatternParallelogram"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FillColor ("Pale Background", Color) = (0.97,0.98,0.97,1)
        _EdgeColor ("Fine Edge", Color) = (0.72,0.79,0.81,1)
        _PatternTex ("Field Triangle Pattern", 2D) = "white" {}
        _PatternStrength ("Pattern Opacity", Range(0,1)) = 0.32
        _Skew ("Side Lean", Range(0,0.3)) = 0.12
        _EdgeWidth ("Edge Width", Range(0,0.02)) = 0.002
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _PatternTex;
            float4 _Color, _FillColor, _EdgeColor, _ClipRect;
            float _PatternStrength, _Skew, _EdgeWidth;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                // Analytic edges avoid stretching or clipping a raster shadow.
                float2 uv=i.uv;
                float x=uv.x-_Skew*uv.y;
                float d=min(min(x,1-_Skew-x),min(uv.y,1-uv.y));
                float aa=max(fwidth(d),0.0001);
                float coverage=smoothstep(0,aa,d);
                float edge=1-smoothstep(_EdgeWidth,_EdgeWidth+aa,d);
                // Reuse the field's upright/inverted triangles, softly confined to opposing corners.
                float corner=max(saturate(1-length((uv-float2(.18,.9))/float2(.62,.7))),saturate(1-length((uv-float2(.82,.1))/float2(.62,.7))));
                float3 pattern=tex2D(_PatternTex,float2(uv.x*.72,.02+uv.y*.44)).rgb;
                float3 fill=lerp(_FillColor.rgb,pattern,corner*_PatternStrength);
                float4 col=float4(lerp(fill,_EdgeColor.rgb,edge),coverage*_FillColor.a)*i.color;
                #ifdef UNITY_UI_CLIP_RECT
                col.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a-.001);
                #endif
                return col;
            }
            ENDCG
        }
    }
}