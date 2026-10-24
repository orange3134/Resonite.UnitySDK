using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FrooxEngine;

public class OpenBrushParticleMotionConverter : ResoniteComponentConverter<OpenBrushParticleMotion> {
    public ResRef<FrooxEngine.MeshRenderer> Renderer;
    public ResRef<UnlitMaterial> Material;
    public ResRef<OpenBrushMeshMultiplexer> Sequence;
    public ResRef<TimeIntDriver> Clock;
    public ResRef<OpenBrushIndexGradient> Index;
    public ResRef<PlaybackPositionDriver> Position;
    
    protected override void UpdateConversion(OpenBrushParticleMotion source,IConversionContext context) {
        if(source.Particles==null || source.Particles.Length==0 || (long)source.FrameCount*source.Particles.Length>1000000)
            throw new InvalidOperationException("Particle bake requires 1..1,000,000 frame-particles; split longer or larger sketches.");
        var material=EnsureComponent(ref Material);material.Enabled=true;material.persistent=true;
        material.BlendMode=BlendMode.Additive;material.ZWrite=ZWrite.Off;material.Sidedness=Sidedness.Double;
        // The installed native point shader lacks the sRGB-interpolation variant.
        // Frame colors are decoded explicitly; use its available linear variant.
        material.UseVertexColors=true;material.VertexColorInterpolationSpace=Renderite.Shared.ColorProfile.Linear;
        material.UseBillboardGeometry=true;material.UsePerBillboardScale=true;material.UsePerBillboardRotation=true;material.UsePerBillboardUV=true;
        // Native point size is a half extent; our records store full quad width.
        material.BillboardSize=Vector2.one*.5f;material.TintColor=new Color(source.PeakGain,source.PeakGain,source.PeakGain,1).ToColorX_Linear();
        material.Texture=context.GetITexture2D(source.BakedTexture!=null ? source.BakedTexture : source.SourceMaterial.mainTexture);material.TextureScale=Vector2.one;material.RenderQueue=3000;
        // Preserve the thin bubble rim under native mip filtering. Verified against
        // front/oblique captures; default filtering blurs the atlas rim into a box.
        if(source.Kind==OpenBrushParticleMotion.BrushKind.RisingBubbles && material.Texture is StaticTexture2D risingTexture)
            risingTexture.MipMapBias=-1.5f;
        var renderer=EnsureComponent(ref Renderer);renderer.Enabled=source.enabled;renderer.persistent=true;renderer.ShadowCastMode=Renderite.Shared.ShadowCastMode.Off;
        while(renderer.Materials.Count>1)renderer.Materials.RemoveAt(renderer.Materials.Count-1);
        if(renderer.Materials.Count==0)renderer.Materials.Add(material);else renderer.Materials[0]=material;
        var sequence=EnsureComponent(ref Sequence);sequence.Enabled=true;sequence.persistent=true;sequence.Target=renderer.Mesh_Element.Member;
        if(source.Frames==null || source.Frames.Length!=source.FrameCount)throw new InvalidOperationException("Particle mesh frames missing");
        while(sequence.Assets.Count>source.FrameCount)sequence.Assets.RemoveAt(sequence.Assets.Count-1);
        for(int f=0;f<source.Frames.Length;f++) {
            var mesh=context.GetMesh(source.Frames[f]);
            if(sequence.Assets.Count<=f)sequence.Assets.Add(mesh);else sequence.Assets[f]=mesh;
        }
        renderer.Mesh=context.GetMesh(source.Frames[0]);
        if(source.Timeline==null) {
            var clock=EnsureComponent(ref Clock);clock.Enabled=true;clock.persistent=true;
            clock.Scale=source.StaticFrames/source.Period;clock.Repeat=source.FrameCount;clock.PingPong=false;clock.Target=sequence.Index_Element.Member;
        } else {
            if(Clock!=null){Clock.Binding.Enabled=false;Clock.Binding.Target=null;}
            var index=EnsureComponent(ref Index);OpenBrushTimelineLink.Index(index,Enumerable.Range(0,source.FrameCount).Select(source.Seconds).ToArray(),sequence.Index_Element.Member);
            OpenBrushTimelineLink.Position(EnsureComponent(ref Position),source.Timeline,index.Progress_Element.Member);
        }
    }
    protected override void Cleanup() {

        if(Position!=null)DestroyImmediate(Position);if(Index!=null)DestroyImmediate(Index);if(Clock!=null)DestroyImmediate(Clock);
        if(Sequence!=null)DestroyImmediate(Sequence);if(Renderer!=null)DestroyImmediate(Renderer);if(Material!=null)DestroyImmediate(Material);
    }
}






