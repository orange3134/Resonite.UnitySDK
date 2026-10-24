// Test oracle uses the source function unchanged.
Shader "Hidden/OpenBrush/KeijiroReference" {
 SubShader {Pass {Cull Off ZWrite Off ZTest Always
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 #define _IS_TILT_MESH 1
 #include "KeijiroSourceReference.hlsl"
 float4 _Position,_Normal,_UVRadius;float _Seconds;
 float4 frag(v2f_img i):SV_Target {
  float3 result;KeijiroTubeVertex_float(_UVRadius.xy,_UVRadius.z,_Seconds,_Position.xyz,_Normal.xyz,result);
  return float4(result,1);
 }
 ENDCG
 }}
}
