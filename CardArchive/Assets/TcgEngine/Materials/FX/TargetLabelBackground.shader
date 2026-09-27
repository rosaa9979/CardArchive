Shader "TcgEngine/TargetLabelBackground"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.10, 0.16, 0.25, 1)
        _PanelSize ("Panel size", Vector) = (4, 1.4, 0, 0)
        _Feather ("Soft edge width", Float) = 0.4
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                float4 _PanelSize;
                float _Feather;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 local : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.local = input.positionOS.xy;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 inset = _PanelSize.xy * 0.5 - abs(input.local);
                float2 fade = smoothstep(0.0, max(_Feather, 0.001), inset);
                return half4(_InkColor.rgb, _InkColor.a * fade.x * fade.y);
            }
            ENDHLSL
        }
    }
}
