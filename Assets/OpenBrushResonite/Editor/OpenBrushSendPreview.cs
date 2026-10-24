using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ResoniteLink;
using UniGLTF;

// Explicit batch entry point. No automatic sends on load or recompilation.
public static class OpenBrushSendPreview
{
    [Serializable] public class Report {
        public string rootId; public string name; public string endpoint; public int rendererCount;
        public int smokeCount; public string smokeMaterialId; public string[] assetRootIds;
        public bool completed; public string error; public string[] convertedMaterials;
        public string particleSystemId; public string particleEmitterId; public string[] textureMotionIds; public string[] meshMotionIds; public string playbackId; public int growthPartCount;
    }
    static string Arg(string key) {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, key);
        if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("Required argument: " + key);
        return args[i+1];
    }
    public static void Run()
    {
        LinkInterface link = null;
        var report = new Report();
        bool growth = Environment.GetCommandLineArgs().Contains("-openbrushGrowth");
        bool graphiteDisco = growth || Environment.GetCommandLineArgs().Contains("-openbrushGraphiteDisco");
        bool wireShape = graphiteDisco || Environment.GetCommandLineArgs().Contains("-openbrushWireShape");
        bool procedural = wireShape || Environment.GetCommandLineArgs().Contains("-openbrushProcedural");
        bool motion = procedural || Environment.GetCommandLineArgs().Contains("-openbrushMotion");
        bool allBrushes = motion || Environment.GetCommandLineArgs().Contains("-openbrushAll");
        var output = Path.GetFullPath(growth ? ".local/sdk-send-growth-v7-report.json" : graphiteDisco ? ".local/sdk-send-graphite-disco-report.json" : wireShape ? ".local/sdk-send-wire-shape-report.json" : procedural ? ".local/sdk-send-procedural-report.json" : motion ? ".local/sdk-send-motion-report.json" : allBrushes ? ".local/sdk-send-all-report.json" : ".local/sdk-send-report.json");
        try
        {
            int port = int.Parse(Arg("-openbrushPort"));
            var model = Path.GetFullPath(Arg("-openbrushModel"));
            var manifestPath = Path.GetFullPath(Arg("-openbrushManifest"));
            if (File.Exists(output)) throw new InvalidOperationException("A send report already exists; inspect it before sending again.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            using (var data = new AutoGltfFileParser(model).Parse())
            using (var importer = new ImporterContext(data))
            {
                var instance = importer.Load();
                instance.ShowMeshes();
                var root = instance.gameObject;
                root.name = growth ? "BrushTest SDK - Animated Drawing v7" : graphiteDisco ? "BrushTest SDK - WigglyGraphite and Disco v6" : wireShape ? "BrushTest SDK - LightWire Shape v5" : procedural ? "BrushTest SDK - Fire and LightWire v4" : motion ? "BrushTest SDK - Sparkle v3" : allBrushes ? "BrushTest SDK - All Brushes v2" : "BrushTest SDK - Smoke v1";
                // Match the existing comparison object's orientation and size, offset 3.5m.
                root.transform.position = new Vector3(graphiteDisco ? 14.5637145f : wireShape ? 11.0637145f : procedural ? 7.5637145f : motion ? 4.0637145f : allBrushes ? 0.5637145f : -2.9362855f, 5.505796f, -8.755463f);
                if(growth)root.transform.position=new Vector3(4.6f,1.8f,1.3f);
                root.transform.rotation = new Quaternion(0, 1, 0, -4.371139E-08f);
                root.transform.localScale = Vector3.one * 1.2784332f;
                var manifest = JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(manifestPath));
                if (allBrushes)
                {
                    if (OpenBrushMaterialImport.Prepare(root, manifest, false) != 375) throw new InvalidOperationException("Incomplete material coverage.");
                    report.smokeCount = manifest.materials.Single(m => m.openBrush.brushGuid == OpenBrushSmokeImport.SmokeGuid).targets.Length;
                }
                else report.smokeCount = OpenBrushSmokeImport.Prepare(root, manifest, saveAssets: false);
                report.rendererCount = root.GetComponentsInChildren<Renderer>(true).Length;
                if (report.smokeCount != 22 || report.rendererCount != 375)
                    throw new InvalidOperationException("Unexpected BrushTest geometry; nothing sent.");
                if (motion) OpenBrushMotionImport.Prepare(root);
                if (procedural) OpenBrushProceduralImport.Prepare(root);
                if (graphiteDisco) OpenBrushDiscoImport.Prepare(root,JsonUtility.FromJson<OpenBrushDiscoImport.Data>(File.ReadAllText(Arg("-openbrushDiscoData"))));
                if(growth)OpenBrushGrowthImport.Prepare(root,JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(Arg("-openbrushTimeline"))),JsonUtility.FromJson<OpenBrushDiscoImport.Data>(File.ReadAllText(Arg("-openbrushDiscoData"))));
                if(procedural)OpenBrushFireImport.Prepare(root);
                report.name = root.name;
                report.endpoint = "ws://localhost:" + port;
                var meshes = root.GetComponentsInChildren<MeshFilter>(true);
                if (meshes.Any(m => m.sharedMesh == null)) throw new InvalidOperationException("Missing source mesh.");
                link = new LinkInterface();
                var session = Task.Run(async () => {
                    await link.Connect(new Uri(report.endpoint), CancellationToken.None);
                    return await link.GetSessionData();
                }).GetAwaiter().GetResult();
                if (!session.Success) throw new InvalidOperationException(session.ErrorInfo);
                var window = ScriptableObject.CreateInstance<ResoniteLinkWindow>();
                window.Port = port;
                window.ConvertSkybox = false;
                // Upstream exposes conversion, but the connection holder is private.
                typeof(ResoniteLinkWindow).GetField("_linkInterface", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, link);
                typeof(ResoniteLinkWindow).GetField("_uniqueSessionId", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(window, session.UniqueSessionId);
                var converter = new SceneConverter();
                converter.EnsureInitialized(window);
                converter.EnsureAssetConverter();
                report.rootId = converter.GetTransformSlotId(root.transform);
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                bool conversionError = false;
                Application.LogCallback errors = (message, stack, type) => {
                    if (type == LogType.Error || type == LogType.Exception) conversionError = true;
                };
                Application.logMessageReceived += errors;
                try { converter.ConvertScene(); }
                finally { Application.logMessageReceived -= errors; }
                report.assetRootIds = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects()
                    .Where(g => g != root).Select(g => converter.GetTransformSlotId(g.transform)).ToArray();
                var smoke = UnityEngine.Object.FindObjectOfType<OpenBrushSmokeConverter>();
                if (smoke != null) report.smokeMaterialId = converter.GetId(smoke.Unlit.Binding);
                if (allBrushes)
                {
                    var profiles = UnityEngine.Object.FindObjectsOfType<OpenBrushMaterialConverter>();
                    report.convertedMaterials = profiles.Select(p => p.Profile.Name + "=" + (p.Unlit != null ? converter.GetId(p.Unlit.Binding) : p.Specular != null ? converter.GetId(p.Specular.Binding) : converter.GetId(p.Surface.Binding))).ToArray();
                    report.smokeMaterialId = converter.GetId(profiles.Single(p => p.Profile.Name == "Smoke").Unlit.Binding);
                    if (profiles.Select(p=>p.Profile.Guid).Distinct().Count() != 14) conversionError = true;
                }
                if (motion) {
                    var particle = UnityEngine.Object.FindObjectOfType<OpenBrushEmbersConverter>();
                    if (particle == null) throw new InvalidOperationException("Embers converter was not called.");
                    report.particleSystemId = converter.GetId(particle.System.Binding);
                    report.particleEmitterId = converter.GetId(particle.Emitter.Binding);
                }
                if (procedural) {
                    var sequences=UnityEngine.Object.FindObjectsOfType<OpenBrushTextureMotionConverter>();
                    report.textureMotionIds=sequences.Select(s=>s.name+"="+converter.GetId(s.Sequence.Binding)+";"+converter.GetId(s.Clock.Binding)).ToArray();
                    if(sequences.Length!=3) conversionError=true;
                }
                if(graphiteDisco) {
                    var sequences=UnityEngine.Object.FindObjectsOfType<OpenBrushMeshMotionConverter>();
                    report.meshMotionIds=sequences.Select(s=>s.name+"="+converter.GetId(s.Sequence.Binding)+";"+converter.GetId(s.Clock.Binding)+";"+converter.GetId(s.Glints.Binding)+";"+converter.GetId(s.GlintClock.Binding)+";"+converter.GetId(s.Renderer.Binding)).ToArray();
                    if(sequences.Length!=1)conversionError=true;
                }
                if(growth) {
                    var timeline=root.GetComponent<OpenBrushTimelineConverter>();report.playbackId=converter.GetId(timeline.Player.Binding);
                    report.growthPartCount=root.GetComponentsInChildren<OpenBrushGrowthPart>().Length;
                    if(report.growthPartCount!=375)conversionError=true;
                    if(!conversionError && !converter.IsCorrupted)Task.Run(async()=>await timeline.Player.Binding.Play(converter)).GetAwaiter().GetResult();
                }
                report.completed = !converter.IsCorrupted && !conversionError;
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                if (!report.completed) throw new InvalidOperationException("SDK reported a conversion error; inspect the report and world before retry.");
                Debug.Log("OPENBRUSH_SDK_SENT " + JsonUtility.ToJson(report));
            }
            link.Dispose();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            report.error = e.ToString();
            // Preserve a previous send report when the duplicate-send guard fires.
            if (!File.Exists(output) || !string.IsNullOrEmpty(report.rootId))
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
            Debug.LogException(e);
            link?.Dispose();
            EditorApplication.Exit(1);
        }
    }
}




