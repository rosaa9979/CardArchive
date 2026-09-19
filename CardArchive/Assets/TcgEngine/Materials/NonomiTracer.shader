Shader "CardArchive/FX/Nonomi Tracer"
{
    Properties { _Radial ("Radial flash", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent+30" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        ZTest LEqual
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            float _Radial;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float width = lerp(0.12, 0.9, smoothstep(0, 0.75, i.uv.y));
                float side = saturate(1 - abs(p.x) / width);
                float tip = 1 - smoothstep(0.78, 1, i.uv.y);
                float streak = side * side * smoothstep(0, 0.72, i.uv.y) * tip;
                float radial = pow(saturate(1 - length(p)), 2);
                float mask = lerp(streak, radial, _Radial);
                fixed3 whiteCore = lerp(i.color.rgb, fixed3(1, 0.99, 0.85), saturate(side * side * 0.65));
                return fixed4(whiteCore, mask * i.color.a);
            }
            ENDHLSL
        }
    }
}
