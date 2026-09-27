Shader "TcgEngine/RangeHatching"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.98, 0.98, 0.96, 1)
        _OutlineColor ("Outline", Color) = (0.10, 0.16, 0.25, 1)
        _ShapeSize ("Sprite local dimensions", Vector) = (9.25, 10.92, 0, 0)
        _FlowSpeed ("Flow speed", Range(0, 0.1)) = 0.018
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True" }
        Cull Off
        ZWrite Off
        Blend One Zero
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _InkColor;
                half4 _OutlineColor;
                float4 _ShapeSize;
                float _FlowSpeed;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 local : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.local = input.positionOS.xy / max(_ShapeSize.xy, float2(0.001, 0.001));
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.local;
                // Pointy-top hexagon. No filled background or perimeter line.
                clip(0.5 - abs(p.x));
                clip(0.5 - abs(p.y) - 0.5 * abs(p.x));
                float2 diagonal = float2(p.x + p.y, p.x - p.y) * 0.70710678;
                diagonal.x += _Time.y * _FlowSpeed;
                float2 pitch = float2(0.26, 0.20);
                float2 cell = (frac(diagonal / pitch + 0.5) - 0.5) * pitch;
                float distanceToDash = length(float2(max(abs(cell.x) - 0.055, 0), cell.y));
                clip(0.018 - distanceToDash);
                // Opaque ink and outline: the tile never tints the drawn strokes.
                half3 color = distanceToDash < 0.009 ? _InkColor.rgb : _OutlineColor.rgb;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
