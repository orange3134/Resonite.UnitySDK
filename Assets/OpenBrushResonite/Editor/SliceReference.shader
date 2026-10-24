// Test oracle: equations from normal Slice.shader / ColorSpace.cginc (Apache-2.0).
Shader "Hidden/OpenBrush/SliceReference" {
 Properties {_MainTex("Texture",2D)="white"{}}
 SubShader {Pass {
 Cull Off ZWrite Off ZTest Always
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 #include "../OpenBrushColorSpaceReference.cginc"
 float4 _UVW;
 float4 frag(v2f_img i):SV_Target {
  float hue=fmod(_UVW.z*5,6);
  float3 base_rgb=hue06_to_base_rgb(hue);
  return float4(saturate(cy_to_rgb(base_rgb,_UVW.x,_UVW.y)),1);
 }
 ENDCG
 }}
}
