using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Optional source-environment reflection. It travels with the model and only
// affects its local bounds; ambient light and the world's visible sky stay separate.
public static class OpenBrushReflectionImport {
    public static ReflectionProbe Prepare(GameObject root, Bounds worldBounds, string source, float intensity) {
        if (!File.Exists(source)) throw new FileNotFoundException("Reflection image missing", source);
        if (float.IsNaN(intensity) || float.IsInfinity(intensity) || intensity < 0)
            throw new ArgumentOutOfRangeException(nameof(intensity));
        byte[] bytes = File.ReadAllBytes(source);
        string hash;
        using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
        string path = "Assets/OpenBrushGenerated/Reflection_" + hash + Path.GetExtension(source);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (!File.Exists(path)) File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Unsupported reflection image: " + source);
        var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
        settings.textureShape = TextureImporterShape.TextureCube;
        settings.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
        settings.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
        settings.seamlessCubemap = true;
        settings.mipmapEnabled = true;
        settings.sRGBTexture = true;
        settings.readable = true;
        settings.filterMode = FilterMode.Trilinear;
        settings.wrapMode = TextureWrapMode.Clamp;
        importer.SetTextureSettings(settings);
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var cube = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        if (cube == null || cube.mipmapCount < 2) throw new InvalidOperationException("Expected a filtered reflection cubemap");
        var slot = new GameObject("Open Brush Source Reflection");
        slot.transform.SetParent(root.transform, false);
        slot.transform.position = worldBounds.center;
        slot.transform.rotation = Quaternion.identity;
        var probe = slot.AddComponent<ReflectionProbe>();
        probe.mode = ReflectionProbeMode.Baked;
        probe.customBakedTexture = cube;
        probe.intensity = intensity;
        probe.importance = 10;
        probe.size = worldBounds.size + Vector3.one * .2f;
        probe.blendDistance = 0;
        probe.boxProjection = false;
        probe.hdr = true;
        probe.resolution = cube.width;
        Debug.Log("OPENBRUSH_REFLECTION_PREPARED " + cube.width + "px, " + cube.mipmapCount + " mips, intensity=" + intensity);
        return probe;
    }
}
