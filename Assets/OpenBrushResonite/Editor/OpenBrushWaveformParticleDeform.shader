// Copyright 2020 The Tilt Brush Authors. Apache-2.0.
// Normal WaveformParticles.shader, audio disabled; original source coordinates.
Shader "Hidden/OpenBrush/WaveformParticleDeform" {
 Properties { _MainTex("Positions and age",2D)="black"{} _Offsets("Offsets",2D)="black"{} _Paint("Paint",2D)="black"{} }
 SubShader { Pass { ZWrite Off ZTest Always Cull Off Blend Off
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 #include "ElectricityNoiseReference.cginc"
 sampler2D _MainTex,_Offsets,_Paint;
 float _Seconds;
 float4 frag(v2f_img i):SV_Target {
  float4 data=tex2D(_MainTex,i.uv);float3 offset=tex2D(_Offsets,i.uv).xyz;
  float4 paint=tex2D(_Paint,i.uv);
  // Export metres and axes -> Open Brush decimetres and canvas axes.
  float3 mid=(data.xyz-offset)*float3(-10,10,-10);
  float age=max(0,_Seconds-data.w),release=saturate(age*.1);
  float3 q=mid*(1.5+paint.r)+age;float d=10+paint.g*3;
  float3 moved=mid+release*10*float3(curlX(q,d),curlY(q,d),curlZ(q,d));
  return float4(moved*float3(-.1,.1,-.1)+offset,1);
 }
 ENDCG
 } }
}
