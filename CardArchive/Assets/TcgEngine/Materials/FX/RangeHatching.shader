Shader "TcgEngine/RangeHatching"
{
    Properties
    {
        _InkColor ("Ink", Color) = (0.98, 0.98, 0.96, 1)
        _OutlineColor ("Outline", Color) = (0.10, 0.16, 0.25, 1)
        _ShapeSize ("Sprite local dimensions", Vector) = (9.25, 10.92, 0, 0)
        _Reveal ("Slide reveal", Range(0, 1)) = 1
        _StripeSpacing ("Stripe spacing", Float) = 0.25
        _StripeWidth ("Stripe width", Float) = 0.038
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
                float _Reveal;
                float _StripeSpacing;
                float _StripeWidth;
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
                // Two halves travel along the hatch direction from opposite sides.
                // Restrict each to its original half so repeating UVs cannot wrap
                // new strokes into view while the pattern is outside the hexagon.
                float side = diagonal.x < 0 ? -1 : 1;
                diagonal.x -= side * (1 - saturate(_Reveal)) * 0.65;
                clip(side * diagonal.x);
                // Continuous diagonal stripes: no repeating short dash segments.
                float pitch = max(_StripeSpacing, 0.01);
                float distanceToStripe = abs(frac(diagonal.y / pitch + 0.5) - 0.5) * pitch;
                float radius = max(_StripeWidth, 0.001) * 0.5;
                clip(radius - distanceToStripe);
                // Opaque ivory core with a thin navy edge stays legible on all teams.
                half3 color = distanceToStripe < radius * 0.65 ? _InkColor.rgb : _OutlineColor.rgb;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
