// Copyright 2020 The Tilt Brush Authors. Apache-2.0.
// Source D3D11 sharp interior, vertex bloom, and bounded native HDR conversion.
Shader "Hidden/OpenBrush/ElectricitySurface" {
 SubShader { Pass { ZWrite Off ZTest Always Cull Off Blend Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 struct A{float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct V{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float3 bloom:TEXCOORD1;};
 V vert(A v){V o;o.pos=float4(v.vertex.xy*2-1,0,1);
 #if UNITY_UV_STARTS_AT_TOP
 o.pos.y=-o.pos.y;
 #endif
 o.uv=v.uv;float minimum=length(v.color.rgb)*.05;o.bloom=pow(max(v.color.rgb,minimum),2.2)*2*exp(.2*10);return o;}
 float4 frag(V i):SV_Target {
  float sharp=abs(i.uv.y-.5)<.1?9:1;
  float3 rgb=i.bloom*sharp;
  float3 decoded=lerp(GammaToLinearSpace(saturate(rgb)),rgb,step(1,rgb));
  // Source bloomColor deliberately raises dim color channels so its sharp
  // core approaches white. Whole-color normalization would erase that core.
  return float4(LinearToGammaSpace(min(decoded,4)/4),1);
 }
 ENDCG
 } }
}
