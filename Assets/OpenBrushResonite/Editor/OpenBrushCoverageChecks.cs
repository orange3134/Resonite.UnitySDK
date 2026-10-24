using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class OpenBrushCoverageChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Coverage check: "+why);}
 public static void Run(){try{
  Check(OpenBrushProceduralImport.IsWiggly("e814fef1-97fd-7194-4a2f-50c2bb918be2"),"legacy Graphite routes through stepped animation");
  Check(OpenBrushProfiles.All.Select(p=>p.Guid).Distinct().Count()==OpenBrushProfiles.All.Length,"exact GUIDs are unique");
  var offsets=new[]{new Vector3(-.01f,-.01f,0),new Vector3(.01f,-.01f,0),new Vector3(0,.01f,0)};
  var positions=offsets.Select(o=>o+new Vector3(-.4f,1.3f,.7f)).ToArray();
  var part=new OpenBrushElectricityImport.Part{positions=positions,offsets=offsets,colors=Enumerable.Repeat(new Color(.2f,.5f,.8f,1),3).ToArray()};
  using(var d=new OpenBrushWaveformParticleImport.Deformer(part,new float[3])) {
   var zero=d.Evaluate(0);var a=d.Evaluate(5);var b=d.Evaluate(10.123f);
   Check(zero.Zip(positions,(x,y)=>(x-y).magnitude).Max()<1e-6,"zero-age particles retain source positions");
   Check(a.Zip(b,(x,y)=>(x-y).magnitude).Max()>.0001,"normal curl changes with time");
   foreach(var frame in new[]{a,b}){
    Check(frame.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"finite curl positions");
    Check(Mathf.Abs((frame[1]-frame[0]).magnitude-.02f)<1e-5,"particle width is preserved at different ages");
    Check(frame.Zip(positions,(x,y)=>(x-y).magnitude).Max()<.4,"source decimetres do not produce metre-sized displacement");
   }
  }
  var root=new GameObject("Tube check");var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=4;
  var child=new GameObject("WaveformTube_stroke_0000_part_000");child.transform.SetParent(root.transform,false);
  var texture=new Texture2D(4,4,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Repeat};texture.SetPixels(Enumerable.Repeat(new Color(.8f,.2f,.1f,.5f),16).ToArray());texture.Apply();
  var material=new Material(Shader.Find("OpenBrush/ExportedBrush")){mainTexture=texture};material.SetOverrideTag("OpenBrushGuid",OpenBrushWaveformImport.TubeGuid);
  var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},uv=new[]{Vector2.zero,Vector2.right,Vector2.up},colors=Enumerable.Repeat(Color.white,3).ToArray(),triangles=new[]{0,1,2}};child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;
  OpenBrushWaveformImport.Prepare(root);var motion=root.GetComponentsInChildren<OpenBrushTextureMotion>().Single();
  Check(motion.Frames.Length==2 && motion.FrameTimes.SequenceEqual(new[]{0f,4f}),"no-audio Tube has only two endpoint frames, not hundreds of duplicate textures");
  Check(motion.Frames[0].GetPixels32().SequenceEqual(motion.Frames[1].GetPixels32()),"Tube no-audio texture stays constant");
  Check(motion.Frames[0].GetPixels().Max(c=>c.r)>.1f,"Tube includes original texture and emission");
  UnityEngine.Object.DestroyImmediate(root);
  Debug.Log("OPENBRUSH_COVERAGE_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
