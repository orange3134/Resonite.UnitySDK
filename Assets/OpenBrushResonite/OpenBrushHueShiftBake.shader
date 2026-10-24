// Copyright 2020 The Tilt Brush Authors
// Copyright 2026 Open Brush Resonite converter contributors
// Apache-2.0. Normal TaperedHueShift shader / LoftedHueShift graph equations.
Shader "Hidden/OpenBrush/HueShiftBake" {
 Properties { _MainTex("Source pattern",2D)="white"{} }
 SubShader { Pass {
  Cull Off ZWrite Off ZTest Always Blend Off
  CGPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #pragma target 3.0
  #include "UnityCG.cginc"
  sampler2D _MainTex;float _Lofted;
  struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
  struct Varying {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
  Varying vert(Input v) {
   Varying o;o.pos=float4(v.vertex.xy*2-1,0,1);
   #if UNITY_UV_STARTS_AT_TOP
    o.pos.y=-o.pos.y;
   #endif
   o.uv=v.uv;o.color=v.color;return o;
  }
  float4 frag(Varying i):SV_Target {
   // The original scalar conversions intentionally use the red component and
   // UV.x. This is NOT a conventional rotation of the incoming RGB hue.
   float hue=i.color.r*(5+i.color.r);
   float3 shifted=saturate(float3(-1+abs(hue-3),2-abs(hue-2),2-abs(hue-4)));
   float weight=saturate(pow(abs(i.uv.x-.5)*2,2));
   float4 base=_Lofted>.5?i.color:tex2D(_MainTex,i.uv)*i.color;
   float alpha=_Lofted>.5?1:step(.5,base.a);
   float3 rgb=lerp(base.rgb,shifted,weight);
   // Preserve source display RGB. Extra gamma encoding washed out low channels
   // in the five-color native comparison. Lit brightness is calibrated separately.
   return float4(rgb,alpha);
  }
  ENDCG
 } }
}
