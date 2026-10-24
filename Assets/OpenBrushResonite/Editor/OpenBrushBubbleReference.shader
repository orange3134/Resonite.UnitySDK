Shader "Hidden/OpenBrushBubbleReference" {
SubShader { Pass { ZTest Always Cull Off ZWrite Off
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
#include "BubbleNoiseReference.cginc"
float4 _SeedTime;
float4 frag(v2f_img i):SV_Target {
    float3 seed=_SeedTime.xyz;float seconds=_SeedTime.w;
    float t=seconds*.5+1;
    float3 jitter=float3(sin(t+seconds+seed.z*.2),cos(t*1.2+seconds+seed.x*.2),cos(t+seconds+seed.x*.2))*.02;
    float3 v=(seed+jitter)*.1+seconds*.25;
    float3 curl=float3(curlX(v,30),curlY(v,30),curlZ(v,30))*10;
    return float4((jitter+curl)*.1,1);
}
ENDCG
} } }
