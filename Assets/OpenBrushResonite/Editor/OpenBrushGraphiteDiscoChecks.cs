using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushGraphiteDiscoChecks
{
    static void Check(bool ok,string description) {if(!ok)throw new InvalidOperationException(description);Debug.Log("Graphite/Disco check: "+description);}
    public static void CheckAll() {
        using(var data=new AutoGltfFileParser(Path.GetFullPath(".local/BrushTest-growth-v5-compact.glb")).Parse())
        using(var importer=new ImporterContext(data)) {
            var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
            root.transform.rotation=new Quaternion(0,1,0,-4.371139E-08f);root.transform.localScale=Vector3.one*1.2784332f;
            OpenBrushMaterialImport.Prepare(root,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(".local/BrushTest-growth-v5-compact.materials.json")),false);
            OpenBrushProceduralImport.Prepare(root);
            var pencil=root.GetComponentsInChildren<OpenBrushTextureMotion>().Single(m=>m.SourceMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushProceduralImport.WigglyGuid);
            Check(pencil.Frames.Length==6 && pencil.FramesPerSecond==12 && Math.Abs(pencil.TextureScale.x-1.1f)<1e-6,"WigglyGraphite has original six steps, 12fps and U scale");
            var texture=(Texture2D)pencil.SourceMaterial.mainTexture;
            var first=pencil.Frames[0].GetPixels32();var last=pencil.Frames[5].GetPixels32();
            Check(first.Where((p,i)=>!p.Equals(last[i])).Count()>first.Length*.01f,"WigglyGraphite pencil alpha pattern actually changes");
            for(int f=0;f<6;f++)for(int x=3;x<texture.width;x+=173) {
                var actual=pencil.Frames[f].GetPixel(x,texture.height/7);
                var expected=OpenBrushTextureSampling.SampleRaw(texture,(x+.5f)/texture.width+(f+1)*1.1f,(texture.height/7+.5f)/texture.height);
                Check(Math.Abs(actual.a-expected.a)<.005,"pencil sampling preserves animated cutout alpha");
            }
            var raw=JsonUtility.FromJson<OpenBrushDiscoImport.Data>(File.ReadAllText(".local/BrushTest-disco-motion.json"));
            var originalRenderers=root.GetComponentsInChildren<MeshRenderer>();
            var originalMeshes=root.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).ToArray();
            OpenBrushDiscoImport.Prepare(root,raw);OpenBrushDiscoImport.Prepare(root,raw);
            var motion=root.GetComponentsInChildren<OpenBrushMeshMotion>();
            Check(motion.Length==1,"Disco setup is idempotent and raw coordinates match the growth model");
            Check(originalRenderers.Where(r=>!r.enabled).All(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushDiscoImport.Guid),"only Disco's completed static geometry is replaced");
            Check(root.GetComponentsInChildren<MeshFilter>().Select(m=>m.sharedMesh).SequenceEqual(originalMeshes),"growth meshes and LightWire bulb geometry remain unchanged");
            var m=motion[0];Check(m.Frames.Length==48 && m.GlintFrames.Length==48 && Math.Abs(48/m.FramesPerSecond-Mathf.PI/2)<1e-6,"Disco shape and glints share the original pi/2-second period");
            var part=raw.parts[0];float maxDelta=0;
            for(int f=0;f<48;f++) {
                var vertices=m.Frames[f].vertices;
                for(int i=0;i<vertices.Length;i++) {
                    int v=part.triangles[i];float d=(vertices[i]-part.positions[v]).magnitude;
                    if(d>part.uvRadius[v].z*.6001f+1e-6)throw new InvalidOperationException("Disco exceeded source radius");
                    maxDelta=Mathf.Max(maxDelta,(vertices[i]-m.Frames[0].vertices[i]).magnitude);
                }
            }
            Check(maxDelta>.001f,"Disco changes shape within its original 0.6-radius displacement cap");
            Check(m.GlintFrames.SelectMany(t=>t.GetPixels()).Any(c=>c.r>.1f),"Disco has bright facets without unbounded HDR");
            Check(!m.Frames[0].normals.SequenceEqual(m.Frames[12].normals),"flat facet normals follow deformation and change reflections");
            for(int v=0;v<part.positions.Length;v++)
                if((OpenBrushDiscoImport.Deform(part.positions[v],part.normals[v],part.uvRadius[v],0)-OpenBrushDiscoImport.Deform(part.positions[v],part.normals[v],part.uvRadius[v],Mathf.PI/2)).magnitude>1e-6)throw new InvalidOperationException("Disco loop seam");
            Check(true,"Disco deformation closes exactly at the loop boundary");
            ResoniteComponent container;var probe=new GameObject("Mesh sequence contract");probe.AddResoniteComponent<OpenBrushMeshMultiplexer>(out container);
            Check(container.TypeName=="[FrooxEngine]FrooxEngine.AssetMultiplexer<[FrooxEngine]FrooxEngine.Mesh>","mesh multiplexer has the reflected closed native type");
            Check(ComponentConverterRepository.TryGetConverter(typeof(OpenBrushMeshMotion))?.Type==typeof(OpenBrushMeshMotionConverter),"SDK discovers Disco motion converter");
            UnityEngine.Object.DestroyImmediate(probe);UnityEngine.Object.DestroyImmediate(root);
        }
        Debug.Log("OPENBRUSH_GRAPHITE_DISCO_CHECKS_PASSED");
    }
}

