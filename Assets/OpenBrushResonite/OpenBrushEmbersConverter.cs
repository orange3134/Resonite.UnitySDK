using UnityEngine;
using FrooxEngine.PhotonDust;

public class OpenBrushEmbersConverter : ResoniteComponentConverter<OpenBrushEmbersMotion>
{
    public ResRef<FrooxEngine.PhotonDust.ParticleSystem> System;
    public ResRef<ParticleStyle> Style;
    public ResRef<MeshEmitter> Emitter;
    public ResRef<BillboardParticleRenderer> Renderer;
    public ResRef<FrooxEngine.UnlitMaterial> Material;
    public ResRef<LifetimeRangeInitializer> Lifetime;
    public ResRef<SizeRangeInitializer> Size;
    public ResRef<SpeedRangeInitializer> Speed;

    public ResRef<OpenBrushMeshMultiplexer> GrowthMeshes;
    public ResRef<OpenBrushIndexGradient> GrowthIndex;
    public ResRef<OpenBrushFloatGradient> GrowthRate;
    public ResRef<OpenBrushBoolGradient> GrowthVisibility;
    public ResRef<FrooxEngine.PlaybackPositionDriver> IndexPosition,RatePosition,VisibilityPosition;
    public ResRef<PositionSimulatorModule> Position;
    public ResRef<ColorOverLifetimeTexture> Fade;

