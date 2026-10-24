// Reference fragment equations from Tilt Brush Authors Comet.shader, Apache-2.0.
// Full copyright/license notice accompanies OpenBrushCometImport.cs.
Shader "Hidden/OpenBrushCometReference" {
Properties {_MainTex("Pattern",2D)="white" {} _Gradient("Gradient",2D)="white" {}}
SubShader { Pass { ZTest Always Cull Off ZWrite Off
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex,_Gradient;
float4 _UVTime;
float4 frag(v2f_img input):SV_Target {
    float2 uv=_UVTime.xy;float time=-_UVTime.z;
    float r=tex2D(_MainTex,uv+float2(time,time)).r;
    float g=tex2D(_MainTex,uv+float2(time*1.5,0)).g;
    float b=tex2D(_MainTex,uv+float2(time*.5,0)).b;
    if (_UVTime.w > .5) return float4(r,g,b,1);
    float lookup=(r+g+b)/3.0*(1-uv.x);
    lookup=(pow(lookup,2)+.125)*3;
    float falloff=max((.2-uv.x)*5,0);
    return float4(tex2D(_Gradient,saturate(lookup+falloff)).rrr,1);
}
ENDCG
} } }

