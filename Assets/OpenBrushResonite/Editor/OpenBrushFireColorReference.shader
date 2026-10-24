// Tilt Brush Authors Fire.shader / Brush.cginc equations, Apache-2.0.
// See OpenBrushFireImport.cs for the copyright and license notice.
Shader "Hidden/OpenBrushFireColorReference" {
SubShader { Pass { ZTest Always Cull Off ZWrite Off
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
float4 _Brush,_Sample;
float _Copies,_Mode;
float4 frag(v2f_img input):SV_Target {
    float4 color=_Brush;
    float cmin=length(color.rgb)*.05;
    color.rgb=max(color.rgb,float3(cmin,cmin,cmin));
    color=pow(color,2.2);color.rgb*=2*exp(.5*10);
    if(_Mode<.5)return color;
    color*= _Sample;
    return float4(color.rgb*color.a*_Copies,1);
}
ENDCG
} } }