    T Module<T>(ref ResRef<T> field) where T : FrooxEngine.Component, IParticleSystemSubsystem, new()
    {
        var value = EnsureComponent(ref field, m => Style.Binding.Modules.Add(m));
        value.Enabled = true; value.persistent = true;
        return value;
    }
    protected override void UpdateConversion(OpenBrushEmbersMotion source, IConversionContext context)
    {
        var system = EnsureComponent(ref System);
        var style = EnsureComponent(ref Style);
        system.Enabled = source.enabled; system.persistent = true;
        system.MaxParticleCount = Mathf.CeilToInt(source.Rate * source.Lifetime * 2);
        system.Style = style;
        system.SimulationSpace.LocalSpace = source.transform.GetSlot();
        style.Enabled = true; style.persistent = true;
        style.UseSystemLocalScale = true;
        style.UseSystemLocalRotation = true;
        style.ParticleScaleMode = PhotonDust.ScaleMultiplierMode.Continuous;

        var lifetime = Module(ref Lifetime);
        lifetime.MinValue = source.Lifetime * .85f; lifetime.MaxValue = source.Lifetime * 1.15f;
        var size = Module(ref Size);
        size.MinValue = Vector3.one * source.Size * .65f;
        size.MaxValue = Vector3.one * source.Size * 1.3f;


        var speed = Module(ref Speed);
        speed.MinValue = source.Travel.magnitude / source.Lifetime * .85f;
        speed.MaxValue = source.Travel.magnitude / source.Lifetime * 1.15f;
        Module(ref Position).Collisions = false;
        var lifetimeTexture = context.GetTexture2D(source.LifetimeTexture);
        if (lifetimeTexture is FrooxEngine.StaticTexture2D readableTexture) {
            readableTexture.Readable = true;
            readableTexture.DirectLoad = true;
        }
        Module(ref Fade).Texture = lifetimeTexture;

        var material = EnsureComponent(ref Material);
        material.Enabled = true; material.persistent = true;
        material.BlendMode = FrooxEngine.BlendMode.Additive;
        material.Sidedness = FrooxEngine.Sidedness.Double;
        material.ZWrite = FrooxEngine.ZWrite.Off;
        material.UseVertexColors = true;
        material.VertexColorInterpolationSpace = Renderite.Shared.ColorProfile.Linear;
        // PhotonDust BillboardParticleRenderer already expands and faces the quads.
        // Unlit billboard geometry is for point meshes; applying it again misreads
        // the particle vertex data as billboard size/rotation and creates huge cards.
        material.UseBillboardGeometry = false;
        material.UsePerBillboardScale = false;
        material.UsePerBillboardRotation = false;
        material.BillboardSize = Vector2.one;
        material.TintColor = new UnityEngine.Color(2, 2, 2, 1).ToColorX_Linear();
        material.Texture = context.GetITexture2D(source.Sprite);
        material.TextureScale = Vector2.one;
        material.RenderQueue = 3000;
        var renderer = EnsureComponent(ref Renderer);
        renderer.Enabled = true; renderer.persistent = true;
        renderer.Material = material;
        renderer.Alignment = Renderite.Shared.BillboardAlignment.Facing;
        renderer.MinBillboardScreenSize = 0;
        renderer.MaxBillboardScreenSize = 1;
        style.Renderer = renderer;

        var emitter = EnsureComponent(ref Emitter);
        emitter.Enabled = source.enabled; emitter.persistent = true;
        emitter.System = system;
        emitter.Mesh = context.GetMesh(source.EmissionMesh);
        // The emitter samples the mesh on the CPU, independently of rendering.
        if (emitter.Mesh is FrooxEngine.StaticMesh readableMesh) readableMesh.Readable = true;
        emitter.EmitFrom = PhotonDust.MeshEmissionSource.Faces;
        emitter.UniformDistribution = true;
        emitter.UseVertexColors = true;
        emitter.UVScale = Vector2.one;
        emitter.DirectionMode = PhotonDust.MeshEmitterDirection.LocalSpace;
        emitter.Direction = source.Travel.normalized;
        emitter.RandomDirectionWeight = .08f;
        emitter.Rate = source.Rate;
        emitter.BurstOnStart = false;
        if(source.Timeline!=null) {
            var sequence=EnsureComponent(ref GrowthMeshes);sequence.Enabled=true;sequence.persistent=true;sequence.Target=emitter.Mesh_Element.Member;
            while(sequence.Assets.Count>source.EmissionFrames.Length)sequence.Assets.RemoveAt(sequence.Assets.Count-1);
            for(int i=0;i<source.EmissionFrames.Length;i++) {
                var mesh=context.GetMesh(source.EmissionFrames[i]);if(mesh is FrooxEngine.StaticMesh readable)readable.Readable=true;
                if(sequence.Assets.Count<=i)sequence.Assets.Add(mesh);else sequence.Assets[i]=mesh;
            }
            var index=EnsureComponent(ref GrowthIndex);OpenBrushTimelineLink.Index(index,source.FrameTimes,sequence.Index_Element.Member);
            OpenBrushTimelineLink.Position(EnsureComponent(ref IndexPosition),source.Timeline,index.Progress_Element.Member);
            var rate=EnsureComponent(ref GrowthRate);rate.Enabled=true;rate.persistent=true;rate.Interpolate=false;rate.Target=emitter.Rate_Element.Member;rate.Points.Clear();
            var zero=rate.Points.Add();zero.Position=0;zero.Value=0;var on=rate.Points.Add();on.Position=source.Start;on.Value=source.Rate;
            OpenBrushTimelineLink.Position(EnsureComponent(ref RatePosition),source.Timeline,rate.Progress_Element.Member);
            var visible=EnsureComponent(ref GrowthVisibility);visible.Enabled=true;visible.persistent=true;visible.Interpolate=false;visible.Target=renderer.Enabled_Element.Member;visible.Points.Clear();
            var off=visible.Points.Add();off.Position=0;off.Value=false;var show=visible.Points.Add();show.Position=source.Start;show.Value=true;
            OpenBrushTimelineLink.Position(EnsureComponent(ref VisibilityPosition),source.Timeline,visible.Progress_Element.Member);
        }
    }
    protected override void Cleanup()
    {
        if(GrowthMeshes!=null)DestroyImmediate(GrowthMeshes);if(GrowthIndex!=null)DestroyImmediate(GrowthIndex);if(GrowthRate!=null)DestroyImmediate(GrowthRate);if(GrowthVisibility!=null)DestroyImmediate(GrowthVisibility);
        if(IndexPosition!=null)DestroyImmediate(IndexPosition);if(RatePosition!=null)DestroyImmediate(RatePosition);if(VisibilityPosition!=null)DestroyImmediate(VisibilityPosition);
        if (System != null) DestroyImmediate(System);
        if (Style != null) DestroyImmediate(Style);
        if (Emitter != null) DestroyImmediate(Emitter);
        if (Renderer != null) DestroyImmediate(Renderer);
        if (Material != null) DestroyImmediate(Material);
        if (Lifetime != null) DestroyImmediate(Lifetime);
        if (Size != null) DestroyImmediate(Size);
        if (Speed != null) DestroyImmediate(Speed);

        if (Position != null) DestroyImmediate(Position);
        if (Fade != null) DestroyImmediate(Fade);
    }
}




