using System;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class OpenBrushDotsChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Dots check: "+why);}
 public static void Run(){try {
  var root=new GameObject("Dots checks");var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=120;
  var particles=new[]{0f,.25f,1f}.Select((alpha,i)=>new OpenBrushParticleMotion.Particle{
   center=new Vector3(i,2,3),origin=new Vector3(-10,-10,-10),size=.1f,rotation=.3f,uv=new Vector4(0,0,1,1),color=new Color(.2f,.4f,.8f,alpha),playBirth=1+i*.237f}).ToArray();
  var source=new Texture2D(2,2,TextureFormat.RGBA32,false,false);source.SetPixels(new[]{new Color(.1f,.7f,.2f,0),new Color(.8f,.3f,.6f,.5f),Color.white,Color.black});source.Apply();
  var material=new Material(Shader.Find("OpenBrush/ExportedBrush"));material.mainTexture=source;
  OpenBrushDotsAppearance.Prepare(root.transform,particles,material,timeline);
  var layers=root.GetComponentsInChildren<OpenBrushParticleMotion>();Check(layers.Length==2,"base and emission layers");
  var plain=layers.Single(l=>l.Kind==OpenBrushParticleMotion.BrushKind.DotsBase);var glow=layers.Single(l=>l.Kind==OpenBrushParticleMotion.BrushKind.DotsEmission);
  Check(plain.FrameCount==5 && plain.ExplicitTimes.SequenceEqual(glow.ExplicitTimes),"two-minute hold needs only five birth-event frames per layer");
  Check(plain.Seconds(2)==particles[1].playBirth,"birth time retains sub-frame precision");
  Check(plain.Frames[0].normals.All(n=>n.x==0),"initially blank");
  Check(plain.Frames[2].normals[0].x==.1f && plain.Frames[2].normals[1].x==.1f && plain.Frames[2].normals[2].x==0,"each particle appears at its birth");
  Check(plain.Frames[1].vertices[0]==particles[0].center,"Dots is stationary, without unrelated exponential spread");
  Check(plain.Frames[4].normals[0]==new Vector3(.1f,.1f,.3f),"point size and rotation retained");
  Check(layers.All(l=>!l.BakedTexture.isDataSRGB),"native sprite samples stay raw");
  float error=0;
  foreach(var i in Enumerable.Range(0,particles.Length))foreach(var t in Enumerable.Range(0,4)) {
   var tex=source.GetPixels()[t];var paint=particles[i].color;var c=paint*.4f*tex;var expected=c+c*(c.a*300);
   var a=plain.Frames[4].colors[i]*plain.BakedTexture.GetPixels()[t];var b=glow.Frames[4].colors[i]*glow.BakedTexture.GetPixels()[t];
   var actual=a*a.a+b*b.a;
   for(int k=0;k<3;k++)error=Mathf.Max(error,Mathf.Abs(expected[k]-actual[k]));
  }
  Check(error<.0001,"two-layer output matches scalar source fragment including alpha zero and one; error="+error);
  Check(glow.Frames[4].colors[0].r==0 && glow.Frames[4].colors[0].a==1 && plain.Frames[4].colors[0].r>0,"zero random alpha retains base color");
  Check(MeshConverter.ConvertMesh(glow.Frames[4]).Colors[2].b>30,"emission HDR survives raw mesh transport");
  var staticRoot=new GameObject("Static Dots");OpenBrushDotsAppearance.Prepare(staticRoot.transform,particles,material,null);
  Check(staticRoot.GetComponentsInChildren<OpenBrushParticleMotion>().All(l=>l.FrameCount==1 && l.Frames[0].normals.All(n=>n.x>0)),"static Dots uses one fully visible mesh per layer");
  Debug.Log("OPENBRUSH_DOTS_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}

