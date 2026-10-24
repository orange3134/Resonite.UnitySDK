using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OpenBrushMaterialImport
{
    [MenuItem("Resonite SDK/Open Brush/Prepare All Audited Brushes on Selected Model")]
    public static void PrepareSelected()
    {
        var root=Selection.activeGameObject;
        if(root==null || EditorUtility.IsPersistent(root)) throw new InvalidOperationException("Select the model root in the scene.");
        var path=EditorUtility.OpenFilePanel("Open Brush materials.json","","json");
        if(string.IsNullOrEmpty(path)) return;
        Prepare(root, JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(path)), true);
    }

    public static int Prepare(GameObject root, OpenBrushSmokeImport.Manifest manifest, bool saveAssets)
    {
        if(manifest==null || manifest.schemaVersion!=1 || manifest.materials==null)
            throw new InvalidOperationException("Expected an Open Brush version 1 material manifest.");
        var shader=Shader.Find("OpenBrush/ExportedBrush");
        if(shader==null) throw new InvalidOperationException("ExportedBrush shader is missing.");
        var targets=new Dictionary<Renderer,OpenBrushSmokeImport.Entry>();
        // Preflight every entry and renderer before making changes. Unknown GUIDs are not guessed.
        foreach(var entry in manifest.materials)
        {
            OpenBrushProfiles.Find(entry?.openBrush?.brushGuid);
            if(entry.openBrush.brushGuid == "8e58ceea-7830-49b4-aba9-6215104ab52a" && entry.openBrush.vertexBake != "mylar-squeeze-v1")
                throw new InvalidOperationException("MylarTube requires source squeeze deformation; regenerate the drawing bundle.");
            if(entry.openBrush.brushGuid == OpenBrushFlowImport.Sparks && entry.openBrush.vertexBake != "sparks-flare-v1")
                throw new InvalidOperationException("Sparks requires source flare displacement; regenerate the drawing bundle.");
            if(entry.openBrush.brushGuid == OpenBrushFlowImport.Rain && entry.openBrush.vertexBake != "rain-bulge-v1")
                throw new InvalidOperationException("Rain requires source radius displacement; regenerate the drawing bundle.");
            if(entry.openBrush.brushGuid == OpenBrushToonImport.Guid && entry.openBrush.vertexBake != "toon-world-y-v1")
                throw new InvalidOperationException("Toon requires source vertex shading preparation; regenerate with prepare-openbrush-model.py or animate-brush-glb.py.");
            if(entry.openBrush.brushGuid == "9871385a-df73-4396-9e33-31e4e4930b27" && entry.openBrush.vertexBake != "inverted-toon-two-surfaces-v1")
                throw new InvalidOperationException("TubeToonInverted requires its black body and colored shell; regenerate the drawing bundle.");
            var temporary=new OpenBrushSmokeImport.Manifest { schemaVersion=1, materials=new[] {
                new OpenBrushSmokeImport.Entry { openBrush=new OpenBrushSmokeImport.Identity { brushGuid=OpenBrushSmokeImport.SmokeGuid },
                    targets=entry.targets, pbrMetallicRoughness=entry.pbrMetallicRoughness } } };
            foreach(var match in OpenBrushSmokeImport.ResolveTargets(root, temporary))
            {
                if(targets.ContainsKey(match.Key)) throw new InvalidOperationException("Repeated material target: "+match.Key.name);
                if(match.Key.sharedMaterials.Length!=1 || match.Key.sharedMaterial==null)
                    throw new InvalidOperationException("Expected one source material on "+match.Key.name);
                var mesh=match.Key.GetComponent<MeshFilter>()?.sharedMesh;
                if(mesh==null || mesh.colors.Length!=mesh.vertexCount)
                    throw new InvalidOperationException("Missing vertex colors on "+match.Key.name);
                targets.Add(match.Key,entry);
            }
        }
        if(saveAssets && !AssetDatabase.IsValidFolder("Assets/OpenBrushGenerated"))
            AssetDatabase.CreateFolder("Assets","OpenBrushGenerated");
        Undo.RecordObjects(targets.Keys.Cast<UnityEngine.Object>().ToArray(),"Prepare Open Brush materials");
        var cache=new Dictionary<Tuple<UnityEngine.Material,string>,UnityEngine.Material>();
        foreach(var item in targets)
        {
            var old=item.Key.sharedMaterial;
            var p=OpenBrushProfiles.Find(item.Value.openBrush.brushGuid);
            var key=Tuple.Create(old,p.Guid);
            if(!cache.TryGetValue(key,out var material))
            {
                material=new UnityEngine.Material(shader) { name="OpenBrush_"+p.Name,
                    mainTexture=p.IgnoreAlbedoTexture ? null : old.mainTexture, mainTextureScale=old.mainTextureScale, mainTextureOffset=old.mainTextureOffset };
                material.SetOverrideTag("OpenBrushGuid",p.Guid);
                var factor=item.Value.pbrMetallicRoughness?.baseColorFactor;
                var tint=factor==null ? Color.white : new Color(factor[0],factor[1],factor[2],factor[3]).gamma;
                tint.r*=p.Tint; tint.g*=p.Tint; tint.b*=p.Tint;
                material.SetColor("_TintColor",tint);
                material.SetTexture("_BumpMap",old.HasProperty("_BumpMap") ? old.GetTexture("_BumpMap") : null);
                material.SetFloat("_BumpScale",old.HasProperty("_BumpScale") ? old.GetFloat("_BumpScale") : 1);
                if(p.Additive && p.Cutout) {
                    var alphaTexture=old.mainTexture as Texture2D;
                    if(alphaTexture==null || !alphaTexture.isReadable)throw new InvalidOperationException("Missing readable additive mask");
                    material.SetTexture("_MaskTex",alphaTexture);
                    var rgb=UnityEngine.Object.Instantiate(alphaTexture);var pixels=rgb.GetPixels();
                    for(int i=0;i<pixels.Length;i++)pixels[i].a=1;
                    rgb.SetPixels(pixels);rgb.Apply(true,false);material.mainTexture=rgb;
                }
                material.SetFloat("_Gain",p.Gain);
                material.SetFloat("_Cutoff",p.Cutout ? p.Cutoff : 0);
                material.SetFloat("_SrcBlend",p.Additive || p.Transparent ? 5 : 1);
                material.SetFloat("_DstBlend",p.Additive ? 1 : p.Transparent ? 10 : 0);
                material.SetFloat("_ZWrite",p.Additive || p.Transparent ? 0 : 1);
                material.renderQueue=p.Additive || p.Transparent ? 3000 : p.Cutout ? 2450 : 2000;
                if(saveAssets)
                {
                    foreach(var property in new[] {"_MainTex","_BumpMap","_MaskTex"})
                    {
                        var texture=material.GetTexture(property);
                        if(texture==null || EditorUtility.IsPersistent(texture)) continue;
                        var copy=UnityEngine.Object.Instantiate(texture);
                        AssetDatabase.CreateAsset(copy,AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/"+p.Name+property+".asset"));
                        material.SetTexture(property,copy);
                    }
                    AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/"+p.Name+".mat"));
                }
                if(!p.IgnoreAlbedoTexture && old.mainTexture!=null && material.mainTexture==null)
                    throw new InvalidOperationException("Texture lost while preparing "+p.Name);
                cache.Add(key,material);
            }
            if(p.LinearVertexColors)OpenBrushVertexColors.Prepare(item.Key.GetComponent<MeshFilter>(),saveAssets);
            item.Key.sharedMaterial=material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(item.Key);
        }
        if(saveAssets) AssetDatabase.SaveAssets();
        Debug.Log("Open Brush profiles applied: "+cache.Count+" materials / "+targets.Count+" renderers.");
        return targets.Count;
    }
}


