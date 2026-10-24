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

// General model entry point: no BrushTest material, stroke count or placement assumptions.
public static class OpenBrushSendModel {
    [Serializable] public class Report {
        public string name,endpoint,rootId,error,mode;public float effectLoopSeconds; public string[] assetRootIds,brushes,bakeAdjustments;
        public int rendererCount,materialCount,growthPartCount;public string playbackId;public float duration;public string reflectionSource;public float reflectionIntensity;public bool completed;public Vector3 boundsCenter,boundsSize;
    }
    static OpenBrushConversionBundle bundle;
    static bool Has(string name)=>Array.IndexOf(Environment.GetCommandLineArgs(),name)>=0 || (bundle!=null && bundle.Has(name));
    static string Arg(string name) {
        if(bundle!=null && bundle.Has(name))return bundle.Get(name);
        var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,name);
        if(index<0 || index+1>=args.Length)throw new ArgumentException("Required argument: "+name);
        return args[index+1];
    }
    public static void Run() {
        LinkInterface link=null;Report report=new Report();string output=null;
        try {
            bundle=null;OpenBrushAtlasBudget.Adjustments.Clear();
            if(Has("-openbrushBundle")) {
                bundle=OpenBrushConversionBundle.Load(Arg("-openbrushBundle"));
                foreach(var name in new[]{"-openbrushModel","-openbrushManifest","-openbrushTimeline","-openbrushDisco","-openbrushParticles","-openbrushElectricity","-openbrushHyperGrid","-openbrushDanceFloor","-openbrushKeijiro"})
                    if(Array.IndexOf(Environment.GetCommandLineArgs(),name)>=0)throw new InvalidOperationException("Do not mix bundle inputs with standalone file arguments: "+name);
            }
            output=Path.GetFullPath(Arg("-openbrushReport"));
            if(File.Exists(output))throw new InvalidOperationException("Inspect and archive the previous send report before retrying.");
            OpenBrushImportProgress.Start(Has("-openbrushProgress") ? Arg("-openbrushProgress") : null);
            OpenBrushImportProgress.Write("unity","変換済みデータを確認中");
            report.name=Arg("-openbrushName");report.endpoint="ws://localhost:"+int.Parse(Arg("-openbrushPort"));
            var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(Arg("-openbrushManifest")));
            if(manifest?.materials==null || manifest.materials.Length==0)throw new InvalidOperationException("Empty manifest");
            report.brushes=manifest.materials.Select(m=>OpenBrushProfiles.Find(m.openBrush.brushGuid).Name).Distinct().ToArray();
            if(report.brushes.Contains("3D Printing Brush") && manifest.flatLighting==null)throw new InvalidOperationException("3D Printing needs source lighting metadata; regenerate the bundle.");
            OpenBrushDiscoImport.Data disco=null;
            if(report.brushes.Contains("Disco"))disco=JsonUtility.FromJson<OpenBrushDiscoImport.Data>(File.ReadAllText(Arg("-openbrushDisco")));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            OpenBrushImportProgress.Write("unity","モデルを読み込み中");
            using(var data=new AutoGltfFileParser(Path.GetFullPath(Arg("-openbrushModel"))).Parse())
            using(var importer=new ImporterContext(data)) {
                var instance=importer.Load();OpenBrushSourceTangents.Restore(importer);OpenBrushSourceBloom.Restore(importer);OpenBrushSourceSlice.Restore(importer);instance.ShowMeshes();var root=instance.gameObject;root.name=report.name;
                var xyz=Arg("-openbrushPosition").Split(',').Select(x=>float.Parse(x,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                if(xyz.Length!=3)throw new ArgumentException("Expected x,y,z position");
                root.transform.position=new Vector3(xyz[0],xyz[1],xyz[2]);
                // Legacy Open Brush reflects X on export; UniGLTF reflects Z on import.
                // Rotate 180 degrees around Y to restore the original Open Brush basis.
                root.transform.rotation=Quaternion.Euler(0,180,0);
                OpenBrushImportProgress.Write("unity","ブラシを設定中");
                report.rendererCount=OpenBrushMaterialImport.Prepare(root,manifest,false);
                if(report.rendererCount==0 || report.rendererCount!=root.GetComponentsInChildren<MeshRenderer>(true).Length)
                    throw new InvalidOperationException("Manifest must cover every mesh renderer");
                OpenBrushProceduralImport.Prepare(root);
                if(report.brushes.Contains("Embers"))OpenBrushMotionImport.Prepare(root);
                if(disco!=null)OpenBrushDiscoImport.Prepare(root,disco);
                OpenBrushGrowthImport.Data timing=null;
                var bounds=root.GetComponentsInChildren<Renderer>(true).First().bounds;
                foreach(var r in root.GetComponentsInChildren<Renderer>(true))bounds.Encapsulate(r.bounds);
                report.boundsCenter=bounds.center;report.boundsSize=bounds.size;
                if(Has("-openbrushTimeline")) {
                    timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(Arg("-openbrushTimeline")));
                    if(timing.segments.Length!=report.rendererCount)throw new InvalidOperationException("Timeline must cover every source mesh renderer");
                    var timeline=timing.completed ? OpenBrushGrowthImport.PrepareCompleted(root,timing) : OpenBrushGrowthImport.Prepare(root,timing,disco);
                    report.mode=timing.completed ? "completed" : "animated";
                    report.effectLoopSeconds=timing.completed ? timeline.Duration : 0;
                    report.growthPartCount=root.GetComponentsInChildren<OpenBrushGrowthPart>().Length;
                    report.duration=timing.completed ? 0 : timeline.Duration;
                    // Place controls below the model in world space regardless of its source origin.
                    var controls=root.transform.Find("Playback Controls");
                    if(controls!=null)controls.position=new Vector3(bounds.center.x,bounds.min.y-.12f,bounds.min.z-.04f);
                }
                OpenBrushImportProgress.Write("unity","ブラシ効果を準備中");
                if(report.brushes.Contains("Rising Bubbles") || report.brushes.Contains("Stars") || report.brushes.Contains("Snow") || report.brushes.Contains("Bubbles") || report.brushes.Contains("Dots")) OpenBrushParticleImport.Prepare(root,JsonUtility.FromJson<OpenBrushParticleImport.Data>(File.ReadAllText(Arg("-openbrushParticles"))),timing);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-openbrushExperimentalNeonMatcap")>=0)OpenBrushNeonMatcapImport.Prepare(root);
                else OpenBrushNeonImport.Prepare(root);
                OpenBrushFireImport.Prepare(root);
                OpenBrushCometImport.Prepare(root);
                OpenBrushFlowImport.Prepare(root);
                OpenBrushWaveformImport.Prepare(root);
                OpenBrushHypercolorImport.Prepare(root);
                OpenBrushHueShiftImport.Prepare(root);
                OpenBrushDigitalImport.Prepare(root);
                OpenBrushSliceImport.Prepare(root);
                if(report.brushes.Contains("KeijiroTube"))OpenBrushKeijiroImport.Prepare(root,JsonUtility.FromJson<OpenBrushKeijiroImport.Data>(File.ReadAllText(Arg("-openbrushKeijiro"))),timing);
                OpenBrushTriangleImport.Prepare(root,manifest.flatLighting);
                if(Has("-openbrushPrintingShadows"))OpenBrushPrintingShadowImport.Prepare(root,manifest.flatLighting);
                OpenBrushLightWireImport.Prepare(root);
                if(report.brushes.Contains("Electricity"))OpenBrushElectricityImport.Prepare(root,JsonUtility.FromJson<OpenBrushElectricityImport.Data>(File.ReadAllText(Arg("-openbrushElectricity"))),timing);
                if(report.brushes.Contains("HyperGrid"))OpenBrushHyperGridImport.Prepare(root,JsonUtility.FromJson<OpenBrushHyperGridImport.Data>(File.ReadAllText(Arg("-openbrushHyperGrid"))),timing);
                if(report.brushes.Contains("DanceFloor"))OpenBrushDanceFloorImport.Prepare(root,JsonUtility.FromJson<OpenBrushDanceFloorImport.Data>(File.ReadAllText(Arg("-openbrushDanceFloor"))),timing);
                OpenBrushAdditiveImport.Prepare(root);
                OpenBrushHighlighterImport.Prepare(root);
                OpenBrushSoftFragmentImport.Prepare(root);
                OpenBrushWireframeImport.Prepare(root);
                OpenBrushLeakyPenImport.Prepare(root);
                OpenBrushMarbledImport.Prepare(root);
                OpenBrushMylarImport.Prepare(root);
                if(Has("-openbrushDiamondFilm"))OpenBrushDiamondImport.Prepare(root);
                OpenBrushToonImport.Prepare(root);
                if(Array.IndexOf(Environment.GetCommandLineArgs(),"-openbrushReflection")>=0) {
                    report.reflectionSource=Path.GetFullPath(Arg("-openbrushReflection"));
                    report.reflectionIntensity=float.Parse(Arg("-openbrushReflectionIntensity"),System.Globalization.CultureInfo.InvariantCulture);
                    OpenBrushReflectionImport.Prepare(root,bounds,report.reflectionSource,report.reflectionIntensity);
                }
                if(timing!=null && timing.completed && (root.GetComponentsInChildren<OpenBrushGrowthPart>(true).Length!=0 || root.GetComponentsInChildren<OpenBrushPlaybackButton>(true).Length!=0))
                    throw new InvalidOperationException("Completed drawing unexpectedly contains stroke playback controls or growth drivers");
                report.bakeAdjustments=OpenBrushAtlasBudget.Adjustments.ToArray();
                OpenBrushImportProgress.Write("send","ResoniteLinkに接続中");
                link=new LinkInterface();var session=Task.Run(async()=>{await link.Connect(new Uri(report.endpoint),CancellationToken.None);return await link.GetSessionData();}).GetAwaiter().GetResult();
                if(!session.Success)throw new InvalidOperationException(session.ErrorInfo);
                var window=ScriptableObject.CreateInstance<ResoniteLinkWindow>();window.ConvertSkybox=false;
                typeof(ResoniteLinkWindow).GetField("_linkInterface",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,link);
                typeof(ResoniteLinkWindow).GetField("_uniqueSessionId",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,session.UniqueSessionId);
                var converter=new SceneConverter();converter.EnsureInitialized(window);converter.EnsureAssetConverter();
                report.rootId=converter.GetTransformSlotId(root.transform);Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));
                bool failed=false;Application.LogCallback errors=(m,s,t)=>{if(t==LogType.Error || t==LogType.Exception)failed=true;};
                Action<int,int> progress=(current,total)=>OpenBrushImportProgress.Write("send","アセットを送信中",current,total);
                AssetConversionManager.ConversionProgress+=progress;
                OpenBrushImportProgress.Write("send","ワールドにモデルを作成中");
                Application.logMessageReceived+=errors;
                try{converter.ConvertScene();}
                finally{Application.logMessageReceived-=errors;AssetConversionManager.ConversionProgress-=progress;}
                OpenBrushImportProgress.Write("send","送信結果を確認中");
                report.assetRootIds=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Where(g=>g!=root).Select(g=>converter.GetTransformSlotId(g.transform)).ToArray();
                report.materialCount=UnityEngine.Object.FindObjectsOfType<OpenBrushMaterialConverter>().Length;
                var playback=root.GetComponent<OpenBrushTimelineConverter>();
                if(playback!=null)report.playbackId=converter.GetId(playback.Player.Binding);
                report.completed=!failed && !converter.IsCorrupted;File.WriteAllText(output,JsonUtility.ToJson(report,true));
                if(!report.completed)throw new InvalidOperationException("SDK conversion reported errors");
                Debug.Log("OPENBRUSH_MODEL_SENT "+JsonUtility.ToJson(report));
            }
            link.Dispose();EditorApplication.Exit(0);
        } catch(Exception e) {
            report.bakeAdjustments=OpenBrushAtlasBudget.Adjustments.ToArray();report.error=e.ToString();if(output!=null && (!File.Exists(output) || report.rootId!=null))File.WriteAllText(output,JsonUtility.ToJson(report,true));
            Debug.LogException(e);link?.Dispose();EditorApplication.Exit(1);
        }
    }
}












