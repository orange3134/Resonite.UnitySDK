using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushDiscoMultiChecks {
    const string Base="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/";
    static void Check(bool ok,string text) {if(!ok)throw new InvalidOperationException(text);Debug.Log("Disco multi: "+text);}
    static float Area(Mesh mesh) {var p=mesh.vertices;var t=mesh.triangles;float sum=0;for(int i=0;i<t.Length;i+=3)sum+=Vector3.Cross(p[t[i+1]]-p[t[i]],p[t[i+2]]-p[t[i]]).magnitude;return sum;}
    public static void Run() {try {
        using(var data=new AutoGltfFileParser(Base+"disco-multi-growth-v1.glb").Parse())using(var importer=new ImporterContext(data)) {
            var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
            var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(Base+"disco-multi-growth-v1.materials.json"));
            OpenBrushMaterialImport.Prepare(root,manifest,false);
            var raw=JsonUtility.FromJson<OpenBrushDiscoImport.Data>(File.ReadAllText(Base+"disco-multi-raw-v2.json"));
            var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(Base+"disco-multi-growth-v1.timeline.json"));
            Check(raw.parts.Length==1 && timing.strokes.Length==5,"five strokes share one source batch");
            var batch=root.GetComponentsInChildren<Transform>().Single(t=>t.name==raw.parts[0].nodeName);
            var clone=UnityEngine.Object.Instantiate(batch.gameObject,batch.parent);clone.name=batch.name+"_second";
            foreach(var r in clone.GetComponentsInChildren<MeshRenderer>())r.name+="_second";
            var second=JsonUtility.FromJson<OpenBrushDiscoImport.Part>(JsonUtility.ToJson(raw.parts[0]));second.nodeName=clone.name;
            raw.parts=new[]{raw.parts[0],second};
            timing.segments=timing.segments.Concat(timing.segments.Select(s=>new OpenBrushGrowthImport.Segment{name=s.name+"_second",start=s.start,end=s.end})).ToArray();
            timing.strokes=timing.strokes.Concat(timing.strokes.Select(s=>new OpenBrushGrowthImport.Stroke{name=second.nodeName,sourceStart=s.sourceStart,sourceEnd=s.sourceEnd,playStart=s.playStart,playEnd=s.playEnd})).ToArray();
            OpenBrushDiscoImport.Prepare(root,raw);OpenBrushDiscoImport.Prepare(root,raw);
            var motions=root.GetComponentsInChildren<OpenBrushMeshMotion>();
            Check(motions.Length==2 && motions[0].SourceMaterial!=motions[1].SourceMaterial,"idempotent batches own independent glint materials");
            foreach(var motion in motions) {
                Check(motion.SourceMaterial.GetTag("OpenBrushDiscoSurface",false,"")=="1","audited source specular path selected");
                var pixels=((Texture2D)motion.SourceMaterial.mainTexture).GetPixels();
                Check(pixels.Select(c=>new Vector3(c.r,c.g,c.b)).Distinct().Count()==5,"specular atlas retains five separate colors");
                Check(pixels.All(c=>Mathf.Abs(c.a-.65f)<1e-6f),"texture alpha retains source smoothness when native PBS uses a specular map");
                Check(pixels.All(c=>c.r<.18f && c.g<.18f && c.b<.19f),"specular uses decoded source color product");
                Check(motion.GlintFrames.SelectMany(t=>t.GetPixels()).All(c=>c.r<=1 && c.g<=1 && c.b<=1),"glints are bounded for native bloom");
            }
            var native=new FrooxEngine.PBS_Specular();OpenBrushMaterialConverter.ConfigureDisco(native);
            Check(native.AlbedoColor.color==Color.black && native.SpecularColor.color.a==.65f,"black diffuse and source smoothness retained");
            var timeline=OpenBrushGrowthImport.Prepare(root,timing,raw);
            Check(root.GetComponentsInChildren<OpenBrushGrowthPart>().Length==160,"all ten strokes keep drawing segments");
            foreach(var motion in motions) {
                var part=raw.parts.Single(p=>p.nodeName==motion.transform.parent.name);
                var strokes=timing.strokes.Where(s=>s.name==part.nodeName).OrderBy(s=>s.playStart).ToArray();
                Check(motion.Timeline==timeline && Area(motion.Frames[0])<1e-8,"empty lead-in uses shared playback");
                Check(motion.Frames.Last().vertexCount==part.triangles.Length,"all source faces survive completed drawing");
                Check(motion.FrameTimes.Zip(motion.FrameTimes.Skip(1),(a,b)=>b>a).All(x=>x),"frame times strictly increase");
                for(int i=0;i<strokes.Length;i++) {
                    int frame=Array.FindIndex(motion.FrameTimes,t=>t>=strokes[i].playEnd+.05f);
                    var colors=motion.Frames[frame].colors.Select(c=>new Vector3(c.r,c.g,c.b)).Distinct().ToArray();
                    Check(colors.Length==i+1,"completed color count "+(i+1)+" excludes future strokes");
                    int middle=Array.FindIndex(motion.FrameTimes,t=>t>=(strokes[i].playStart+strokes[i].playEnd)*.5f);
                    Check(Area(motion.Frames[middle])<Area(motion.Frames[frame]),"stroke "+i+" has partial geometry");
                }
                int a=Array.FindIndex(motion.FrameTimes,t=>t>strokes.Last().playEnd+.1f);
                var p0=motion.Frames[a].vertices;var p1=motion.Frames[a+7].vertices;
                Check(p0.Zip(p1,(p,q)=>(p-q).magnitude).Max()>.001f,"completed surface keeps deforming");
                // Missing identity cannot silently map two strokes to the same interval.
                var groups=part.strokeStarts;part.strokeStarts=null;bool rejected=false;
                try {OpenBrushDiscoGrowth.MatchStrokes(part,strokes,timing.duration);}catch(InvalidOperationException){rejected=true;}
                part.strokeStarts=groups;Check(rejected,"ambiguous legacy multistroke sidecar is rejected");
            }
            UnityEngine.Object.DestroyImmediate(root);
        }
        Debug.Log("OPENBRUSH_DISCO_MULTI_CHECKS_PASSED");EditorApplication.Exit(0);
    }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
