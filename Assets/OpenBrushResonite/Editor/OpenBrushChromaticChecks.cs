using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
// Independent scalar evaluation of the normal ChromaticWave shader, including
// vertex-stage bloom interpolation, alpha, _Time.w and source color frequencies.
public static class OpenBrushChromaticChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Chromatic check: "+why);}
 static Color Bloom(Color c){float low=new Vector3(c.r,c.g,c.b).magnitude*.05f;return new Color(Mathf.Pow(Mathf.Max(c.r,low),2.2f)*2*Mathf.Exp(4.5f),Mathf.Pow(Mathf.Max(c.g,low),2.2f)*2*Mathf.Exp(4.5f),Mathf.Pow(Mathf.Max(c.b,low),2.2f)*2*Mathf.Exp(4.5f),Mathf.Pow(c.a,2.2f));}
 static Color Interpolate(Color[] c,float x,float y)=>x+y<=1?c[0]*(1-x-y)+c[1]*x+c[2]*y:c[1]*(1-y)+c[2]*(1-x)+c[3]*(x+y-1);
 static Color Reference(Color[] original,float x,float y,float t){
  var c=Interpolate(original,x,y);var b=Interpolate(original.Select(Bloom).ToArray(),x,y);
  float v=(y+4*x)%1;var output=Color.black;
  for(int k=0;k<3;k++){
   float wave=.15f*Mathf.Sin(-(20+10*k)*c[k]*t*3+x*100*c[k]);
   float rgb=b[k]*b.a*Mathf.Clamp01(1-40*Mathf.Abs(v-.5f+wave));
   // UnityCG's fast sRGB decode polynomial, with HDR left unchanged.
   float decoded=rgb>=1?rgb:rgb*(rgb*(rgb*.305306011f+.682171111f)+.012522878f);
   // Preserve spectral intensity ratios in the bounded native HDR range.
   decoded*=Mathf.Min(1,4/Mathf.Max(Mathf.Max(b.r,Mathf.Max(b.g,b.b))*b.a,1e-6f));
   float encoded=1.055f*Mathf.Pow(Mathf.Min(decoded,4)/4,1/2.4f)-.055f;
   output[k]=Mathf.Max(0,encoded);
  }return output;
 }
 public static void Run(){try{
  var root=new GameObject("Chromatic reference check");var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=.1f;
  var child=new GameObject("Chromatic_stroke_0000_part_000");child.transform.SetParent(root.transform,false);
  var colors=new[]{new Color(.8f,.05f,.03f,.2f),new Color(.1f,.1f,.9f,1),new Color(.04f,.7f,.12f,.6f),new Color(.8f,.8f,.8f,1)};
  var mesh=new Mesh{vertices=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(0,1,0),new Vector3(1,1,0)},uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},colors=colors,triangles=new[]{0,1,2,2,1,3}};
  child.AddComponent<MeshFilter>().sharedMesh=mesh;
  var material=new Material(Shader.Find("OpenBrush/ExportedBrush"));material.SetOverrideTag("OpenBrushGuid",OpenBrushFlowImport.ChromaticWave);
  child.AddComponent<MeshRenderer>().sharedMaterial=material;
  OpenBrushFlowImport.Prepare(root);var motion=root.GetComponentInChildren<OpenBrushTextureMotion>();
  Check(motion.Frames.Length==7 && motion.FramesPerSecond==60 && motion.FrameTimes.Last()==.1f,"60 fps samples include the endpoint");
  float maximum=0;int bright=0;
  for(int f=0;f<7;f+=3)for(int y=2;y<126;y+=3)for(int x=2;x<126;x+=3){
   var actual=motion.Frames[f].GetPixel(x+2,y+2);var expected=Reference(colors,x/127f,y/127f,motion.FrameTimes[f]);
   for(int k=0;k<3;k++){maximum=Mathf.Max(maximum,Mathf.Abs(actual[k]-expected[k]));if(expected[k]>.2f)bright++;}
  }
  Debug.Log("Chromatic scalar maximum error="+maximum+" bright samples="+bright);
  Check(maximum<.025f && bright>50,"GPU bake matches source scalar reference for nonuniform color and alpha");
  Check(mesh.colors.SequenceEqual(colors) && child.GetComponent<MeshRenderer>().enabled==false,"source colors retained and original surface hidden");
  Check(motion.Frames[0].GetPixels32().Zip(motion.Frames[6].GetPixels32(),(a,b)=>!a.Equals(b)).Count(v=>v)>500,"colored waves move over time");
  File.WriteAllBytes(".local/chromatic-reference-0.png",motion.Frames[0].EncodeToPNG());
  SaveFixtureProbe();
  Debug.Log("OPENBRUSH_CHROMATIC_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static void SaveFixtureProbe(){
  const string stem="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/Chromatic01-growth-v1";
  using(var data=new AutoGltfFileParser(stem+".glb").Parse())using(var importer=new ImporterContext(data)){
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   OpenBrushMaterialImport.Prepare(root,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(stem+".materials.json")),false);
   root.AddComponent<OpenBrushTimeline>().Duration=10.8f;
   OpenBrushFlowImport.PrepareSamples(root,new[]{8.5f,8.6f},1,4);
   var motion=root.GetComponentInChildren<OpenBrushTextureMotion>();
   for(int i=0;i<2;i++)File.WriteAllBytes(".local/chromatic-ratio-"+i+".png",motion.Frames[i].EncodeToPNG());
  }
 }
}
