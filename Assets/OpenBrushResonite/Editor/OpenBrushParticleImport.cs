using System;
using System.Linq;
using UnityEngine;

public static class OpenBrushParticleImport {
    public const string RisingGuid="a8147ce1-005e-abe4-88e8-09a1eaadcc89";
    public const string DotsGuid="6a1cf9f9-032c-45ec-9b1d-a6680bee30f7";
    public const string BubblesGuid="89d104cd-d012-426b-b5b3-bbaee63ac43c";
    public const string SnowGuid="d902ed8b-d0d1-476c-a8de-878a79e3a34c";
    public const string StarsGuid="0eb4db27-3f82-408d-b5a1-19ebd7d5b711";
    [Serializable] public class Data {public int schemaVersion;public string coordinateSystem;public Part[] parts;}
    [Serializable] public class Part {public string nodeName,brushGuid;public OpenBrushParticleMotion.Particle[] particles;}
    // Completed Snow uses static phase groups. Slow Bubbles loops may reduce cadence. Keep
    // sparkle sampling, all particles, source speed and loop periods intact.
    public static int[] CompletedFramePlan(Part[] parts,long limit=1000000) {
        if(limit<1 || parts==null || parts.Any(p=>p==null || p.particles==null))throw new InvalidOperationException("Invalid particle budget input");
        var defaults=parts.Select(p=>p.brushGuid==DotsGuid ? 2 : p.brushGuid==SnowGuid ? 1 : (p.brushGuid==BubblesGuid || p.brushGuid==RisingGuid) ? 1800 : OpenBrushParticleMotion.CycleFrames).ToArray();
        var minimum=parts.Select((p,i)=>p.brushGuid==SnowGuid ? 1 : (p.brushGuid==BubblesGuid || p.brushGuid==RisingGuid) ? 600 : defaults[i]).ToArray();
        Func<int[],double> cost=values=>parts.Select((p,i)=>(double)p.particles.Length*values[i]).Sum();
        if(cost(defaults)<=limit)return defaults;
        if(cost(minimum)>limit)throw new InvalidOperationException("Particle bake exceeds 1,000,000 frame-particles even at 10 fps; split the sketch.");
        double low=0,high=1;
        for(int step=0;step<40;step++) {
            double scale=(low+high)*.5;
            var candidate=defaults.Select((frames,i)=>Math.Max(minimum[i],(int)Math.Floor(frames*scale))).ToArray();
            if(cost(candidate)<=limit)low=scale;else high=scale;
        }
        return defaults.Select((frames,i)=>Math.Max(minimum[i],(int)Math.Floor(frames*low))).ToArray();
    }
    public static void Prepare(GameObject root,Data data,OpenBrushGrowthImport.Data timing=null) {
        if(data==null || data.schemaVersion!=1 || data.coordinateSystem!="UnityInvertZ" || data.parts==null || data.parts.Length==0)
            throw new InvalidOperationException("Expected original particle sidecar");
        if(data.parts.Select(p=>p.nodeName).Distinct().Count()!=data.parts.Length)throw new InvalidOperationException("Duplicate particle source nodes");
        bool completed=timing!=null && timing.completed;
        var completedPlan=completed ? CompletedFramePlan(data.parts) : null;
        if(completed)timing=null;
        var timeline=timing==null ? null : root.GetComponent<OpenBrushTimeline>();
        double frames=timing==null ? 0 : Math.Ceiling(timing.duration*60.0)+1;
        if(!completed && (data.parts.Any(p=>p.particles==null) || data.parts.Sum(p=>(double)p.particles.Length*(p.brushGuid==DotsGuid ? 2*(timing==null ? 1 : p.particles.Select(x=>x.birth).Distinct().Count()+2) : timing==null ? ((p.brushGuid==BubblesGuid || p.brushGuid==RisingGuid) ? 1800 : p.brushGuid==SnowGuid ? 750 : OpenBrushParticleMotion.CycleFrames) : frames))>1000000))throw new InvalidOperationException("Particle bake exceeds 1,000,000 frame-particles; split the sketch.");
        var all=root.GetComponentsInChildren<Transform>(true);
        foreach(var part in data.parts) {
            if((part.brushGuid!=RisingGuid && part.brushGuid!=StarsGuid && part.brushGuid!=SnowGuid && part.brushGuid!=BubblesGuid && part.brushGuid!=DotsGuid) || part.particles==null || part.particles.Length==0)throw new InvalidOperationException("Unsupported particle data");
            var matches=all.Where(t=>t.name==part.nodeName).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException("Missing particle source node: "+part.nodeName);
            var parent=matches[0];if(parent.GetComponentInChildren<OpenBrushParticleMotion>(true)!=null)throw new InvalidOperationException("Particles already prepared");
            var renderers=parent.GetComponentsInChildren<MeshRenderer>(true);
            if(renderers.Length==0 || renderers.Any(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")!=part.brushGuid))throw new InvalidOperationException("Particle material mismatch");
            foreach(var p in part.particles) {
                if(!float.IsFinite(p.size) || p.size<=0 || !float.IsFinite(p.birth) || p.sourceEnd<p.sourceStart)
                    throw new InvalidOperationException("Invalid particle record");
                if(timing!=null) {
                    var strokes=timing.strokes.Where(s=>s.name==part.nodeName && Mathf.Abs(s.sourceStart-p.sourceStart)<.0001f && Mathf.Abs(s.sourceEnd-p.sourceEnd)<.0001f).ToArray();
                    if(strokes.Length!=1)throw new InvalidOperationException("Ambiguous particle stroke timing");
                    var stroke=strokes[0];p.playBirth=Mathf.Lerp(stroke.playStart,stroke.playEnd,Mathf.InverseLerp(stroke.sourceStart,stroke.sourceEnd,p.birth));
                }
            }
        }
        if(completed)OpenBrushSnowImport.Prepare(root,data.parts.Where(p=>p.brushGuid==SnowGuid).ToArray());
        foreach(var part in data.parts) {
            if(completed && part.brushGuid==SnowGuid)continue;
            var parent=all.Single(t=>t.name==part.nodeName);var renderers=parent.GetComponentsInChildren<MeshRenderer>(true);
            if(part.brushGuid==DotsGuid) {
                OpenBrushDotsAppearance.Prepare(parent,part.particles,renderers[0].sharedMaterial,timeline);
                foreach(var renderer in renderers)renderer.enabled=false;
                continue;
            }
            var child=new GameObject(OpenBrushProfiles.Find(part.brushGuid).Name+" - Original Particle Motion");child.transform.SetParent(parent,false);
            var motion=child.AddComponent<OpenBrushParticleMotion>();motion.Particles=part.particles;motion.SourceMaterial=renderers[0].sharedMaterial;
            motion.Kind=part.brushGuid==RisingGuid ? OpenBrushParticleMotion.BrushKind.RisingBubbles : part.brushGuid==BubblesGuid ? OpenBrushParticleMotion.BrushKind.Bubbles : part.brushGuid==SnowGuid ? OpenBrushParticleMotion.BrushKind.Snow : OpenBrushParticleMotion.BrushKind.Stars;
            if(completed) {
                int planned=completedPlan[Array.IndexOf(data.parts,part)];
                if(planned!=motion.StaticFrames) {
                    string adjustment=OpenBrushProfiles.Find(part.brushGuid).Name+" particle cadence reduced from "+motion.StaticFrames+" to "+planned+" frames per "+motion.Period+" second loop to fit memory budget; all particles, motion speed and loop duration retained. Motion may be less smooth.";
                    motion.StaticFrameCountOverride=planned;
                    if(!OpenBrushAtlasBudget.Adjustments.Contains(adjustment)){OpenBrushAtlasBudget.Adjustments.Add(adjustment);Debug.LogWarning(adjustment);}
                }
            }
            motion.PeakGain=1;
            if(motion.Kind==OpenBrushParticleMotion.BrushKind.Stars) {
                var texture=motion.SourceMaterial.mainTexture as Texture2D;
                if(texture==null || !texture.isReadable)throw new InvalidOperationException("Stars requires readable original sprite RGBA");
                // Source Stars is authored for gamma rendering. Keep sprite
                // samples raw; native Additive applies the original alpha.
                var raw=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,texture.mipmapCount>1,true){name="Stars_RawSprite_HDR",filterMode=texture.filterMode,wrapModeU=texture.wrapModeU,wrapModeV=texture.wrapModeV};
                raw.SetPixels(texture.GetPixels());raw.Apply(texture.mipmapCount>1,false);motion.BakedTexture=raw;
            }
            if(motion.Kind==OpenBrushParticleMotion.BrushKind.Bubbles)OpenBrushBubbleAppearance.Prepare(motion);
            if(motion.Kind==OpenBrushParticleMotion.BrushKind.RisingBubbles)OpenBrushRisingAppearance.Prepare(motion);
            motion.Timeline=timeline;motion.Bake();
            foreach(var renderer in renderers)renderer.enabled=false;
        }
    }
}



