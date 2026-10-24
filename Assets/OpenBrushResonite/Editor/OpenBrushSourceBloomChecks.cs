using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;

public static class OpenBrushSourceBloomChecks {
 const string Dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bloom-gradient-v1/";
 static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Debug.Log("SourceBloom: "+message);}
 static Texture2D[] Bake(string name,bool reference=false,bool simulateOldBug=false,int scale=1) {
  using(var data=new AutoGltfFileParser(Dir+name+".glb").Parse())using(var importer=new ImporterContext(data)) {
   var root=importer.Load().gameObject;
   var originals=root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh).ToArray();
   var colors=originals.Select(m=>m.colors).ToArray();
   if(reference || simulateOldBug) {
    if(reference)Check(originals.Length==2,"reference has only two unsplit original triangles per brush");
    foreach(var mesh in originals) {
     // Independent source-shader vertex calculation on the UNSPLIT reference.
     mesh.SetUVs(2,mesh.colors.Select(c=>{
      float m=Mathf.Sqrt(c.r*c.r+c.g*c.g+c.b*c.b)*.05f;
      return new Vector4(Mathf.Pow(Mathf.Max(c.r,m),2.2f),Mathf.Pow(Mathf.Max(c.g,m),2.2f),Mathf.Pow(Mathf.Max(c.b,m),2.2f),Mathf.Pow(c.a,2.2f));
     }).ToArray());
    }
   } else {
    bool refused=false;try{OpenBrushSourceBloom.Read(originals[0]);}catch(InvalidOperationException e){refused=e.Message.Contains("regenerate");}
    Check(refused,"legacy gradient bundle requires regeneration");
    Check(OpenBrushSourceBloom.Restore(importer)==originals.Length,"restored every section's source varying");
    var left=new Vector4(Mathf.Pow(.1f,2.2f),Mathf.Pow(.02f,2.2f),Mathf.Pow(.3f,2.2f),Mathf.Pow(.15f,2.2f));
    var right=new Vector4(Mathf.Pow(.8f,2.2f),Mathf.Pow(new Vector3(.8f,.3f,.1f).magnitude*.05f,2.2f),Mathf.Pow(.1f,2.2f),Mathf.Pow(.95f,2.2f));
    right.y=Mathf.Pow(.3f,2.2f);
    float maxError=0;
    foreach(var mesh in originals) {
     var b=OpenBrushSourceBloom.Read(mesh);var uv=mesh.uv;
     for(int v=0;v<b.Length;v++)maxError=Mathf.Max(maxError,(b[v]-Vector4.Lerp(left,right,uv[v].x)).magnitude);
    }
    Check(maxError<2e-7f,"imported varying equals independent source interpolation before rasterization: "+maxError);
   }
   var manifest=JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(Dir+name+".materials.json"));
   OpenBrushMaterialImport.Prepare(root,manifest,false);
   root.AddComponent<OpenBrushTimeline>().Duration=1;
   OpenBrushFlowImport.PrepareSamples(root,new[]{0f,.173f,.631f},scale,16);
   Check(originals.Select((m,i)=>m.colors.SequenceEqual(colors[i])).All(x=>x),"original RGBA survives separately from bloom varying");
   var frames=root.GetComponentInChildren<OpenBrushTextureMotion>().Frames;
   return frames;
  }
 }
 public static void Run(){try {
  var reference=Bake("coarse",true);
  int totalChanged=0;
  foreach(string name in new[]{"coarse","medium","fine"}) {
   var frames=Bake(name);
   for(int f=0;f<frames.Length;f++) {
    var a=reference[f].GetPixels32();var b=frames[f].GetPixels32();
    int changed=0,max=0;double squared=0;
    for(int i=0;i<a.Length;i++) {
     int d=Math.Max(Math.Abs(a[i].r-b[i].r),Math.Max(Math.Abs(a[i].g-b[i].g),Math.Abs(a[i].b-b[i].b)));
     max=Math.Max(max,d);if(d>2)changed++;
     squared+=(a[i].r-b[i].r)*(a[i].r-b[i].r)+(a[i].g-b[i].g)*(a[i].g-b[i].g)+(a[i].b-b[i].b)*(a[i].b-b[i].b);
    }
    double rms=Math.Sqrt(squared/(a.Length*3));
    Debug.Log("BLOOM_COMPARE "+name+" frame="+f+" max="+max+" pixelsOver2="+changed+" RMS="+rms);
    File.WriteAllBytes(".local/bloom-gradient-"+name+"-"+f+".png",frames[f].EncodeToPNG());
    totalChanged+=changed;
    // D3D snaps raster positions to subpixels. At UV chart seams this can
    // shift a few steep shader edges; the varying accuracy is checked above.
    Check(changed<a.Length*.001 && rms<.3,"at least 99.9% of atlas pixels agree within two byte levels, RMS below .3");
   }
  }
  var wrong=Bake("fine",false,true);
  int wrongChanged=reference[1].GetPixels32().Zip(wrong[1].GetPixels32(),(a,b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)))>2).Count(x=>x);
  Check(wrongChanged>1000,"regression would detect the old post-clipping bloom calculation: "+wrongChanged+" pixels");
  var largeRef=Bake("coarse",true,false,4);var largeFine=Bake("fine",false,false,4);
  int largeChanged=largeRef[1].GetPixels32().Zip(largeFine[1].GetPixels32(),(a,b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)))>2).Count(x=>x);
  Debug.Log("BLOOM_SUBPIXEL_PROBE 4x pixelsOver2="+largeChanged+" fraction="+(largeChanged/(double)(largeRef[1].width*largeRef[1].height)));
  var constant=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up},colors=new[]{Color.red,Color.red,Color.red},triangles=new[]{0,1,2}};
  Check(OpenBrushSourceBloom.Read(constant).Length==3,"constant-color legacy bundles remain compatible");
  Debug.Log("OPENBRUSH_SOURCE_BLOOM_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
