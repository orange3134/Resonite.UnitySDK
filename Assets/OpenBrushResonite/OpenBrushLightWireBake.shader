// Normal Open Brush LightWire graph and AudioReactive.hlsl equations.
// Copyright The Tilt Brush Authors, Apache-2.0.
Shader "Hidden/OpenBrush/LightWireBake" {
 SubShader { Pass {
 Cull Off ZWrite Off ZTest Always Blend Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 float _Seconds,_Output;
 struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct Varying {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 Varying vert(Input v){Varying o;o.pos=float4(v.vertex.xy*2-1,0,1);
 #if UNITY_UV_STARTS_AT_TOP
 o.pos.y=-o.pos.y;
 #endif
 o.uv=v.uv;o.color=v.color;return o;}
 float4 frag(Varying i):SV_Target {
  float envelope=sin(fmod(i.uv.x*2,1)*3.14159265);
  float bulb=envelope<.1?1:0,border=abs(envelope)<.01?0:1;
  if(_Output<.5)return float4(i.color.rgb*.2*(1-bulb)*border,1);
  if(_Output<1.5)return float4(((.3-.15*bulb)*border).xxx,.3-.3*bulb);
  if(_Output<2.5)return float4(1,lerp(.365,.03,bulb),1,1);
  float on=1-saturate(abs(fmod(i.uv.x*2+.5,7)-fmod(_Seconds*3,7))*1.5);
  int index=(int)fmod(i.uv.x*2+.5,3);
  float3 tint=index==0?float3(.2,.2,1):index==1?float3(1,.2,.2):float3(.2,1,.2);
  float3 c=i.color.rgb*tint*on;
  c=max(c,length(c)*.05);
  // Bake the complete source HDR expression before native color processing.
  // Splitting gain from the map is not equivalent across native color profiles.
  return float4(pow(max(c,0),2.2)*bulb*(2*exp(7.0)),1);
 }
 ENDCG
 } }
}
