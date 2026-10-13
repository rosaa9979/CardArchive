Shader "CardArchive/FX/ScreenDim"
{
 Properties { _Opacity ("Opacity", Range(0,1)) = 0 }
 SubShader
 {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
  Cull Off ZWrite Off ZTest Always
  Blend SrcAlpha OneMinusSrcAlpha
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   float _Opacity;
   struct Input { float4 vertex : POSITION; };
   struct Output { float4 vertex : SV_POSITION; };
   Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); return o; }
   fixed4 frag(Output i) : SV_Target { return fixed4(0,0,0,_Opacity); }
   ENDCG
  }
 }
}
