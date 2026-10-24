using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class OpenBrushGrowthImport {
    [Serializable] public class Data {public float duration;public bool completed;public Segment[] segments;public Stroke[] strokes;}
    [Serializable] public class Segment {public string name;public float start,end;}
    [Serializable] public class Stroke {public string name;public float sourceStart,sourceEnd,playStart,playEnd;}
    // Completed drawings retain a short clock only for finite brush effects.
    // No growth drivers, Disco reveal bake, Embers emission gating, or buttons.
    public static OpenBrushTimeline PrepareCompleted(GameObject root,Data data) {
        if(data==null || !data.completed || !float.IsFinite(data.duration) || data.duration<.1f || data.duration>60 || root.GetComponent<OpenBrushTimeline>()!=null)
            throw new InvalidOperationException("Invalid completed-drawing effect timeline");
        var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=data.duration;
        timeline.AutoPlay=true;timeline.DurationClip=CreateDurationClip(data.duration);
        return timeline;
    }
    public static float Birth(Data data,Stroke stroke,float sourceTime) => data.completed ? 0 : Mathf.Lerp(stroke.playStart,stroke.playEnd,Mathf.InverseLerp(stroke.sourceStart,stroke.sourceEnd,sourceTime));
    static AudioClip CreateDurationClip(float duration) {
        // File import uses the SDK's supported audio-file path and preserves a reusable asset.
        const int rate=8000;int samples=Mathf.CeilToInt(duration*rate);
        const string path="Assets/OpenBrushGenerated/BrushGrowthTimeline.wav";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using(var writer=new BinaryWriter(File.Create(path))) {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
            writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);
            writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);
            for(int i=0;i<samples;i++)writer.Write((short)0);
        }
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<AudioClip>(path) ?? throw new InvalidOperationException("Timeline audio import failed");
    }
    public static OpenBrushTimeline Prepare(GameObject root,Data data,OpenBrushDiscoImport.Data disco=null) {
        if(root.GetComponent<OpenBrushTimeline>()!=null)throw new InvalidOperationException("Growth already prepared");
        if(data==null || float.IsNaN(data.duration) || float.IsInfinity(data.duration) || data.duration<=0 || data.segments==null || data.segments.Length==0)
            throw new InvalidOperationException("Invalid timeline");
        var all=root.GetComponentsInChildren<Transform>(true);
        if(data.segments.Select(s=>s.name).Distinct().Count()!=data.segments.Length)
            throw new InvalidOperationException("Duplicate growth targets");
        foreach(var segment in data.segments) {
            if(float.IsNaN(segment.start) || float.IsNaN(segment.end) || segment.start<0 || segment.end<=segment.start || segment.end>data.duration)
                throw new InvalidOperationException("Invalid segment times");
            var matches=all.Where(t=>t.name==segment.name).ToArray();
            if(matches.Length!=1 || matches[0].GetComponent<MeshRenderer>()==null)
                throw new InvalidOperationException("Missing or ambiguous growth renderer: "+segment.name);
        }
        var motions=root.GetComponentsInChildren<OpenBrushMeshMotion>();
        if(motions.Length>0) {
            if(disco?.parts==null || disco.parts.Length!=motions.Length || data.strokes==null)
                throw new InvalidOperationException("Missing Disco raw-motion parts or stroke timing");
            foreach(var motion in motions) {
                var raw=disco.parts.Where(p=>p.nodeName==motion.transform.parent.name).ToArray();
                if(raw.Length!=1)throw new InvalidOperationException("Missing or ambiguous Disco motion parent");
                OpenBrushDiscoGrowth.MatchStrokes(raw[0],data.strokes.Where(s=>s.name==raw[0].nodeName).ToArray(),data.duration);
            }
        }
        var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=data.duration;
        timeline.DurationClip=CreateDurationClip(data.duration);
        foreach(var segment in data.segments) {
            var t=all.Single(x=>x.name==segment.name);
            var part=t.gameObject.AddComponent<OpenBrushGrowthPart>();part.Timeline=timeline;part.Start=segment.start;part.End=segment.end;
        }
        foreach(var motion in motions) {
            var raw=disco.parts.Single(p=>p.nodeName==motion.transform.parent.name);
            var strokes=data.strokes.Where(s=>s.name==raw.nodeName).ToArray();
            OpenBrushDiscoGrowth.Bake(raw,motion.transform.parent,strokes,data.duration,out var frames,out var glints,out var times);
            foreach(var old in motion.Frames)UnityEngine.Object.DestroyImmediate(old);
            foreach(var old in motion.GlintFrames)UnityEngine.Object.DestroyImmediate(old);
            motion.Frames=frames;motion.GlintFrames=glints;motion.FrameTimes=times;motion.Timeline=timeline;
        }
        var embers=root.GetComponentInChildren<OpenBrushEmbersMotion>();
        if(embers!=null) {
        embers.Timeline=timeline;
        var parts=root.GetComponentsInChildren<OpenBrushGrowthPart>().Where(p=>p.GetComponent<MeshRenderer>()?.sharedMaterial.GetTag("OpenBrushGuid",false,"")=="02ffb866-7fb2-4d15-b761-1012cefb1360").OrderBy(p=>p.End).ToArray();
        if(parts.Length==0)throw new InvalidOperationException("Embers is missing growth geometry");
        embers.EmissionFrames=new Mesh[parts.Length];embers.FrameTimes=parts.Select(p=>p.End).ToArray();embers.Start=parts[0].End;
        for(int i=0;i<parts.Length;i++) {
            var mesh=new Mesh{name="EmbersGrowth_"+i,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};
            mesh.CombineMeshes(parts.Take(i+1).Select(p=>new CombineInstance{mesh=p.GetComponent<MeshFilter>().sharedMesh,transform=root.transform.worldToLocalMatrix*p.transform.localToWorldMatrix}).ToArray(),true,true);
            var colors=mesh.colors;for(int c=0;c<colors.Length;c++)colors[c].a=1;mesh.colors=colors;embers.EmissionFrames[i]=mesh;
        }
        }
        var panel=new GameObject("Playback Controls");panel.transform.SetParent(root.transform,false);panel.transform.localPosition=new Vector3(0,-.65f,-.5f);panel.transform.localRotation=Quaternion.Euler(0,180,0);
        var labels=new[]{"Restart","Play / Pause","Clear"};var actions=new[]{FrooxEngine.PlaybackAction.Play,FrooxEngine.PlaybackAction.ToggleResumePause,FrooxEngine.PlaybackAction.Stop};
        for(int i=0;i<3;i++) {
            var button=GameObject.CreatePrimitive(PrimitiveType.Cube);button.name=labels[i];button.transform.SetParent(panel.transform,false);button.transform.localPosition=new Vector3((i-1)*.34f,0,0);button.transform.localScale=new Vector3(.31f,.12f,.035f);
            var marker=button.AddComponent<OpenBrushPlaybackButton>();marker.Timeline=timeline;marker.Action=actions[i];
            var text=new GameObject(labels[i]+" Label");text.transform.SetParent(panel.transform,false);text.transform.localPosition=new Vector3((i-1)*.34f,0,-.022f);var label=text.AddComponent<TextMesh>();label.text=labels[i];label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=16;label.characterSize=.03f;label.color=Color.black;
        }
        return timeline;
    }
}

