// Non-audio Waveform.shader equations. Copyright The Tilt Brush Authors.
// Apache-2.0. Preserve vertex bloom before interpolation, then bound native bloom.
Shader "Hidden/OpenBrush/WaveformBake" {
 Properties { _MainTex("Original pattern",2D)="white"{} }
 SubShader { Pass {
 Cull Off ZWrite Off ZTest Always Blend Off
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 float _Seconds, _Ceiling, _Fft;
 sampler2D _MainTex;
 struct Input {float4 vertex:POSITION;float2 uv:TEXCOORD0;float2 copies:TEXCOORD1;float4 color:COLOR;};
 struct Varying {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float copies:TEXCOORD1;float4 color:COLOR;float4 bloomed:TEXCOORD2;};
 Varying vert(Input v){Varying o;o.pos=float4(v.vertex.xy*2-1,0,1);
 #if UNITY_UV_STARTS_AT_TOP
 o.pos.y=-o.pos.y;
 #endif
 o.uv=v.uv;o.color=v.color;o.copies=v.copies.x;
 float minimum=length(v.color.rgb)*.05;
 o.bloomed=pow(float4(max(v.color.rgb,minimum),v.color.a),2.2);
 o.bloomed.rgb*=2*exp(.51785713*10);
 return o;}
 float4 frag(Varying i):SV_Target {
 float envelope=_Fft>.5?1:sin(i.uv.x*3.14159);
 float3 waves=.15*sin(-float3(30,40,50)*i.color.rgb*(_Seconds*3)+i.uv.x*100*i.color.rgb);
 float wave=waves.x+waves.y+waves.z;
 float pinch=(1-envelope)*40+20;
 float waveLine=_Fft>.5?(abs(i.uv.y-.5)>wave?0:wave):saturate(1-pinch*abs(i.uv.y-.5-wave*envelope));
 float3 rgb=max(i.bloomed.rgb*i.bloomed.a*envelope*waveLine*i.copies,0);
 if(_Fft>1.5) {
  // No captured audio: default waveform sampler and zero accumulated beat.
  // A black/white default differs by one V repeat, producing the same pattern.
  float4 pattern=tex2D(_MainTex,float2(i.uv.x*.25,i.uv.y+i.uv.x-.5));
  rgb=max(i.bloomed.rgb*i.bloomed.a*pattern.rgb*pattern.a*i.copies,0);
 }
 // Native Unlit blooms full-range source HDR much more strongly. Preserve
 // channel ratios by bounding the whole vertex-bloom range, not clipping
 // individual moving channels. This is an explicit appearance approximation.
 float peak=max(i.bloomed.r,max(i.bloomed.g,i.bloomed.b))*i.bloomed.a*i.copies;
 float3 decoded=lerp(pow((max(rgb,0)+.055)/1.055,2.4),rgb/12.92,step(rgb,.04045));
 decoded=lerp(decoded,rgb,step(1,rgb));
 decoded*=min(1,_Ceiling/max(peak,1e-6));
 float3 bounded=saturate(decoded/_Ceiling);
 float3 encoded=lerp(1.055*pow(bounded,1/2.4)-.055,12.92*bounded,step(bounded,.0031308));
 return float4(encoded,1);
 }
 ENDCG
 } }
}


