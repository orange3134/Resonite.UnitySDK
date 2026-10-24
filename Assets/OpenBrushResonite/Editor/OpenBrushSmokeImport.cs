using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OpenBrushSmokeImport
{
    public const string SmokeGuid = "70d79cca-b159-4f35-990c-f02193947fe8";
    [Serializable] public class Manifest { public int schemaVersion; public Entry[] materials; public OpenBrushTriangleImport.Lighting flatLighting; }
    [Serializable] public class Entry { public Identity openBrush; public Target[] targets; public Pbr pbrMetallicRoughness; }
    [Serializable] public class Identity { public string brushGuid; public string vertexBake; }
    [Serializable] public class Target { public string nodeName; }
    [Serializable] public class Pbr { public float[] baseColorFactor; }

    // Prepare an imported v2 GLB before sending its hierarchy through the SDK.
    [MenuItem("Resonite SDK/Open Brush/Prepare Smoke on Selected Model")]
    public static void PrepareSelected()
    {
        var root = Selection.activeGameObject;
        if (root == null || EditorUtility.IsPersistent(root))
            throw new InvalidOperationException("Select the imported model root in the scene.");
        var file = EditorUtility.OpenFilePanel("Open Brush materials.json", "", "json");
        if (string.IsNullOrEmpty(file)) return;
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(file));
        Prepare(root, manifest);
    }

    public static int Prepare(GameObject root, Manifest manifest, bool saveAssets = true)
    {
        var renderers = ResolveTargets(root, manifest);
        if (renderers.Count == 0)
            throw new InvalidOperationException("No Smoke entries in this manifest.");
        var shader = Shader.Find("OpenBrush/ExportedSmoke");
        if (shader == null) throw new InvalidOperationException("ExportedSmoke shader is missing.");

        // Validate the full selection before creating assets or changing any renderer.
        foreach (var renderer in renderers.Keys)
            if (renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial == null ||
                renderer.sharedMaterial.mainTexture == null)
                throw new InvalidOperationException("Expected one textured material: " + renderer.name);

        if (saveAssets && !AssetDatabase.IsValidFolder("Assets/OpenBrushGenerated"))
            AssetDatabase.CreateFolder("Assets", "OpenBrushGenerated");
        var converted = new Dictionary<Tuple<UnityEngine.Material, Entry>, UnityEngine.Material>();
        Undo.RecordObjects(renderers.Keys.Cast<UnityEngine.Object>().ToArray(), "Prepare Open Brush Smoke");
        foreach (var pair in renderers)
        {
            var original = pair.Key.sharedMaterial;
            var key = Tuple.Create(original, pair.Value);
            if (!converted.TryGetValue(key, out var material))
            {
                material = new UnityEngine.Material(shader) { name = "OpenBrush_Smoke",
                    mainTexture = original.mainTexture, mainTextureScale = original.mainTextureScale,
                    mainTextureOffset = original.mainTextureOffset };
                var factor = pair.Value.pbrMetallicRoughness?.baseColorFactor;
                var tint = factor == null ? Color.white : new Color(factor[0], factor[1], factor[2], factor[3]).gamma;
                material.SetColor("_TintColor", tint);
                if (saveAssets)
                {
                    if (!EditorUtility.IsPersistent(material.mainTexture))
                    {
                        var texture = UnityEngine.Object.Instantiate(material.mainTexture);
                        AssetDatabase.CreateAsset(texture, AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/SmokeTexture.asset"));
                        material.mainTexture = texture;
                    }
                    AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/Smoke.mat"));
                }
                converted.Add(key, material);
            }
            pair.Key.sharedMaterial = material;
            PrefabUtility.RecordPrefabInstancePropertyModifications(pair.Key);
        }
        if (saveAssets) AssetDatabase.SaveAssets();
        Debug.Log("Prepared Smoke materials for " + renderers.Count + " renderers. Geometry and animation were preserved.");
        return renderers.Count;
    }

    public static Dictionary<Renderer, Entry> ResolveTargets(GameObject root, Manifest manifest)
    {
        if (manifest == null || manifest.schemaVersion != 1 || manifest.materials == null)
            throw new InvalidOperationException("Expected a version 1 Open Brush material manifest.");
        var result = new Dictionary<Renderer, Entry>();
        var nodes = root.GetComponentsInChildren<Transform>(true).ToLookup(t => t.name);
        foreach (var entry in manifest.materials.Where(e => e?.openBrush?.brushGuid == SmokeGuid))
        {
            var factor = entry.pbrMetallicRoughness?.baseColorFactor;
            if (factor != null && factor.Length != 4)
                throw new InvalidOperationException("Invalid Smoke base color factor.");
            if (entry.targets == null || entry.targets.Length == 0)
                throw new InvalidOperationException("Smoke has no target nodes.");
            foreach (var target in entry.targets)
            {
                if (target == null || string.IsNullOrEmpty(target.nodeName))
                    throw new InvalidOperationException("Empty Smoke target.");
                var matches = nodes[target.nodeName].ToArray();
                if (matches.Length != 1)
                    throw new InvalidOperationException("Missing or ambiguous node: " + target.nodeName);
                var renderer = matches[0].GetComponent<Renderer>();
                if (renderer == null || result.ContainsKey(renderer))
                    throw new InvalidOperationException("Missing or repeated renderer: " + target.nodeName);
                result.Add(renderer, entry);
            }
        }
        return result;
    }
}

