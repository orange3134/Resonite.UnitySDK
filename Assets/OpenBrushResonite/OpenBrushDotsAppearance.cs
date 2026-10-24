using System;
using System.Linq;
using UnityEngine;

// Normal Dots.mat: BaseGain=.4, EmissionGain=300, Tint=white. No audio input.
// Two layers factor RGB*(1+alpha*300) without a texture atlas per random alpha.
// Raw sprite RGB and HDR mesh colors are preserved; native gamma/bloom differs.
public static class OpenBrushDotsAppearance {
    public static void Prepare(Transform parent,OpenBrushParticleMotion.Particle[] particles,Material material,OpenBrushTimeline timeline) {
        var source=material.mainTexture as Texture2D;
        if(source==null || !source.isReadable)throw new InvalidOperationException("Dots requires the original readable sprite RGBA");
        if(material.mainTextureScale!=Vector2.one || material.mainTextureOffset!=Vector2.zero)throw new InvalidOperationException("Unsupported Dots sprite transform");
        var times=timeline==null ? new[]{0f} : particles.Select(p=>p.playBirth).Concat(new[]{0f,timeline.Duration}).Distinct().OrderBy(t=>t).ToArray();
        if(times.Any(t=>!float.IsFinite(t) || t<0 || (timeline!=null && t>timeline.Duration)))throw new InvalidOperationException("Invalid Dots birth time");
        if((long)times.Length*particles.Length*2>1000000)throw new InvalidOperationException("Dots bake exceeds 1,000,000 frame-particles; split the sketch");
        var original=source.GetPixels();
        foreach(bool emission in new[]{false,true}) {
            var child=new GameObject(emission ? "Dots - Emission" : "Dots - Base Color");child.transform.SetParent(parent,false);
            var motion=child.AddComponent<OpenBrushParticleMotion>();
            motion.Kind=emission ? OpenBrushParticleMotion.BrushKind.DotsEmission : OpenBrushParticleMotion.BrushKind.DotsBase;
            motion.Particles=particles;motion.SourceMaterial=material;motion.Timeline=timeline;motion.ExplicitTimes=times;motion.PeakGain=1;
            var pixels=(Color[])original.Clone();if(!emission)for(int i=0;i<pixels.Length;i++)pixels[i].a=1;
            var texture=new Texture2D(source.width,source.height,TextureFormat.RGBA32,source.mipmapCount>1,true){
                name=emission?"Dots_RawEmissionSprite":"Dots_RawBaseSprite",filterMode=source.filterMode,wrapModeU=source.wrapModeU,wrapModeV=source.wrapModeV};
            texture.SetPixels(pixels);texture.Apply(source.mipmapCount>1,false);motion.BakedTexture=texture;
            motion.Bake();
        }
    }
}
