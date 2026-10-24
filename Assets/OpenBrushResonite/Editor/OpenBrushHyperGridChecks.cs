using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class OpenBrushHyperGridChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("HyperGrid check: "+why);}
 static double Source(double p,double size,double age)=>Math.Ceiling(p*(.5/size+5*Math.Max(0,Math.Min(1,1-age*10))))/(.5/size+5*Math.Max(0,Math.Min(1,1-age*10)));
 public static void Run(){try {
  float max=0;int changes=0;
  foreach(float x in new[]{-.131f,-.031f,.001f,.079f,.143f})foreach(float size in new[]{.007f,.013f,.023f})foreach(float age in new[]{0f,.017f,.057f,.1f,3f}) {
   var p=new Vector3(x,1.803f,-.013f);var o=new Vector3(size,0,0);var actual=OpenBrushHyperGridImport.Quantize(p,o,age);
   var expected=new Vector3((float)-Source(-p.x*10,size*10,age)/10,(float)Source(p.y*10,size*10,age)/10,(float)-Source(-p.z*10,size*10,age)/10);
   max=Mathf.Max(max,Vector3.Distance(actual,expected));
   if(actual!=OpenBrushHyperGridImport.Quantize(p,o,3))changes++;
  }
  Check(max<.00001 && changes>10,"positive/negative canvas coordinates, decimeter conversion, release and mature grid agree with scalar source; max="+max);
  var part=new OpenBrushHyperGridImport.Part{positions=new[]{new Vector3(-.13f,1.8f,-.02f),new Vector3(-.11f,1.8f,-.02f),new Vector3(-.13f,1.82f,-.02f),new Vector3(-.11f,1.82f,-.02f)},offsets=new[]{new Vector3(-.01f,-.01f,0),new Vector3(.01f,-.01f,0),new Vector3(-.01f,.01f,0),new Vector3(.01f,.01f,0)},uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},colors=Enumerable.Repeat(new Color(.8f,.1f,.3f,.6f),4).ToArray(),triangles=new[]{0,1,2,2,1,3}};
  var birth=new[]{1.017f};var times=OpenBrushHyperGridImport.SampleTimes(birth,120);
  Check(times.Length==9 && times[1]==birth[0] && times.Last()==120,"birth plus six release samples; long held drawing adds no redundant frames");
  var empty=OpenBrushHyperGridImport.Frame(part,birth,1);var born=OpenBrushHyperGridImport.Frame(part,birth,birth[0]);var mature=OpenBrushHyperGridImport.Frame(part,birth,birth[0]+.1f);var later=OpenBrushHyperGridImport.Frame(part,birth,100);
  Check(empty.triangles.Distinct().Count()==1 && born.triangles.Length==6,"whole original quad appears at its timestamp");
  Check(mature.vertices.SequenceEqual(later.vertices),"non-audio grid is stationary after release");
  Check(born.uv[0]==Vector2.up && born.colors[0]==new Color(1.92f,.24f,.72f,1),"original texture orientation and source RGB-times-alpha energy");
  var raw=MeshConverter.ConvertMesh(born);Check(Mathf.Abs(raw.Colors[0].r-1.92f)<1e-6 && Mathf.Abs(raw.Colors[0].a-1)<1e-6,"HDR vertex channels survive SDK mesh transport");
  var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,false);texture.SetPixels(new[]{new Color(.05f,.3f,.8f,.5f),Color.white,new Color(.8f,.1f,.4f,0),Color.black});texture.Apply();
  var source=new Material(Shader.Find("OpenBrush/ExportedBrush"));source.mainTexture=texture;
  var surface=OpenBrushHyperGridSurface.Build(part,source,out var mappedUv);var atlas=(Texture2D)surface.mainTexture;
  float surfaceError=0;
  for(int y=0;y<2;y++)for(int x=0;x<2;x++) {
   var tex=texture.GetPixel(x,y);var paint=part.colors[0];var expected=paint*tex*(4*paint.a*tex.a);var actual=atlas.GetPixel(x+texture.width/2,y+texture.height/2);
   for(int k=0;k<3;k++){double c=expected[k];double reference=c>=1?c:c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);surfaceError=Mathf.Max(surfaceError,(float)Math.Abs(reference-actual[k]));}
  }
  Check(atlas.mipmapCount>1 && Mathf.IsPowerOfTwo(atlas.width) && Mathf.IsPowerOfTwo(atlas.height),"atlas has mipmaps and power-of-two repeat gutters");
  Check(surfaceError<.002 && atlas.format==TextureFormat.RGBAHalf && !atlas.isDataSRGB,"complete source fragment and post-product color transfer; max="+surfaceError);
  var baked=OpenBrushHyperGridImport.Frame(part,birth,2,mappedUv);Check(baked.colors.All(c=>c==Color.white) && baked.uv.SequenceEqual(mappedUv),"baked surface receives no second paint/alpha multiplication");
  Debug.Log("OPENBRUSH_HYPERGRID_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}


