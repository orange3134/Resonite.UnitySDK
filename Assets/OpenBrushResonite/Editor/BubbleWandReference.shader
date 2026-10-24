// Normal Open Brush BubbleWand vertex oracle, Apache-2.0; noise is MIT.
Shader "Hidden/OpenBrush/BubbleWandReference" {
 SubShader { Pass { ZTest Always Cull Off ZWrite Off
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #pragma target 3.5
 #include "UnityCG.cginc"
 #define _IS_TILT_MESH
 #include "BubbleWandVertex.hlsl"
 float3 _Position,_Normal,_UVRadius;
 float _Seconds,_OutputNormal;
 float4 frag(v2f_img i):SV_Target {
  float3 basis=float3(-1,1,-1),p,n;
  BubbleWandVertex_float(_Position*basis*10,_Normal*basis,float4(_UVRadius.xy,_UVRadius.z*10,0),0,_Seconds,-.54,3,1,p,n);
  return float4((_OutputNormal>0 ? n : p*.1)*basis,1);
 }
 ENDCG
 } }
}
