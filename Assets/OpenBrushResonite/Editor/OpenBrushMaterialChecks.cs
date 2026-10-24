using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;

public static class OpenBrushMaterialChecks
{
    static int count;
    static void Check(bool value,string message) {
        if(!value) throw new InvalidOperationException(message);
        count++; Debug.Log("Brush check: "+message);
    }
    public static void Run() {
        try {
            Check(OpenBrushProfiles.All.Select(p=>p.Guid).Distinct().Count()==OpenBrushProfiles.All.Length,"unique explicit GUID registry");
            bool rejected=false;
            try { OpenBrushProfiles.Find("unknown"); } catch(NotSupportedException) { rejected=true; }
            Check(rejected,"unknown GUID rejected");
            var file=Path.GetFullPath(".local/BrushTest-growth-v2-compact.glb");
            var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(".local/BrushTest-growth-v2-compact.materials.json"));
            using(var data=new AutoGltfFileParser(file).Parse())
            using(var importer=new ImporterContext(data)) {
                var instance=importer.Load(); instance.ShowMeshes();
                var root=instance.gameObject;
                var renderers=root.GetComponentsInChildren<Renderer>(true);
                var originalMeshes=root.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh).ToArray();
                var originalTextures=renderers.ToDictionary(r=>r,r=>r.sharedMaterial.mainTexture);
                Check(OpenBrushMaterialImport.Prepare(root,manifest,false)==375,"all 375 parts prepared");
                var preparedMeshes=root.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh).ToArray();
                for(int i=0;i<originalMeshes.Length;i++) {
                    Check(originalMeshes[i].vertices.SequenceEqual(preparedMeshes[i].vertices) && originalMeshes[i].triangles.SequenceEqual(preparedMeshes[i].triangles),"geometry unchanged");
                    if(originalMeshes[i]!=preparedMeshes[i]) {
                        var before=originalMeshes[i].colors;var after=preparedMeshes[i].colors;
                        Check(before.Zip(after,(a,b)=>a.a==b.a && Mathf.Abs(a.linear.r-b.r)<1e-6f && Mathf.Abs(a.linear.g-b.g)<1e-6f && Mathf.Abs(a.linear.b-b.b)<1e-6f).All(x=>x),"RGB decoded once, alpha retained");
                    }
                }
                foreach(var r in renderers)
                    CheckTexture(r,originalTextures[r]);
                var materials=renderers.Select(r=>r.sharedMaterial).Distinct().ToArray();
                Check(materials.Length==14,"one material per brush");
                foreach(var material in materials) {
                    var p=OpenBrushProfiles.Find(material.GetTag("OpenBrushGuid",false,""));
                    Check(MaterialConverterRepository.TryGetConverter(material)==typeof(OpenBrushMaterialConverter),p.Name+" exact SDK routing");
                    if(p.Unlit) {
                        var target=new FrooxEngine.UnlitMaterial();
                        OpenBrushMaterialConverter.ConfigureUnlit(material,p,target);
                        Check(target.UseVertexColors && target.VertexColorInterpolationSpace==Renderite.Shared.ColorProfile.sRGB,p.Name+" Open Brush sRGB vertex colors");
                        Check(target.TintColor.color.r>=(p.Additive ? p.Gain*.99f : .99f),p.Name+" brightness");
                    } else if(p.SpecularModel) {
                        var target=new FrooxEngine.PBS_VertexColorSpecular();
                        OpenBrushMaterialConverter.ConfigureSpecular(material,p,target);
                        Check(target.Culling==FrooxEngine.Culling.Off && target.AlphaHandling==FrooxEngine.AlphaHandling.AlphaClip,p.Name+" double sided cutout");
                        Check(target.SpecularColor.color.r==0 && target.SpecularColor.color.g==0 && target.SpecularColor.color.b==0,p.Name+" black source specular");
                    } else {
                        var target=new FrooxEngine.PBS_VertexColorMetallic();
                        OpenBrushMaterialConverter.ConfigureSurface(material,p,target);
                        Check(target.Culling==FrooxEngine.Culling.Off,p.Name+" double sided");
                        Check(target.VertexColorTarget==(p.EmissiveWire ? FrooxEngine.PBS_VertexColorMetallic.ColorTarget.Emissive : FrooxEngine.PBS_VertexColorMetallic.ColorTarget.Albedo),p.Name+" vertex color target");
                        Check(Mathf.Approximately(target.Smoothness,p.Smoothness),p.Name+" roughness");
                    }
                }
                var duct=materials.Single(m=>m.GetTag("OpenBrushGuid",false,"")=="d0262945-853c-4481-9cbd-88586bed93cb");
                Check(duct.GetTexture("_BumpMap")!=null,"DuctTape normal map retained");
                // Regression: saving a new material must not discard runtime-imported textures.
                Check(OpenBrushMaterialImport.Prepare(root,manifest,true)==375,"persistent material path");
                Check(root.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh).SequenceEqual(preparedMeshes),"repeat preparation preserves decoded meshes");
                foreach(var r in renderers) {
                    if(originalTextures[r]!=null) Check(r.sharedMaterial.mainTexture!=null && EditorUtility.IsPersistent(r.sharedMaterial.mainTexture),"persistent texture retained");
                }
                UnityEngine.Object.DestroyImmediate(root);
            }
            Debug.Log("OPENBRUSH_MATERIAL_CHECKS_PASSED count="+count);
            EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void CheckTexture(Renderer renderer,Texture original) {
        if(renderer.sharedMaterial.mainTexture!=original) throw new InvalidOperationException("Texture changed: "+renderer.name);
    }
}

