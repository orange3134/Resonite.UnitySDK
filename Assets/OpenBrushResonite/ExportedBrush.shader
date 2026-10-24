// SDK conversion marker and basic Unity preview. Resonite uses the converter's native material.
Shader "OpenBrush/ExportedBrush"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TintColor ("Tint", Color) = (1,1,1,1)
        _MaskTex ("Alpha mask", 2D) = "white" {}
        _UseMask ("Use baked alpha mask", Float) = 0
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _Gain ("Preview gain", Float) = 1
        _Cutoff ("Alpha cutoff", Float) = 0
        _SrcBlend ("Source blend", Float) = 1
        _DstBlend ("Destination blend", Float) = 0
        _ZWrite ("ZWrite", Float) = 1
    }
    SubShader
    {
        Cull Off
        Blend [_SrcBlend] [_DstBlend]
        ZWrite [_ZWrite]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex, _MaskTex;
            float4 _MainTex_ST, _TintColor;
            float _Gain, _Cutoff, _UseMask;
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.color=v.color; o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o; }
            float4 frag(v2f i):SV_Target {
                float4 c=tex2D(_MainTex,i.uv)*i.color*_TintColor;
                clip((_UseMask > .5 ? tex2D(_MaskTex,i.uv).a : c.a)-_Cutoff); c.rgb*=_Gain; return c;
            }
            ENDCG
        }
    }
}


