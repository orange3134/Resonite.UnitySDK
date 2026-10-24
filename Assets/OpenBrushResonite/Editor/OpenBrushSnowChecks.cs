using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class OpenBrushSnowChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
 public static void SendFixture(){
  ResoniteLink.LinkInterface link=null;
  try {
   var root=new GameObject("Snow compact verification");root.transform.position=new Vector3(0,1,2);
   var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=25;timeline.DurationClip=OpenBrushGrowthImport.CreateDurationClip(25,"CompletedSnowTimeline");timeline.AutoPlay=true;
   var child=new GameObject("Snow test group");child.transform.SetParent(root.transform,false);var group=child.AddComponent<OpenBrushSnowGroup>();group.Timeline=timeline;group.Paint=new Color(.3f,.6f,.9f,.25f);
   group.Mesh=OpenBrushSnowImport.PointMesh(new[]{new OpenBrushParticleMotion.Particle{center=Vector3.zero,size=.02f,uv=new Vector4(0,0,1,1)},new OpenBrushParticleMotion.Particle{center=Vector3.right*.2f,size=.03f,uv=new Vector4(0,0,1,1)}});
   group.SourceMaterial=new Material(Shader.Find("OpenBrush/ExportedBrush")){mainTexture=Texture2D.whiteTexture};
   var args=Environment.GetCommandLineArgs();int portArg=Array.IndexOf(args,"-openbrushPort");
   if(portArg<0 || portArg+1>=args.Length || !int.TryParse(args[portArg+1],out int port) || port<1 || port>65535)throw new ArgumentException("SendFixture requires -openbrushPort 1..65535");
   link=new ResoniteLink.LinkInterface();var session=System.Threading.Tasks.Task.Run(async()=>{await link.Connect(new Uri("ws://localhost:"+port),System.Threading.CancellationToken.None);return await link.GetSessionData();}).GetAwaiter().GetResult();
   if(!session.Success)throw new InvalidOperationException(session.ErrorInfo);
   var window=ScriptableObject.CreateInstance<ResoniteLinkWindow>();window.ConvertSkybox=false;
   typeof(ResoniteLinkWindow).GetField("_linkInterface",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(window,link);
   typeof(ResoniteLinkWindow).GetField("_uniqueSessionId",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(window,session.UniqueSessionId);
   var converter=new SceneConverter();converter.EnsureInitialized(window);converter.EnsureAssetConverter();
   bool failed=false;Application.LogCallback capture=(m,stack,type)=>{if(type==LogType.Error || type==LogType.Exception)failed=true;};Application.logMessageReceived+=capture;
   try{converter.ConvertScene();}finally{Application.logMessageReceived-=capture;}
   if(failed || converter.IsCorrupted)throw new InvalidOperationException("Snow native fixture failed");
   System.IO.File.WriteAllText(".local/Twiste-M-Snow-fixture-id.txt",converter.GetTransformSlotId(root.transform));
   Debug.Log("OPENBRUSH_SNOW_NATIVE_SENT root="+converter.GetTransformSlotId(root.transform));link.Dispose();EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);link?.Dispose();EditorApplication.Exit(1);}
 }
 public static void Run(){try {
  var go=new GameObject("Snow oracle");var old=go.AddComponent<OpenBrushParticleMotion>();old.Kind=OpenBrushParticleMotion.BrushKind.Snow;
  old.Particles=Enumerable.Range(0,16).Select(i=>new OpenBrushParticleMotion.Particle{center=new Vector3(i*.17f,i*.23f,-i*.13f),size=.01f+i*.001f,rotation=i*.2f,uv=new Vector4(.25f,0,.5f,.5f),color=new Color(i/16f,.8f,.3f,i/255f)}).ToArray();
  var mesh=OpenBrushSnowImport.PointMesh(old.Particles);
  Check(mesh.vertexCount==16 && mesh.GetTopology(0)==MeshTopology.Points && mesh.vertices.SequenceEqual(old.Particles.Select(p=>p.center)),"Static mesh retains every original center");
  float error=0;
  for(int f=0;f<750;f++) {
   old.Sample(f,out var positions,out var sizes,out var colors);
   for(int i=0;i<old.Particles.Length;i++) {
    OpenBrushSnowGroup.Sample(old.Particles[i].color,f/30f,out var offset,out var tint);
    error=Mathf.Max(error,Vector3.Distance(mesh.vertices[i]+offset,positions[i]));
    Check(Mathf.Abs(tint.r-colors[i].r)+Mathf.Abs(tint.g-colors[i].g)+Mathf.Abs(tint.b-colors[i].b)<1e-6,"Linear tint matches prior shader sampling");
    Check(mesh.normals[i]==new Vector3(sizes[i].x,sizes[i].y,old.Particles[i].rotation),"Native billboard size and rotation retained");
   }
  }
  Check(error<1e-6,"All 750 original positions match");Check(mesh.uv[0]==new Vector2(.375f,.75f) && mesh.uv2[0]==new Vector2(.25f,.5f),"Original sprite coordinates retained");
  var plan=OpenBrushParticleImport.CompletedFramePlan(new[]{new OpenBrushParticleImport.Part{brushGuid=OpenBrushParticleImport.SnowGuid,particles=new OpenBrushParticleMotion.Particle[89727]},new OpenBrushParticleImport.Part{brushGuid=OpenBrushParticleImport.StarsGuid,particles=new OpenBrushParticleMotion.Particle[5154]},new OpenBrushParticleImport.Part{brushGuid=OpenBrushParticleImport.BubblesGuid,particles=new OpenBrushParticleMotion.Particle[84]}});
  Check(plan.SequenceEqual(new[]{1,64,1800}),"Twiste-M counts fit without reducing sparkle or bubble sampling");
  var root=new GameObject("Grouping checks");var material=new Material(Shader.Find("OpenBrush/ExportedBrush"));
  var parts=new OpenBrushParticleImport.Part[3];
  for(int i=0;i<3;i++){
   var parent=new GameObject("SnowPart"+i);parent.transform.SetParent(root.transform,false);if(i==2){parent.transform.localPosition=Vector3.one;parent.transform.localScale=Vector3.one*2;}
   parent.AddComponent<MeshRenderer>().sharedMaterial=material;parts[i]=new OpenBrushParticleImport.Part{nodeName=parent.name,brushGuid=OpenBrushParticleImport.SnowGuid,particles=new[]{old.Particles[0],old.Particles[0]}};
  }
  var originalClip=OpenBrushGrowthImport.CreateDurationClip(8);OpenBrushSnowImport.Prepare(root,parts);
  var groups=root.GetComponentsInChildren<OpenBrushSnowGroup>();
  Check(groups.Length==2 && groups.Sum(g=>g.Mesh.vertexCount)==6,"Same-basis batches merge; different transforms remain separate; all particles retained");
  Check(groups.All(g=>g.Timeline.Duration==25 && g.Timeline.DurationClip.length==25) && originalClip.length==8,"Independent Snow loop does not overwrite drawing timeline");
  Check(root.GetComponentsInChildren<MeshRenderer>().All(r=>!r.enabled),"Original quad renderers hidden after compact preparation");
  Debug.Log("OPENBRUSH_SNOW_CHECKS_PASSED maxPositionError="+error);EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
