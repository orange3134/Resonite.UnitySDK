using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;

public static class OpenBrushMotionChecks
{
    static void Check(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); Debug.Log("Motion check: " + message); }
    public static void Run() {
        try {
            Check(OpenBrushProfiles.All[0].Gain <= 2 && OpenBrushProfiles.All[1].Gain <= 2, "source HDR multipliers are not copied into native bloom");
            Check(ComponentConverterRepository.TryGetConverter(typeof(OpenBrushEmbersMotion))?.Type == typeof(OpenBrushEmbersConverter), "SDK finds the native particle converter");
            using (var data = new AutoGltfFileParser(Path.GetFullPath(".local/BrushTest-growth-v2-compact.glb")).Parse())
            using (var importer = new ImporterContext(data)) {
                var instance = importer.Load(); instance.ShowMeshes();
                var root = instance.gameObject;
                var manifest = JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(".local/BrushTest-growth-v2-compact.materials.json"));
                OpenBrushMaterialImport.Prepare(root, manifest, false);
                var before = root.GetComponentsInChildren<MeshFilter>().Select(m => m.sharedMesh).ToArray();
                var motion = OpenBrushMotionImport.Prepare(root);
                Check(motion.EmissionMesh.vertexCount == 468, "all 24 actual Embers segments supply the emission surface");
                Check(motion.EmissionMesh.bounds.size.magnitude > .1f, "combined emission positions retain the stroke's spread");
                Check(motion.EmissionMesh.colors.All(c => Mathf.Approximately(c.a, 1)), "random seeds are not mistaken for transparency");
                Check(root.GetComponentsInChildren<MeshRenderer>().Count(r => !r.enabled) == 24, "only static Embers are hidden");
                Check(root.GetComponentsInChildren<MeshFilter>().Select(m => m.sharedMesh).SequenceEqual(before), "original meshes remain unchanged");
                Check(OpenBrushMotionImport.Prepare(root) == motion && root.GetComponentsInChildren<OpenBrushEmbersMotion>().Length == 1, "preparing motion twice does not duplicate emitters");
                var pixels = motion.LifetimeTexture.GetPixels();
                Check(pixels.All(c => c.r >= 0 && c.r <= 1) && pixels.Last().r == 0, "sparkle is bounded and particles fade fully");
                Check(pixels.Max(c => c.r) - pixels.Min(c => c.r) > .4f, "lifetime curve has a visible sparkle peak");
                Check(motion.Rate * motion.Lifetime <= 100, "particle budget is bounded");
                Debug.Log("EMBERS_EMISSION_BOUNDS " + motion.EmissionMesh.bounds);
                UnityEngine.Object.DestroyImmediate(root);
            }
            Debug.Log("OPENBRUSH_MOTION_CHECKS_PASSED"); EditorApplication.Exit(0);
        } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
