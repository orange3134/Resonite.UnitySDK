// Fragment equations from the normal Open Brush Hypercolor graph/hlsl.
// Copyright 2020 The Tilt Brush Authors. Apache-2.0.
Shader "Hidden/OpenBrush/HypercolorBake" {
 Properties { _MainTex("Pattern",2D)="white"{} _Bump("Normal",2D)="bump"{} }
 SubShader { Pass {
 Cull Off ZWrite Off ZTest Always Blend Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 sampler2D _MainTex,_Bump;float _Seconds,_Output;
 struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct Varying {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 Varying vert(Input v){Varying o;o.pos=float4(v.vertex.xy*2-1,0,1);
 #if UNITY_UV_STARTS_AT_TOP
 o.pos.y=-o.pos.y;
 #endif
 o.uv=v.uv;o.color=v.color;return o;}
 float3 Decode(float3 rgb){return lerp(pow(max(abs((rgb+.055)/1.055),1.192092896e-7),2.4),rgb/12.92,step(rgb,.04045));}
 float4 frag(Varying i):SV_Target {
  if(_Output>1.5)return tex2Dlod(_Bump,float4(i.uv,0,0));
  // Fixed LOD avoids quality-settings-dependent anisotropic bake filtering.
  float4 source=tex2Dlod(_MainTex,float4(i.uv,0,0));float scroll=2*_Seconds;
  // The HLSL overwrites red BEFORE computing green and blue.
  float r=(sin(source.r*2+scroll*.5-i.uv.x)+1)*2;
  float g=(sin(r*3.3+scroll-i.uv.x)+1)*2;
  float b=(sin(r*4.66+scroll*.25-i.uv.x)+1)*2;
  float3 anim=float3(r,g,b);
  if(_Output>.5)return float4(LinearToGammaSpace(saturate(anim*.2352941)),.4);
  // Graph explicitly decodes the complete tinted RGB, including HDR >1.
  // Bounded diffuse range 32; material restores the atlas scale.
  return float4(LinearToGammaSpace(saturate(Decode(anim*i.color.rgb)/32)),source.a);
 }
 ENDCG
 } }
}



