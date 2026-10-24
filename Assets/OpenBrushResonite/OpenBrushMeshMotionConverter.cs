using System;
using UnityEngine;
using FrooxEngine;
public class OpenBrushMeshMotionConverter : ResoniteComponentConverter<OpenBrushMeshMotion>
{
    public ResRef<FrooxEngine.MeshRenderer> Renderer;
    public ResRef<OpenBrushMeshMultiplexer> Sequence;
    public ResRef<OpenBrushTextureMultiplexer> Glints;
    public ResRef<TimeIntDriver> Clock, GlintClock;
    public ResRef<OpenBrushIndexGradient> GrowthIndex,GlintGrowthIndex;
    public ResRef<PlaybackPositionDriver> GrowthPosition,GlintGrowthPosition;
    protected override void UpdateConversion(OpenBrushMeshMotion source, IConversionContext context)
    {
        if(source.Frames==null || source.Frames.Length<2 || source.GlintFrames.Length!=source.Frames.Length)
            throw new InvalidOperationException("Incomplete Disco sequence");
        var material=context.GetMaterial(source.SourceMaterial);
        var specular=material as PBS_Specular;
        var legacy=material as PBS_VertexColorMetallic;
        if(specular==null && legacy==null)throw new InvalidOperationException("Disco requires its audited material");
        if(specular!=null) {
            specular.EmissiveColor=Color.white.ToColorX_Linear();
            specular.TextureScale=Vector2.one;specular.TextureOffset=Vector2.zero;
        } else {
            // Preserve saved pre-specular scenes until they are prepared again.
            legacy.EmissiveColor=new Color(1.5f,1.5f,1.5f,1).ToColorX_Linear();
            legacy.TextureScale=Vector2.one;legacy.TextureOffset=Vector2.zero;
        }
        var renderer=EnsureComponent(ref Renderer);renderer.Enabled=source.enabled;renderer.persistent=true;
        renderer.ShadowCastMode=Renderite.Shared.ShadowCastMode.On;
        while(renderer.Materials.Count>1)renderer.Materials.RemoveAt(renderer.Materials.Count-1);
        if(renderer.Materials.Count==0)renderer.Materials.Add(material);else renderer.Materials[0]=material;
        renderer.Mesh=context.GetMesh(source.Frames[0]);
        var sequence=EnsureComponent(ref Sequence);sequence.Enabled=source.enabled;sequence.persistent=true;
        sequence.Target=renderer.Mesh_Element.Member;
        var glints=EnsureComponent(ref Glints);glints.Enabled=source.enabled;glints.persistent=true;
        glints.Target=specular!=null ? specular.EmissiveMap_Element.Member : legacy.EmissiveMap_Element.Member;
        while(sequence.Assets.Count>source.Frames.Length)sequence.Assets.RemoveAt(sequence.Assets.Count-1);
        while(glints.Assets.Count>source.Frames.Length)glints.Assets.RemoveAt(glints.Assets.Count-1);
        for(int i=0;i<source.Frames.Length;i++) {
            var mesh=context.GetMesh(source.Frames[i]);var tex=context.GetITexture2D(source.GlintFrames[i]);
            if(sequence.Assets.Count<=i)sequence.Assets.Add(mesh);else sequence.Assets[i]=mesh;
            if(glints.Assets.Count<=i)glints.Assets.Add(tex);else glints.Assets[i]=tex;
        }
        var clock=EnsureComponent(ref Clock);Configure(clock,source);clock.Target=sequence.Index_Element.Member;
        var glintClock=EnsureComponent(ref GlintClock);Configure(glintClock,source);glintClock.Target=glints.Index_Element.Member;
        if(source.Timeline!=null) {
            clock.Enabled=glintClock.Enabled=false;clock.Target=glintClock.Target=null;
            var index=EnsureComponent(ref GrowthIndex);OpenBrushTimelineLink.Index(index,source.FrameTimes,sequence.Index_Element.Member);
            var glintIndex=EnsureComponent(ref GlintGrowthIndex);OpenBrushTimelineLink.Index(glintIndex,source.FrameTimes,glints.Index_Element.Member);
            OpenBrushTimelineLink.Position(EnsureComponent(ref GrowthPosition),source.Timeline,index.Progress_Element.Member);
            OpenBrushTimelineLink.Position(EnsureComponent(ref GlintGrowthPosition),source.Timeline,glintIndex.Progress_Element.Member);
        }
    }
    static void Configure(TimeIntDriver clock,OpenBrushMeshMotion source) {
        clock.Enabled=source.enabled;clock.persistent=true;clock.Scale=source.FramesPerSecond;clock.Repeat=source.Frames.Length;clock.PingPong=false;
    }
    protected override void Cleanup() {
        if(GrowthIndex!=null)DestroyImmediate(GrowthIndex);if(GlintGrowthIndex!=null)DestroyImmediate(GlintGrowthIndex);if(GrowthPosition!=null)DestroyImmediate(GrowthPosition);if(GlintGrowthPosition!=null)DestroyImmediate(GlintGrowthPosition);
        if(Clock!=null)DestroyImmediate(Clock);if(GlintClock!=null)DestroyImmediate(GlintClock);
        if(Sequence!=null)DestroyImmediate(Sequence);if(Glints!=null)DestroyImmediate(Glints);if(Renderer!=null)DestroyImmediate(Renderer);
    }
}
