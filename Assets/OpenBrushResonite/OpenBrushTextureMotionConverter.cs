using System;
using UnityEngine;
using FrooxEngine;

public class OpenBrushTextureMotionConverter : ResoniteComponentConverter<OpenBrushTextureMotion>
{
    public ResRef<OpenBrushTextureMultiplexer> Sequence;
    public ResRef<TimeIntDriver> Clock;
    public ResRef<OpenBrushIndexGradient> TimelineIndex;
    public ResRef<PlaybackPositionDriver> TimelinePosition;
    protected override void UpdateConversion(OpenBrushTextureMotion source, IConversionContext context)
    {
        if (source.Frames == null || source.Frames.Length < 2) throw new InvalidOperationException("Missing baked brush frames.");
        if(source.Timeline!=null && (source.FrameTimes==null || source.FrameTimes.Length!=source.Frames.Length))
            throw new InvalidOperationException("Texture timeline needs one timestamp per frame");
        var material = context.GetMaterial(source.SourceMaterial);
        var sequence = EnsureComponent(ref Sequence);
        sequence.Enabled = source.enabled; sequence.persistent = true;
        if(material is PBS_Specular lit) {
            if(source.EmissionOnly && source.SpecularOnly) throw new InvalidOperationException("Conflicting texture motion channels");
            sequence.Target=source.EmissionOnly?lit.EmissiveMap_Element.Member:source.SpecularOnly?lit.SpecularMap_Element.Member:lit.AlbedoTexture_Element.Member;
            lit.TextureScale=source.TextureScale;
        } else if (source.EmissionOnly && material is PBS_VertexColorMetallic surface) {
            sequence.Target = surface.EmissiveMap_Element.Member;
            surface.TextureScale = source.TextureScale;
        } else if (!source.EmissionOnly && material is MatcapMaterial matcap) {
            sequence.Target=matcap.Matcap_Element.Member;
        } else if (!source.EmissionOnly && material is FresnelMaterial fresnel) {
            sequence.Target=fresnel.NearTexture_Element.Member;fresnel.NearTextureScale=source.TextureScale;
        } else if (!source.EmissionOnly && material is UnlitMaterial unlit) {
            sequence.Target = unlit.Texture_Element.Member;
            unlit.TextureScale = source.TextureScale;
        } else if (!source.EmissionOnly && material is PBS_VertexColorMetallic diffuse) {
            sequence.Target = diffuse.AlbedoTexture_Element.Member;
            diffuse.TextureScale = source.TextureScale;
        } else if (!source.EmissionOnly && material is PBS_VertexColorSpecular specular) {
            sequence.Target = specular.AlbedoTexture_Element.Member;
            specular.TextureScale = source.TextureScale;
        } else throw new InvalidOperationException("Unexpected brush material for texture animation.");
        // Do not accumulate duplicate frame references on repeated SDK conversions.
        while (sequence.Assets.Count > source.Frames.Length) sequence.Assets.RemoveAt(sequence.Assets.Count - 1);
        for (int i=0; i<source.Frames.Length; i++) {
            var texture = context.GetITexture2D(source.Frames[i]);
            if (sequence.Assets.Count <= i) sequence.Assets.Add(texture); else sequence.Assets[i] = texture;
        }
        var clock = EnsureComponent(ref Clock);
        clock.Enabled = source.enabled; clock.persistent = true;
        clock.Target = sequence.Index_Element.Member;
        clock.Scale = source.FramesPerSecond;
        clock.Repeat = source.Frames.Length;
        clock.PingPong = false;
        if(source.Timeline!=null) {
            clock.Enabled=false;clock.Target=null;
            var index=EnsureComponent(ref TimelineIndex);
            OpenBrushTimelineLink.Index(index,source.FrameTimes,sequence.Index_Element.Member);
            OpenBrushTimelineLink.Position(EnsureComponent(ref TimelinePosition),source.Timeline,index.Progress_Element.Member);
        } else {
            if(TimelineIndex!=null){DestroyImmediate(TimelineIndex);TimelineIndex=default;}
            if(TimelinePosition!=null){DestroyImmediate(TimelinePosition);TimelinePosition=default;}
        }
    }
    protected override void Cleanup() {
        if(TimelineIndex!=null)DestroyImmediate(TimelineIndex);
        if(TimelinePosition!=null)DestroyImmediate(TimelinePosition);
        if (Clock != null) DestroyImmediate(Clock);
        if (Sequence != null) DestroyImmediate(Sequence);
    }
}



