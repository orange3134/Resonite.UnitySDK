using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushRainbowChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Rainbow check: "+why);}
 static Color Interpolate(Color[] c,float x,float y)=>x+y<=1?c[0]*(1-x-y)+c[1]*x+c[2]*y:c[1]*(1-y)+c[2]*(1-x)+c[3]*(x+y-1);
 // Independent scalar translation of normal Rainbow.shader (non-audio).
 static Color Reference(Color[] colors,float x,float y,float seconds){
  var c=Interpolate(colors,x,y);double v=Math.Min(1,Math.Max(0,y))*5;
  int shift=(int)Math.Floor(((seconds*.4)%1)*5),row=((int)Math.Floor(v)+shift)%5;
  var hues=new[]{new Color(1,0,0),new Color(.7f,.3f,0),new Color(0,1,0),new Color(0,.2f,1),new Color(.4f,0,1.2f)};
  double band=v%1,gain=Math.Pow((Math.Sin(row+seconds*2)+1)/2,5)*Math.Min(1,Math.Pow(band*(1-band)*5,50))*Math.Exp(.65*3);
  var result=Color.black;
  for(int k=0;k<3;k++){
   double rgb=c[k]*hues[row][k]*gain;
   double decoded=rgb>=1?rgb:rgb*(rgb*(rgb*.305306011+.682171111)+.012522878);
   result[k]=(float)Math.Max(0,1.055*Math.Pow(Math.Min(decoded,4)/4,1/2.4)-.055);
  }return result;
 }
 public static void Run(){try{
  var root=new GameObject("Rainbow scalar check");root.AddComponent<OpenBrushTimeline>().Duration=2.6f;
  var child=new GameObject("Rainbow_stroke_0000_part_000");child.transform.SetParent(root.transform,false);
  var colors=new[]{new Color(.8f,.05f,.03f,.0f),new Color(.1f,.1f,.9f,1),new Color(.04f,.7f,.12f,.6f),new Color(.8f,.8f,.8f,1)};
  var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.one},uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},colors=colors,triangles=new[]{0,1,2,2,1,3,2,1,0,3,1,2}};
  child.AddComponent<MeshFilter>().sharedMesh=mesh;
  var material=new Material(Shader.Find("OpenBrush/ExportedBrush"));material.SetOverrideTag("OpenBrushGuid",OpenBrushFlowImport.Rainbow);
  child.AddComponent<MeshRenderer>().sharedMaterial=material;
  float[] times={0,.49f,.51f,1.01f,2.49f,2.51f};
  OpenBrushFlowImport.PrepareSamples(root,times,1,4);var motion=root.GetComponentInChildren<OpenBrushTextureMotion>();
  Check(motion.FrameTimes.SequenceEqual(times),"absolute shader times preserved across row-shift boundaries");
  float maximum=0;int bright=0;
  for(int f=0;f<times.Length;f++)for(int y=2;y<126;y+=3)for(int x=2;x<126;x+=3){
   var actual=motion.Frames[f].GetPixel(x+2,y+2);var expected=Reference(colors,x/127f,y/127f,times[f]);
   for(int k=0;k<3;k++){maximum=Mathf.Max(maximum,Mathf.Abs(actual[k]-expected[k]));if(expected[k]>.2f)bright++;}
  }
  Debug.Log("Rainbow scalar maximum error="+maximum+" bright samples="+bright);
  Check(maximum<.015f && bright>500,"GPU matches source tint, thin bands, pulse and ignored alpha; duplicate faces do not amplify");
  Check(!child.GetComponent<MeshRenderer>().enabled && child.GetComponentsInChildren<MeshFilter>().Last().sharedMesh.triangles.Length==6,"coincident backfaces merged without changing source geometry");
  Check(motion.Frames[0].GetPixels32().Zip(motion.Frames[2].GetPixels32(),(a,b)=>!a.Equals(b)).Count(v=>v)>500,"rainbow changes over time");
  const string stem="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/Rainbow01-growth-v1";
  using(var data=new AutoGltfFileParser(stem+".glb").Parse())using(var importer=new ImporterContext(data)){
   var instance=importer.Load();instance.ShowMeshes();var model=instance.gameObject;
   OpenBrushMaterialImport.Prepare(model,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(stem+".materials.json")),false);
   model.AddComponent<OpenBrushTimeline>().Duration=10.8f;
   OpenBrushFlowImport.PrepareSamples(model,new[]{8.1f,8.7f},1,4);
   var sequence=model.GetComponentInChildren<OpenBrushTextureMotion>();
   for(int i=0;i<2;i++)File.WriteAllBytes(".local/rainbow-fixture-"+i+".png",sequence.Frames[i].EncodeToPNG());
  }
  Debug.Log("OPENBRUSH_RAINBOW_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
