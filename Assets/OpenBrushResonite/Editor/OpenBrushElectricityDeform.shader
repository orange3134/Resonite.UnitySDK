// Copyright 2020 The Tilt Brush Authors. Apache-2.0.
// Normal Electricity.shader vertex equations, evaluated in original canvas axes.
Shader "Hidden/OpenBrush/ElectricityDeform" {
 Properties { _MainTex("Positions and U",2D)="black"{} _Offsets("Offsets",2D)="black"{} }
 SubShader { Pass { ZWrite Off ZTest Always Cull Off Blend Off
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 #include "ElectricityNoiseReference.cginc"
 sampler2D _MainTex,_Offsets;
 float _Seconds,_Mod,_Displacement;
 float3 Displace(float3 p,float mod){
  float3 v=p*(.1+mod)+_Seconds*3;
  float3 a=float3(curlX(v,30),curlY(v,30),curlZ(v,30));
  v=p*(.2+mod)+_Seconds*3*1.777;
  return a*3+float3(curlX(v,100),curlY(v,100),curlZ(v,100))*7;
 }
 float4 frag(v2f_img i):SV_Target {
  float4 data=tex2D(_MainTex,i.uv);float3 p=data.xyz*float3(-1,1,-1),o=tex2D(_Offsets,i.uv).xyz*float3(-1,1,-1);
  float width=length(o)/.02;float3 mid=p-o;
  float envelope=1-pow(1-sin(data.w*3.14159),10);
  // Scaling both position and offset cancels in the noise argument; evaluating
  // in meters preserves the original app-unit displacement after conversion.
  float3 moved=mid+o*envelope;
  if(width>1e-8)moved+=Displace(mid/width,_Mod)*width*_Displacement*envelope;
  return float4(moved*float3(-1,1,-1),1);
 }
 ENDCG
 } }
}
