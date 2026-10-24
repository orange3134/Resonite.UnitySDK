// BubbleWand normal ShaderGraph equations, Open Brush Unity Tools (Apache-2.0).
// Source N dot V uses true per-fragment view; native Matcap approximates it
// using camera-forward normals. Geometry is evaluated from the original radius
// sidecar, including the bulge and curl noise, before time-band clipping.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class OpenBrushBubbleWandAppearance {
 public const string Guid="eba3f993-f9a1-4d35-b84e-bb08f48981a4";
 public const int Resolution=128,FrameCount=120;
 public const float Period=20;
 public static bool Supports(string guid)=>guid==Guid;
 public static float Rim(float cosine) {
  float edge=1-Mathf.Clamp01(Mathf.Abs(cosine));return edge*(1-Mathf.Pow(edge,5));
 }
 public static Color Shade(Texture2D pattern,Color paint,float cosine,float seconds) {
  float rim=Rim(cosine);
  // Graph Normal(Tangent).G is zero: both UV components start at rim.
  // BrushTime output0 is seconds; the source adds time*.05 only to U.
  var t=OpenBrushTextureSampling.SampleRaw(pattern,rim+seconds/Period,rim);
  return OpenBrushFireImport.DisplayEmission(new Color(
   rim*t.r*(.25f*rim+.75f*paint.r),rim*t.g*(.25f*rim+.75f*paint.g),rim*t.b*(.25f*rim+.75f*paint.b),1));
 }
 public static Texture2D[] Bake(Texture2D pattern,Color paint) {
  if(pattern==null||!pattern.isReadable)throw new InvalidOperationException("BubbleWand needs its readable source pattern");
  var frames=new Texture2D[FrameCount];
  for(int f=0;f<FrameCount;f++) {
   var pixels=new Color[Resolution*Resolution];
   for(int y=0;y<Resolution;y++)for(int x=0;x<Resolution;x++) {
    float nx=2*(x+.5f)/Resolution-1,ny=2*(y+.5f)/Resolution-1;
    float cosine=Mathf.Sqrt(Mathf.Max(0,1-nx*nx-ny*ny));
    
    pixels[y*Resolution+x]=Shade(pattern,paint,cosine,f*Period/FrameCount);
   }
   var texture=new Texture2D(Resolution,Resolution,TextureFormat.RGBA32,true,true){name="BubbleWandView_"+paint+"_"+f,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
   texture.SetPixels(pixels);texture.Apply(true,false);frames[f]=texture;
  }
  return frames;
 }
 public static (Material body,Material film) Prepare(GameObject root,Material source,Color paint) {
  var frames=Bake(source.mainTexture as Texture2D,paint);
  var body=new Material(source){name="BubbleWand Reflective Body",mainTexture=null};
  body.SetColor("_TintColor",paint);body.SetOverrideTag("OpenBrushBubbleWandBody","1");
  var film=new Material(source){name="BubbleWand Animated Film",mainTexture=frames[0]};
  film.SetOverrideTag("OpenBrushViewEmission","1");
  var clock=new GameObject("BubbleWand - Film Clock");clock.transform.SetParent(root.transform,false);
  var motion=clock.AddComponent<OpenBrushTextureMotion>();motion.SourceMaterial=film;motion.Frames=frames;motion.FramesPerSecond=FrameCount/Period;motion.TextureScale=Vector2.one;
  return (body,film);
 }
}
