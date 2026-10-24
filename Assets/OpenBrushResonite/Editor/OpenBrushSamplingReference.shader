// Sampling tests use normal-material textures from Open Brush Unity Tools.
// Fragment equations adapted from Tilt Brush Authors Fire.shader and
// Highlighter/AdditiveCutout.shader (Apache-2.0); WigglyGraphite's UV animation.
Shader "Hidden/OpenBrushSamplingReference" {
Properties {_MainTex("Pattern",2D)="white" {}}
SubShader { Pass { ZTest Always Cull Off ZWrite Off
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;
float4 _UVTimeMode;
float4 frag(v2f_img input):SV_Target {
    float2 uv=_UVTimeMode.xy;
    if (_UVTimeMode.w>.5 && _UVTimeMode.w<1.5) {
        float displacement=tex2D(_MainTex,uv+float2(-_UVTimeMode.z,0)).a*.1;
        float4 c=tex2D(_MainTex,uv-displacement);
        c.rgb*=step(.01,c.rgb);return c;
    }
    if (_UVTimeMode.w>1.5) uv.x=(uv.x+ceil(fmod(_UVTimeMode.z*12,6)))*1.1;
    return tex2D(_MainTex,uv);
}
ENDCG
} } }
