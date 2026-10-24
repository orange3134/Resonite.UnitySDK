using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushGrowthChecks {
    static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("Growth check: "+message);}
    public static void Run(){try {
        using(var data=new AutoGltfFileParser(Path.GetFullPath(".local/BrushTest-growth-v5-compact.glb")).Parse())using(var importer=new ImporterContext(data)) {
            var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;root.transform.rotation=Quaternion.Euler(0,180,0);root.transform.localScale=Vector3.one*1.2784332f;
            var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(".local/BrushTest-growth-v5-compact.materials.json"));
            OpenBrushMaterialImport.Prepare(root,manifest,false);OpenBrushMotionImport.Prepare(root);OpenBrushProceduralImport.Prepare(root);
            var raw=JsonUtility.FromJson<OpenBrushDiscoImport.Data>(File.ReadAllText(".local/BrushTest-disco-growth.json"));OpenBrushDiscoImport.Prepare(root,raw);
            var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(".local/BrushTest-growth-timeline.json"));
            var timeline=OpenBrushGrowthImport.Prepare(root,timing,raw);var parts=root.GetComponentsInChildren<OpenBrushGrowthPart>();
            Check(parts.Length==375 && parts.Min(x=>x.Start)==1,"all 375 growth parts retain the 1-second empty lead-in");
            Check(Math.Abs(timeline.DurationClip.length-timing.duration)<.001,"native timeline length matches original GLB");
            Check(parts.All(x=>x.End<=timeline.Duration-2.999f),"completed drawing holds for three seconds");
            var meshMotion=root.GetComponentInChildren<OpenBrushMeshMotion>();
            Check(meshMotion.Timeline==timeline && meshMotion.FrameTimes.Length==meshMotion.Frames.Length,"Disco frames use the same timeline");
            var first=meshMotion.Frames[0];float area=0;var pos=first.vertices;var tri=first.triangles;
            for(int i=0;i<tri.Length;i+=3)area+=Vector3.Cross(pos[tri[i+1]]-pos[tri[i]],pos[tri[i+2]]-pos[tri[i]]).magnitude;
            Check(area<1e-7,"Disco is invisible before its drawing starts");
            Check(meshMotion.Frames.Last().vertexCount==raw.parts[0].triangles.Length,"Disco completes every original face");
            var stroke=timing.strokes.Single(s=>s.name==raw.parts[0].nodeName);
            var middle=Array.FindIndex(meshMotion.FrameTimes,t=>t>=(stroke.playStart+stroke.playEnd)/2);
            Check(meshMotion.Frames[middle].vertexCount>3 && meshMotion.Frames[middle].vertexCount<meshMotion.Frames.Last().vertexCount,"Disco reveals a partial stroke, not an entire stroke pop-in");
            var embers=root.GetComponentInChildren<OpenBrushEmbersMotion>();Check(embers.EmissionFrames.Length==24,"Embers grows over all 24 source parts");
            Check(embers.EmissionFrames[0].vertexCount<embers.EmissionFrames.Last().vertexCount && embers.EmissionFrames.Last().vertexCount==embers.EmissionMesh.vertexCount,"Embers emission excludes future geometry and finishes with the original surface");
            Check(embers.FrameTimes.Zip(embers.FrameTimes.Skip(1),(a,b)=>b>a).All(x=>x),"Embers progress times strictly increase");
            Check(root.GetComponentsInChildren<OpenBrushPlaybackButton>().Length==3,"restart, pause/resume and clear buttons prepared");
            var context=new SceneConverter();context.EnsureInitialized(ScriptableObject.CreateInstance<ResoniteLinkWindow>());var sample=parts[0];var gradient=new OpenBrushScaleGradient{DrivenTransform=sample.transform};var members=gradient.CollectMembers(context);
            Check(!string.IsNullOrEmpty(((ResoniteLink.Reference)members["Target"]).TargetID),"scale driver resolves the SDK's actual native Slot field ID");
            Check(ComponentConverterRepository.TryGetConverter(typeof(OpenBrushTimeline))?.Type==typeof(OpenBrushTimelineConverter),"SDK discovers playback converter");
            Check(ComponentConverterRepository.TryGetConverter(typeof(OpenBrushGrowthPart))?.Type==typeof(OpenBrushGrowthPartConverter),"SDK discovers segment conversion");
            UnityEngine.Object.DestroyImmediate(root);
        }
        Debug.Log("OPENBRUSH_GROWTH_CHECKS_PASSED");EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
