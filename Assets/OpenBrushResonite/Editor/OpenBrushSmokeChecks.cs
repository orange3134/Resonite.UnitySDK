using System;
using FrooxEngine;
using UnityEditor;
using UnityEngine;

public static class OpenBrushSmokeChecks
{
    // Invoked by Unity -batchmode -executeMethod OpenBrushSmokeChecks.Run.
    public static void Run()
    {
        try
        {
            var shader = UnityEngine.Shader.Find("OpenBrush/ExportedSmoke");
            Require(shader != null, "Preview shader imported");
            var material = new UnityEngine.Material(shader);
            material.SetColor("_TintColor", new Color(0.2f, 0.4f, 0.7f, 0.8f));
            material.SetFloat("_Opacity", 0.25f);
            material.mainTextureScale = new Vector2(2, 3);
            material.mainTextureOffset = new Vector2(0.1f, 0.2f);
            Require(MaterialConverterRepository.TryGetConverter(material) == typeof(OpenBrushSmokeConverter), "Exact SDK converter discovery");
            var data = new FrooxEngine.UnlitMaterial();
            OpenBrushSmokeConverter.Configure(material, data);
            Require(data.BlendMode == BlendMode.Additive && data.Sidedness == Sidedness.Double && data.ZWrite == ZWrite.Off, "Smoke render state");
            Require(data.UseVertexColors && !data.UseBillboardGeometry, "Exported vertex layout");
            Require(data.TextureScale == material.mainTextureScale && data.TextureOffset == material.mainTextureOffset, "UV transform");
            Debug.Log("Tint output: " + data.TintColor.color + "; profile=" + data.TintColor.profile + "; source=" + material.GetColor("_TintColor") + "; opacity=" + material.GetFloat("_Opacity") + "; hasTint=" + material.HasProperty("_TintColor"));
            Require(data.TintColor.profile == ColorProfile.sRGB && data.TintColor.color == new Color(0.2f, 0.4f, 0.7f, 0.2f), "Tint and opacity");

            var root = new GameObject("Model");
            var node = new GameObject("smoke_part");
            node.transform.SetParent(root.transform);
            node.AddComponent<UnityEngine.MeshRenderer>();
            var manifest = new OpenBrushSmokeImport.Manifest { schemaVersion = 1, materials = new[] {
                new OpenBrushSmokeImport.Entry { openBrush = new OpenBrushSmokeImport.Identity { brushGuid = OpenBrushSmokeImport.SmokeGuid },
                    targets = new[] { new OpenBrushSmokeImport.Target { nodeName = "smoke_part" } } } } };
            Require(OpenBrushSmokeImport.ResolveTargets(root, manifest).Count == 1, "Manifest binding");
            var duplicate = new GameObject("smoke_part");
            duplicate.transform.SetParent(root.transform);
            bool rejected = false;
            try { OpenBrushSmokeImport.ResolveTargets(root, manifest); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Ambiguous hierarchy rejected before changes");
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(material);
            Debug.Log("OPENBRUSH_SMOKE_CHECKS_PASSED");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        Debug.Log("Smoke check: " + name);
    }
}
