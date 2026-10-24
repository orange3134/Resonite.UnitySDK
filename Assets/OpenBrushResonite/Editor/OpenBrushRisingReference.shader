// Copyright 2020 The Tilt Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// Original RisingBubbles.shader displacement, isolated for CPU/GPU comparison.
Shader "Hidden/OpenBrushRisingReference" {
 SubShader { Pass { ZTest Always Cull Off ZWrite Off
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _CenterSeed;
 float _Seconds;
 float4 frag(v2f_img i):SV_Target {
    float3 pos=_CenterSeed.xyz;
    float seed=_CenterSeed.w;
    float t01=fmod(_Seconds*0.1+seed*10,1);
    float t2=_Seconds/3;
    float3 dispVec=float3(.5,5,0)*t01;
    dispVec.x+=sin(t01*5+seed*10+t2+pos.z)*.2;
    dispVec.y+=(fmod(seed*100,1)-.5)*5*t01;
    dispVec.z+=cos(t01*5+seed*7+t2+pos.x)*.2;
    return float4(dispVec*.1,t01);
 }
 ENDCG
 } }
}
