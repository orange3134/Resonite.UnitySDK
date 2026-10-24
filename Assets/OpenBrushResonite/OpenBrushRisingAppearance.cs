using System;
using System.Collections.Generic;
using UnityEngine;

// Normal RisingBubbles material: scroll=.1, distance=(.5,5,0), jitter=.2,
// frequency=5, spread=1.25. Equations ported from Open Brush RisingBubbles.shader
// (Copyright 2020 The Tilt Brush Authors, Apache License 2.0).
public static class OpenBrushRisingAppearance {
    public const int PhaseCount=64;
    public static float Phase(float seconds,float seed)=>Mathf.Repeat(seconds*.1f+seed*10,1);
    public static int PhaseIndex(float seconds,float seed)=>Mathf.Min(PhaseCount-1,Mathf.FloorToInt(Phase(seconds,seed)*PhaseCount));
    public static Vector3 Displacement(Vector3 center,float seed,float seconds) {
        float phase=Phase(seconds,seed),wave=phase*5+seconds/3;
        return .1f*new Vector3(.5f*phase+Mathf.Sin(wave+seed*10+center.z)*.2f,
            (5+(Mathf.Repeat(seed*100,1)-.5f)*5)*phase,
            Mathf.Cos(wave+seed*7+center.x)*.2f);
    }
    public static Color Shade(Color tex,Color paint,float phase) {
        // Decode after adding the independent white reflection; no HDR clamp.
        float gain=5*(1-phase);
        return new Color(Mathf.GammaToLinearSpace(tex.r*paint.r*gain+tex.a),
            Mathf.GammaToLinearSpace(tex.g*paint.g*gain+tex.a),
            Mathf.GammaToLinearSpace(tex.b*paint.b*gain+tex.a),1);
    }
    public static void Prepare(OpenBrushParticleMotion motion) {
        var source=motion.SourceMaterial.mainTexture as Texture2D;
        if(source==null || !source.isReadable)throw new InvalidOperationException("Rising Bubbles requires readable original RGBA sprite");
        var palette=new List<Color>();var map=new Dictionary<Color,int>();var entries=new int[motion.Particles.Length];
        for(int i=0;i<entries.Length;i++) {
            var c=motion.Particles[i].color;c.a=1;
            if(!map.TryGetValue(c,out int n)){n=palette.Count;palette.Add(c);map.Add(c,n);}
            entries[i]=n;
        }
        // Area-filter the 512px sprite into 128px phase charts. At normal drawing
        // sizes bubbles span only a few pixels; mipmaps retain their thin rim.
        // 64 levels bound the paint-factor error to 5/128; highlights are exact.
        int sw=Mathf.Min(128,source.width),sh=Mathf.Min(128,source.height);
        if(source.width%sw!=0 || source.height%sh!=0)throw new InvalidOperationException("Unsupported Rising Bubbles source dimensions");
        int count=palette.Count*PhaseCount,columns=Mathf.CeilToInt(Mathf.Sqrt(count)),rows=(count+columns-1)/columns;
        const int pad=4;int tw=sw+2*pad,th=sh+2*pad,w=columns*tw,h=rows*th;
        if(w>8192 || h>8192 || (long)w*h>16777216)throw new InvalidOperationException("Rising Bubbles color/phase atlas exceeds 16M pixels; split the sketch");
        var sourcePixels=source.GetPixels();var samples=new Color[sw*sh];int sx=source.width/sw,sy=source.height/sh;
        for(int y=0;y<sh;y++)for(int x=0;x<sw;x++) {
            var sum=Color.clear;
            for(int dy=0;dy<sy;dy++)for(int dx=0;dx<sx;dx++)sum+=sourcePixels[(y*sy+dy)*source.width+x*sx+dx];
            samples[y*sw+x]=sum/(sx*sy);
        }
        var pixels=new Color[w*h];
        for(int n=0;n<count;n++)for(int y=0;y<th;y++)for(int x=0;x<tw;x++) {
            var tex=samples[Mathf.Clamp(y-pad,0,sh-1)*sw+Mathf.Clamp(x-pad,0,sw-1)];
            pixels[(n/columns*th+y)*w+n%columns*tw+x]=Shade(tex,palette[n/PhaseCount],(n%PhaseCount+.5f)/PhaseCount);
        }
        var atlas=new Texture2D(w,h,TextureFormat.RGBAHalf,true,true){name="RisingBubbles_ColorPhase_WhiteHighlight",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        atlas.SetPixels(pixels);atlas.Apply(true,false);motion.BakedTexture=atlas;
        motion.PhaseUV=new Vector4[entries.Length][];
        for(int i=0;i<entries.Length;i++) {
            motion.PhaseUV[i]=new Vector4[PhaseCount];var uv=motion.Particles[i].uv;
            for(int f=0;f<PhaseCount;f++) {
                int n=entries[i]*PhaseCount+f;
                motion.PhaseUV[i][f]=new Vector4((n%columns*tw+pad+uv.x*sw)/w,
                    (n/columns*th+pad+(1-uv.w)*sh)/h,
                    (n%columns*tw+pad+uv.z*sw)/w,
                    (n/columns*th+pad+(1-uv.y)*sh)/h);
            }
        }
    }
}
