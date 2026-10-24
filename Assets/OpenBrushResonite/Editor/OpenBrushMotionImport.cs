using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OpenBrushMotionImport
{
    [MenuItem("Resonite SDK/Open Brush/Add Embers Motion to Prepared Model")]
    public static void PrepareSelected() {
        var root = Selection.activeGameObject;
        if (root == null || EditorUtility.IsPersistent(root)) throw new InvalidOperationException("Select the prepared model root in the scene.");
        Prepare(root, true);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(root.scene);
    }
    const string EmbersGuid = "02ffb866-7fb2-4d15-b761-1012cefb1360";
    public static OpenBrushEmbersMotion Prepare(GameObject root, bool saveAssets = false)
    {
        var existing = root.GetComponentInChildren<OpenBrushEmbersMotion>(true);
        if (existing != null) return existing;
        var parts = root.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.sharedMaterial != null && r.sharedMaterial.GetTag("OpenBrushGuid", false, "") == EmbersGuid).ToArray();
        if (parts.Length == 0) throw new InvalidOperationException("Prepare the GUID material profiles before adding Embers motion.");
        if (parts.Any(r => r.GetComponent<MeshFilter>()?.sharedMesh == null))
            throw new InvalidOperationException("An Embers part is missing its source mesh.");
        var sprite = parts[0].sharedMaterial.mainTexture as Texture2D;
        if (sprite == null) throw new InvalidOperationException("Missing Embers sprite.");
        var mesh = new Mesh { name = "Embers emission surface" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.CombineMeshes(parts.Select(r => new CombineInstance {
            mesh = r.GetComponent<MeshFilter>().sharedMesh,
            transform = root.transform.worldToLocalMatrix * r.transform.localToWorldMatrix
        }).ToArray(), true, true);
        // Open Brush's alpha is a per-particle random seed, not opacity.
        var colors = mesh.colors;
        if (colors.Length != mesh.vertexCount) throw new InvalidOperationException("Embers requires vertex colors.");
        for (int i = 0; i < colors.Length; i++) colors[i].a = 1;
        mesh.colors = colors;
        var fade = CreateLifetimeTexture();
        if (saveAssets) {
            if (!AssetDatabase.IsValidFolder("Assets/OpenBrushGenerated")) AssetDatabase.CreateFolder("Assets", "OpenBrushGenerated");
            if (!EditorUtility.IsPersistent(sprite)) {
                sprite = UnityEngine.Object.Instantiate(sprite);
                AssetDatabase.CreateAsset(sprite, AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/EmbersSprite.asset"));
            }
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/EmbersMotionMesh.asset"));
            AssetDatabase.CreateAsset(fade, AssetDatabase.GenerateUniqueAssetPath("Assets/OpenBrushGenerated/EmbersLifetime.asset"));
        }
        var child = new GameObject("Embers - Native Sparkle Motion");
        child.transform.SetParent(root.transform, false);
        var motion = child.AddComponent<OpenBrushEmbersMotion>();
        motion.EmissionMesh = mesh; motion.Sprite = sprite; motion.LifetimeTexture = fade;
        // Only the replacement's static Embers are hidden; all other brushes remain untouched.
        foreach (var part in parts) part.enabled = false;
        if (saveAssets) AssetDatabase.SaveAssets();
        return motion;
    }
    public static Texture2D CreateLifetimeTexture()
    {
        var texture = new Texture2D(256, 1, TextureFormat.RGBA32, false, true) {
            name = "Embers sparkle and fade", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
        };
        var pixels = new Color[256];
        for (int i = 0; i < pixels.Length; i++) {
            float age = i / 255f;
            // Bounded approximation of the shader's narrow sin^30 flashes and age fade.
            // Birth timing and lifetime jitter keep different particles out of phase.
            float sparkle = Mathf.Pow(Mathf.Abs(Mathf.Sin(age * 9.5f)), 20);
            float envelope = Mathf.Pow(1 - age, 2);
            float brightness = (.12f + .88f * sparkle) * envelope;
            pixels[i] = new Color(brightness, brightness, brightness, 1);
        }
        texture.SetPixels(pixels); texture.Apply(false, false);
        return texture;
    }
}

