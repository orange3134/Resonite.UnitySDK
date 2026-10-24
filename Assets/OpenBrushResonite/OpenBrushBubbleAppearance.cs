using System;
using System.Collections.Generic;
using UnityEngine;

// Bubbles adds texture alpha as a white highlight BEFORE source gamma decoding.
// Bake that operation per original RGB into an HDR atlas, keeping highlights
// white even on a black stroke. Multiplying an RGBA sprite cannot reproduce it.
public static class OpenBrushBubbleAppearance {
    public static Color Shade(Color tex,Color brush) => new Color(
        Mathf.GammaToLinearSpace(tex.r*brush.r+tex.a),
        Mathf.GammaToLinearSpace(tex.g*brush.g+tex.a),
        Mathf.GammaToLinearSpace(tex.b*brush.b+tex.a),1);
    public static void Prepare(OpenBrushParticleMotion motion) {
        var source=motion.SourceMaterial.mainTexture as Texture2D;
        if(source==null || !source.isReadable)throw new InvalidOperationException("Bubbles requires readable source RGBA texture");
        var palette=new List<Color>();var map=new Dictionary<Color,int>();
        var entries=new int[motion.Particles.Length];
        for(int i=0;i<entries.Length;i++) {
            var c=motion.Particles[i].color;c.a=1; // Alpha is random phase, never paint opacity.
            if(!map.TryGetValue(c,out int index)){index=palette.Count;palette.Add(c);map.Add(c,index);}
            entries[i]=index;
        }
        int columns=Mathf.CeilToInt(Mathf.Sqrt(palette.Count)),rows=(palette.Count+columns-1)/columns;
        const int pad=2;int tw=source.width+pad*2,th=source.height+pad*2,w=columns*tw,h=rows*th;
        if(w>8192 || h>8192 || (long)w*h>16777216)throw new InvalidOperationException("Bubble color atlas exceeds texture budget; split the sketch.");
        var pixels=source.GetPixels();var output=new Color[w*h];
        for(int n=0;n<palette.Count;n++) {
            for(int y=0;y<th;y++)for(int x=0;x<tw;x++) {
                var tex=pixels[Mathf.Clamp(y-pad,0,source.height-1)*source.width+Mathf.Clamp(x-pad,0,source.width-1)];
                output[(n/columns*th+y)*w+n%columns*tw+x]=Shade(tex,palette[n]);
            }
        }
        var atlas=new Texture2D(w,h,TextureFormat.RGBAFloat,true,true){name="Bubbles_ColorAndWhiteHighlight",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        atlas.SetPixels(output);atlas.Apply(true,false);motion.BakedTexture=atlas;
        motion.BakedUV=new Vector4[entries.Length];
        for(int i=0;i<entries.Length;i++) {
            int n=entries[i];var uv=motion.Particles[i].uv;
            // UV here is already converted to Unity orientation; particle Bake must not flip again.
            motion.BakedUV[i]=new Vector4((n%columns*tw+pad+uv.x*source.width)/w,
                (n/columns*th+pad+(1-uv.w)*source.height)/h,
                (n%columns*tw+pad+uv.z*source.width)/w,
                (n/columns*th+pad+(1-uv.y)*source.height)/h);
        }
    }
}
