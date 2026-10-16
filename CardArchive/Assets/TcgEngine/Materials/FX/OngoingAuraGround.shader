Shader "TcgEngine/OngoingAuraGround"
{
 Properties
 {
  [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
  _Color ("Tint", Color) = (1,1,1,1)
  _GroundOpacity ("Gradient trail opacity", Range(0,1)) = 0.18
  _RingOpacity ("Ring opacity", Range(0,1)) = 0.85
  _RingPhase ("Ring phase", Range(0,1)) = 0
  _RingWidth ("Ring half width", Range(0.005,0.1)) = 0.014
  [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
  [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
  Cull Off
  Lighting Off
  ZWrite Off
  Blend One OneMinusSrcAlpha
  Pass
  {
   Tags { "LightMode"="SRPDefaultUnlit" }
   CGPROGRAM
   #pragma vertex SpriteVert
   #pragma fragment GroundFrag
   #pragma multi_compile_instancing
   #include "UnitySprites.cginc"
   float _GroundOpacity;
   float _RingOpacity;
   float _RingPhase;
   float _RingWidth;
   fixed4 GroundFrag(v2f input) : SV_Target
   {
    float radius=length(input.texcoord*2.0-1.0);
    float footprint=1.0-smoothstep(0.74,0.92,radius);
    float ground=0.0;
    // Animator owns the phase; there is no independent clock.
    float ring=0.0;
    float aa=max(fwidth(radius),0.002);
    for(int i=0;i<2;i++)
    {
     float phase=frac(_RingPhase+i*0.5);
     float front=lerp(0.025,0.92,phase);
     float coverage=1.0-smoothstep(_RingWidth,_RingWidth+aa,abs(radius-front));
     float fade=smoothstep(0.0,0.1,phase)*(1.0-smoothstep(0.72,1.0,phase));
     ring+=coverage*fade*_RingOpacity;
     float inside=1.0-smoothstep(front,front+aa,radius);
     float gradient=pow(saturate(radius/max(front,0.025)),2.0);
     ground=max(ground,gradient*inside*fade*_GroundOpacity);
    }
    ring=saturate(ring)*footprint;
    ground*=footprint;
    fixed4 color=input.color;
    color.a*=ground+ring*(1.0-ground);
    color.rgb*=color.a;
    return color;
   }
   ENDCG
  }
 }
}
