using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UniGLTF;
// Scalar Ashima 2D simplex reference independent of the GPU shader execution.
// Ashima Arts (2011), MIT; source Electricity equations Tilt Brush, Apache-2.0.
public static class OpenBrushElectricityChecks {
 static void Check(bool ok,string text){if(!ok)throw new InvalidOperationException(text);Debug.Log("Electricity check: "+text);}
 static float Mod(float x)=>x-Mathf.Floor(x/289)*289;
 static float Perm(float x)=>Mod((x*34+1)*x);
 static float Noise(float x,float y){
  const float c=.211324865405187f,b=.366025403784439f,z=-.577350269189626f;
  float ix=Mathf.Floor(x+(x+y)*b),iy=Mathf.Floor(y+(x+y)*b);
  float x0=x-ix+(ix+iy)*c,y0=y-iy+(ix+iy)*c;float ox=x0>y0?1:0,oy=1-ox;
  float[] xs={x0,x0-ox+c,x0+z},ys={y0,y0-oy+c,y0+z},dx={0,ox,1},dy={0,oy,1};float result=0;ix=Mod(ix);iy=Mod(iy);
  for(int k=0;k<3;k++){
   float p=Perm(Perm(iy+dy[k])+ix+dx[k]);float attenuation=Mathf.Max(.5f-xs[k]*xs[k]-ys[k]*ys[k],0);attenuation*=attenuation;attenuation*=attenuation;
   float a=2*(p*.024390243902439f-Mathf.Floor(p*.024390243902439f))-1,h=Mathf.Abs(a)-.5f;a-=Mathf.Floor(a+.5f);
   attenuation*=1.79284291400159f-.85373472095314f*(a*a+h*h);result+=attenuation*(a*xs[k]+h*ys[k]);
  }return result*130;
 }
 static Vector3 N(Vector3 v)=>new Vector3(Noise(v.x,v.y),Noise(v.y,v.z),Noise(v.z,v.x));
 static Vector3 Curl(Vector3 v,float d){var dx=N(v+Vector3.right*d)-N(v-Vector3.right*d);var dy=N(v+Vector3.up*d)-N(v-Vector3.up*d);var dz=N(v+Vector3.forward*d)-N(v-Vector3.forward*d);return new Vector3(dy.z-dz.y,dz.x-dx.z,dx.y-dy.x)/(2*d);}
 static Vector3 Reference(Vector3 p,Vector3 offset,float u,float t,float mod){
  Vector3 flip=new Vector3(-1,1,-1);p=Vector3.Scale(p,flip);offset=Vector3.Scale(offset,flip);float width=offset.magnitude/.02f,e=1-Mathf.Pow(1-Mathf.Sin(u*3.14159f),10);var mid=p-offset;
  var disp=width<1e-8f?Vector3.zero:(Curl(mid/width*(.1f+mod)+Vector3.one*t*3,30)*3+Curl(mid/width*(.2f+mod)+Vector3.one*t*3*1.777f,100)*7)*width*2*e;
  return Vector3.Scale(mid+offset*e+disp,flip);
 }
 public static void Run(){try{
  const string stem="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/Electricity01";
  var raw=JsonUtility.FromJson<OpenBrushElectricityImport.Data>(File.ReadAllText(stem+"-motion-v1.json"));var p=raw.parts[0];float error=0;
  using(var gpu=new OpenBrushElectricityImport.Deformer(p)){
   foreach(float time in new[]{0f,.137f,2.75f})foreach(float mod in new[]{1f,1.333f,1.77f}){
    var positions=gpu.Evaluate(time,mod);Check(positions.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),"finite GPU displacement");
    for(int i=0;i<p.positions.Length;i+=11)error=Mathf.Max(error,(positions[i]-Reference(p.positions[i],p.offsets[i],p.uv[i].x,time,mod)).magnitude);
   }
   var untapered=gpu.Evaluate(.3f,1,0);
   float envelopeError=0;for(int i=0;i<p.positions.Length;i++){float e=1-Mathf.Pow(1-Mathf.Sin(p.uv[i].x*3.14159f),10);envelopeError=Mathf.Max(envelopeError,(untapered[i]-(p.positions[i]-p.offsets[i]+p.offsets[i]*e)).magnitude);}
   Check(envelopeError<1e-5f,"GPU texel ordering and ribbon envelope");
  }
  Debug.Log("Electricity GPU/scalar maximum meters="+error);Check(error<.003f,"original curl noise, coordinate reflections and time agree with scalar reference");
  using(var data=new AutoGltfFileParser(stem+"-growth-v1.glb").Parse())using(var importer=new ImporterContext(data)){
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   OpenBrushMaterialImport.Prepare(root,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(stem+"-growth-v1.materials.json")),false);
   var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(stem+"-growth-v1.timeline.json"));OpenBrushGrowthImport.Prepare(root,timing,null);
   OpenBrushElectricityImport.Prepare(root,raw,timing,new[]{0f,1.5f,8.5f,8.6f});var motion=root.GetComponentInChildren<OpenBrushGeometryMotion>();
   Check(motion.Frames[0].bounds.size==Vector3.zero,"blank lead-in has no visible geometry");
   Check(motion.Frames[1].vertexCount>3 && motion.Frames[1].vertexCount<motion.Frames[2].vertexCount,"drawing clips moving triangles progressively");
   Check(motion.Frames[2].vertexCount==p.triangles.Length*3,"all three original shader passes survive at completion");
   var a=motion.Frames[2].vertices;var b=motion.Frames[3].vertices;Check(a.Zip(b,(x,y)=>(x-y).magnitude).Average()>.001f,"completed geometry keeps moving");
   Check(root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushElectricityImport.Guid).All(r=>!r.enabled),"undeformed fallback renderers hidden");
   Check(ComponentConverterRepository.TryGetConverter(typeof(OpenBrushGeometryMotion))?.Type==typeof(OpenBrushGeometryMotionConverter),"SDK discovers native timed geometry converter");
   var atlas=(Texture2D)motion.SourceMaterial.mainTexture;File.WriteAllBytes(".local/electricity-surface.png",atlas.EncodeToPNG());
   Check(atlas.GetPixels().Any(c=>c.r>c.g*2 && c.r>c.b*2) && atlas.GetPixels().Any(c=>c.b>c.g*2 && c.b>c.r*2),"red and blue survive surface baking");
  }
  Debug.Log("OPENBRUSH_ELECTRICITY_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
