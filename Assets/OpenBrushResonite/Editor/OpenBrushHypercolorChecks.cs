using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
public static class OpenBrushHypercolorChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Hypercolor check: "+why);}
 static float Encode(float v)=>(float)(v<=.0031308?12.92*v:1.055*Math.Pow(v,1/2.4)-.055);
 static float Decode(float v)=>(float)(v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4));
 public static void Run(){try{
  var root=new GameObject("Hypercolor reference");root.AddComponent<OpenBrushTimeline>().Duration=.7f;
  var child=new GameObject("Hypercolor_stroke_0000_part_000");child.transform.SetParent(root.transform,false);
  var colors=new[]{new Color(.8f,.05f,.03f,0),new Color(.1f,.1f,.9f,1),new Color(.04f,.7f,.12f,.6f),new Color(.8f,.8f,.8f,1)};
  var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,new Vector3(1,1,0)},uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},colors=colors,triangles=new[]{0,1,2,2,1,3},normals=Enumerable.Repeat(Vector3.back,4).ToArray()};
  child.AddComponent<MeshFilter>().sharedMesh=mesh;var material=new Material(Shader.Find("OpenBrush/ExportedBrush"));material.SetOverrideTag("OpenBrushGuid",OpenBrushHypercolorImport.Guid);child.AddComponent<MeshRenderer>().sharedMaterial=material;
  OpenBrushHypercolorImport.PrepareSamples(root,new[]{0f,.35f,.7f},1);
  var motion=root.GetComponentsInChildren<OpenBrushTextureMotion>();var diffuse=motion.Single(m=>!m.SpecularOnly);var spec=motion.Single(m=>m.SpecularOnly);
  var tex=new Texture2D(2,2,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Clamp};tex.LoadImage(File.ReadAllBytes("Assets/OpenBrushResonite/SourceTextures/Hypercolor_BaseMap.png"));
  float maximum=0;int samples=0;string worst="";
  for(int f=0;f<3;f++)for(int y=2;y<126;y+=5)for(int x=2;x<126;x+=5){
   float u=x/127f,v=y/127f,t=diffuse.FrameTimes[f];var c=u+v<=1?colors[0]*(1-u-v)+colors[1]*u+colors[2]*v:colors[1]*(1-v)+colors[2]*(1-u)+colors[3]*(u+v-1);
   var source=OpenBrushTextureSampling.SampleRaw(tex,u,v);
   float r=(float)(2*(Math.Sin(source.r*2+t-u)+1));float g=(float)(2*(Math.Sin(r*3.3+2*t-u)+1));float b=(float)(2*(Math.Sin(r*4.66+.5*t-u)+1));
   var anim=new[]{r,g,b};var actual=diffuse.Frames[f].GetPixel(x+2,y+2);var reflect=spec.Frames[f].GetPixel(x+2,y+2);
   for(int k=0;k<3;k++){float d=Mathf.Abs(actual[k]-Encode(Mathf.Clamp01(Decode(anim[k]*c[k])/32))),q=Mathf.Abs(reflect[k]-Encode(Mathf.Clamp01(anim[k]*.2352941f)));if(Mathf.Max(d,q)>maximum)worst="f="+f+" x="+x+" y="+y+" channel="+k+" pattern="+source+" tint="+c+" anim="+string.Join(",",anim)+" actual="+actual+" spec="+reflect+" d="+d+" q="+q;maximum=Mathf.Max(maximum,d,q);samples++;}
   maximum=Mathf.Max(maximum,Mathf.Abs(actual.a-source.a),Mathf.Abs(reflect.a-.4f));
  }
  Debug.Log("Worst sample: "+worst);File.WriteAllBytes(".local/hypercolor-reference.png",diffuse.Frames[0].EncodeToPNG()); Debug.Log("Hypercolor scalar maximum="+maximum+" samples="+samples);Check(maximum<.025f,"source sequential color equations, tint decode, mask and animated specular match GPU");
  Check(diffuse.Frames[0].GetPixel(0,0).a==0,"uncovered atlas pixels are transparent, preventing triangle outlines");
  Check(diffuse.FrameTimes.SequenceEqual(spec.FrameTimes),"both texture channels follow identical absolute times");
  Check(child.GetComponentsInChildren<MeshFilter>().Last().sharedMesh.tangents.Length==4,"normal-map tangent basis exists");
  const string stem="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/Hypercolor01-growth-v1";
  using(var data=new AutoGltfFileParser(stem+".glb").Parse())using(var importer=new ImporterContext(data)){
   var instance=importer.Load();instance.ShowMeshes();var model=instance.gameObject;
   OpenBrushMaterialImport.Prepare(model,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(stem+".materials.json")),false);model.AddComponent<OpenBrushTimeline>().Duration=10.8f;
   OpenBrushHypercolorImport.PrepareSamples(model,new[]{8.1f,8.7f},1);var seq=model.GetComponentsInChildren<OpenBrushTextureMotion>();
   foreach(var channel in seq)File.WriteAllBytes(".local/hypercolor-"+(channel.SpecularOnly?"specular":"diffuse")+".png",channel.Frames[0].EncodeToPNG());
   Check(model.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)==80,"all growth pieces replaced with lit animated surfaces");
  }
  Debug.Log("OPENBRUSH_HYPERCOLOR_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}





