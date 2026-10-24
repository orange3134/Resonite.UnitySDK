// Copyright 2020 The Tilt Brush Authors
// Copyright 2026 Open Brush Resonite converter contributors
// Licensed under the Apache License, Version 2.0.
// Fragment equations from the normal Plasma/Streamers/ChromaticWave/Rainbow materials. No audio input.
Shader "Hidden/OpenBrush/FlowBake" {
 Properties { _MainTex("Source pattern",2D)="white"{} }
 SubShader { Pass {
  Cull Off ZWrite Off ZTest Always Blend Off
  CGPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #pragma target 3.0
  #include "UnityCG.cginc"
  #include "OpenBrushCelestial.cginc"
  sampler2D _MainTex, _DisplaceTex;
  float _Seconds, _Kind, _TextureGain, _CelestialCeiling;
  struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; float2 copies:TEXCOORD1; float4 sourceBloom:TEXCOORD2; float4 color:COLOR; };
  struct Varying { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float copies:TEXCOORD1; float4 color:COLOR; float4 bloomed:TEXCOORD2; };
  Varying vert(Input v) {
   Varying o;o.pos=float4(v.vertex.xy*2-1,0,1);
   // Direct clip-space rendering needs the render-target Y flip on D3D.
   #if UNITY_UV_STARTS_AT_TOP
    o.pos.y=-o.pos.y;
   #endif
   o.uv=v.uv;o.color=v.color;o.copies=v.copies.x;
   // The nonlinear part was evaluated on ORIGINAL vertices before clipping.
   // Keep original RGBA too: ChromaticWave uses it for wave frequency.
   o.bloomed=v.sourceBloom;
   o.bloomed.rgb*=2*exp((_Kind>5.5 && _Kind<6.5?.405:.45)*10);
   return o;
  }
  float4 frag(Varying i):SV_Target {
   if(_Kind>7.5) {
    // Source has Blend One One, so phase/paint alpha must not attenuate RGB.
    float3 rgb=(_Kind<8.5?FairyFragment(i.uv,i.color,_Seconds):SpaceFragment(i.uv,i.color,_Seconds)).rgb*i.copies;
    rgb=max(rgb,0);
    // The source player renders in Gamma. Decode its display-range output
    // once, retaining HDR values. This family uses a linear RGBAHalf atlas.
    float3 linearRgb=lerp(GammaToLinearSpace(saturate(rgb)),rgb,step(1,rgb));
    return float4(min(linearRgb,_CelestialCeiling),1);
   }
   float4 tex;
   if (_Kind<.5) {
    float alpha=saturate(i.color.a);
    float3 A=float3(.55,.3,.7), rate=float3(1.2,1,1.33), M=float3(1,2.2,1.5);
    float3 us=A*i.uv.x-rate*_Seconds;
    float3 tmp=abs(frac(M*A*i.uv.x-(float3(1.5,3,2.25)+M*rate)*_Seconds)-.5);
    float3 vs=i.uv.y+.4*alpha*float3(1,-1,1)*tmp;
    vs=saturate(lerp((vs-.5)*4,vs,sin(3.14159*.5*alpha)));
    tex=tex2D(_MainTex,float2(us.x,vs.x))+tex2D(_MainTex,float2(us.y,vs.y))+tex2D(_MainTex,float2(us.z,vs.z));
    float3 lineDistance=(vs-.5)/.012;
    tex+=dot(1-saturate(lineDistance*lineDistance),float3(1,1,1));
    tex*=.8*(1+30*pow(1-alpha,5));
    i.color.a=1;
   } else if (_Kind<1.5) {
    float row=floor(i.uv.y*5);
    float random=frac(sin(dot(row.xx,float2(12.9898,78.233)*2))*43758.5453);
    float2 uv=i.uv;uv.x+=random*200;
    uv.y=5*uv.y+dot(float2(.05,-.05),sin(uv.x*float2(10,23)+_Seconds*float2(10,6)));
    uv.x*=.5+random*.3;uv.x-=_Seconds*(.5+fmod(row*1.61803398875,1));
    tex=tex2D(_MainTex,uv);tex+=tex*tex*55;
    float edge=fmod(uv.y,1);tex*=edge*edge*(1-edge)*(1-edge);
    tex*=exp(.4*5);
   } else if (_Kind<2.5) {
    // Non-audio ChromaticWave; GetTime().w is three times elapsed seconds.
    // The default inactive audio accumulator is zero. Incoming alpha is retained.
    float y=fmod(i.uv.y+4*i.uv.x,1);
    float3 wave=.15*sin(-float3(20,30,40)*i.color.rgb*(_Seconds*3)+i.uv.x*100*i.color.rgb);
    tex=float4(saturate(1-40*abs(y-.5+wave)),1);
    i.color=i.bloomed;
   } else if (_Kind<3.5) {
    // Normal Rainbow, emission .65; GetTime().z = 2 seconds.
    // Source ignores vertex alpha and uses Max blending, so coincident
    // front/back copies must NOT multiply brightness. Native overlaps
    // between distinct strokes still use additive blending (approximation).
    float y=saturate(i.uv.y)*5;
    float row=floor(fmod(floor(y)+floor(frac(_Seconds*.4)*5),5)+.1);
    float band=fmod(y,1);
    float3 hue=row==0?float3(1,0,0):row==1?float3(.7,.3,0):row==2?float3(0,1,0):row==3?float3(0,.2,1):float3(.4,0,1.2);
    float pulse=pow((sin(row+_Seconds*2)+1)*.5,5);
    float edge=saturate(pow(band*(1-band)*5,50));
    tex=float4(hue*pulse*edge*exp(.65*3),1);
    i.color.a=1;i.copies=1;
   } else if (_Kind<4.5) {
    // Normal Wind: _Speed=1, RGB texture only. Vertex alpha attenuates emission;
    // texture alpha is not used by encodeHdr(float3) in the original shader.
    tex=float4(tex2D(_MainTex,i.uv+float2(_Seconds*.5,0)).rgb,1);
   } else if (_Kind<5.5) {
    // Normal Rain: _MainTex_ST=(4,1,0,0), _Speed=4, _NumSides=6.
    // Signed fmod and row hash deliberately match the original HLSL.
    float2 uv=i.uv*float2(4,1);
    float row=(int)(uv.y*6);
    float random=abs(frac(sin(dot(row.xx,float2(12.9898,78.233)*2))*4550))*.7;
    float u=fmod(uv.x*4-fmod(_Seconds*16,4)+random*_Seconds*11,4);
    tex=tex2D(_MainTex,float2(u,uv.y*6));
    if(u<0 || u>1)tex=0;
    // Source fades RGBA before multiplying final RGB by final alpha.
    tex*=1-saturate(pow(abs(uv.x*.25),9));
   } else if (_Kind<6.5) {
    // Normal Fire2 (no audio). DisplaceTex_ST is not used by its shader.
    float2 displacement=(tex2D(_DisplaceTex,i.uv).xy*2-1)*.04;
    float mask=tex2D(_MainTex,i.uv).g;
    float2 uv=i.uv+displacement;
    float flame1=tex2D(_MainTex,uv*.7+float2(-_Seconds*.75,0)).r;
    float flame2=tex2D(_MainTex,float2(uv.x,1-uv.y)+float2(-_Seconds*.4,-_Seconds*.1)).r;
    float flames=smoothstep(0,.8,mask*saturate(flame1+flame2)*.5)*mask;
    // Original _FlameFadeMin=8.53; clamp base only to avoid roundoff NaNs.
    flames*=pow(max(0,1-i.uv.x),8.53)*17.06;
    tex=float4(flames.xxx,1);i.color=i.bloomed;
   } else {
    // Normal Sparks: nonlinear longitudinal speed and face phase offsets.
    float x=pow(max(i.uv.x,0),1.73);
    float row=(int)(i.uv.y*4);
    float random=abs(2*frac(sin(dot(row.xx,float2(12.9898,78.233)*2))*50))*.75;
    float u=fmod(x*11.78-fmod(_Seconds*3*11.78,11.78)+random*11.78,11.78);
    tex=tex2D(_MainTex,float2(u,i.uv.y*4));
    if(u<0 || u>1)tex=0;
    // Original bloom multiplies RGBA, so the eventual RGB*alpha squares it.
    tex*=exp(.723*5)*(1-x);
   }
   float4 c=i.color*tex;
   float3 rgb=max(c.rgb*c.a*i.copies,0);
   // Like Brush.cginc SrgbToLinear_Large: decode 0..1 while leaving HDR
   // magnitudes alone. Bound Streamers highlights separately from Plasma.
   float3 rgbLinear=lerp(GammaToLinearSpace(saturate(rgb)),rgb,step(1,rgb));
   if(_Kind>1.5 && _Kind<2.5) {
    // Compress the whole source color range, not each moving spectral line.
    // Independent channel clipping made secondary green as bright as blue.
    // Decode first so dim channels keep their original source color response.
    float peak=max(i.bloomed.r,max(i.bloomed.g,i.bloomed.b))*i.bloomed.a*i.copies;
    rgbLinear*=min(1,_TextureGain/max(peak,1e-6));
   }
   rgbLinear=min(rgbLinear,_Kind<.5?1:_TextureGain);
   // Carry bounded HDR in an ordinary sRGB atlas with a matching linear
   // material multiplier. This avoids RGBAFloat's fourfold memory cost.
   return float4(LinearToGammaSpace(rgbLinear/_TextureGain),1);
  }
  ENDCG
 } }
}

