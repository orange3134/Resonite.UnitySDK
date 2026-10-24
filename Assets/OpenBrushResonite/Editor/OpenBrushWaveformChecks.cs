using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class OpenBrushWaveformChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("Waveform check: "+why);}
 static Color Bloom(Color c){float floor=new Vector3(c.r,c.g,c.b).magnitude*.05f,gain=2*Mathf.Exp(.51785713f*10);return new Color(Mathf.Pow(Mathf.Max(c.r,floor),2.2f)*gain,Mathf.Pow(Mathf.Max(c.g,floor),2.2f)*gain,Mathf.Pow(Mathf.Max(c.b,floor),2.2f)*gain,Mathf.Pow(c.a,2.2f));}
 static float Decode(float x)=>x>=1?x:x<=.04045f?x/12.92f:Mathf.Pow((x+.055f)/1.055f,2.4f);
 static float Encode(float x)=>x<=.0031308f?x*12.92f:1.055f*Mathf.Pow(x,1/2.4f)-.055f;
 static Color Interpolate(Color[] colors,float u,float v)=>u+v<=1?colors[0]*(1-u-v)+colors[1]*u+colors[2]*v:colors[1]*(1-v)+colors[2]*(1-u)+colors[3]*(u+v-1);
 public static void Run(){try{
  RunCase(false);RunCase(true);
  Debug.Log("OPENBRUSH_WAVEFORM_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
 static void RunCase(bool fft){
  var root=new GameObject("Waveform reference");var timeline=root.AddComponent<OpenBrushTimeline>();timeline.Duration=2;
  var child=new GameObject("Waveform_stroke_0000_part_000");child.transform.SetParent(root.transform,false);
  var colors=new[]{new Color(.8f,.06f,.03f,.2f),new Color(.04f,.7f,.12f,.8f),new Color(.04f,.14f,.9f,1),new Color(.12f,.06f,.18f,.5f)};
  var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,new Vector3(1,1,0)},uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one},colors=colors,triangles=new[]{0,1,2,2,1,3,0,2,1,2,3,1}};
  child.AddComponent<MeshFilter>().sharedMesh=mesh;var material=new Material(Shader.Find("OpenBrush/ExportedBrush"));material.SetOverrideTag("OpenBrushGuid",fft?OpenBrushWaveformImport.FftGuid:OpenBrushWaveformImport.Guid);child.AddComponent<MeshRenderer>().sharedMaterial=material;
  var times=new[]{0f,.123f,.5f,1.9f};OpenBrushWaveformImport.PrepareSamples(root,times,1);
  var motion=root.GetComponentsInChildren<OpenBrushTextureMotion>().Single();var blooms=colors.Select(Bloom).ToArray();
  float maximum=0,peak=0;int illuminated=0;double change=0;
  for(int f=0;f<times.Length;f++)for(int y=2;y<126;y+=3)for(int x=2;x<254;x+=3){
   float u=x/255f,v=y/127f;var c=Interpolate(colors,u,v);var bloom=Interpolate(blooms,u,v);float envelope=fft?1:Mathf.Sin(u*3.14159f);
   float wave=.15f*(Mathf.Sin(-30*c.r*(times[f]*3)+u*100*c.r)+Mathf.Sin(-40*c.g*(times[f]*3)+u*100*c.g)+Mathf.Sin(-50*c.b*(times[f]*3)+u*100*c.b));
   float line=fft?(Mathf.Abs(v-.5f)>wave?0:wave):Mathf.Clamp01(1-((1-envelope)*40+20)*Mathf.Abs(v-.5f-wave*envelope));var expected=bloom*(bloom.a*envelope*line*2);
   float sourcePeak=Mathf.Max(bloom.r,bloom.g,bloom.b)*bloom.a*2;
   for(int k=0;k<3;k++)expected[k]=Encode(Mathf.Clamp01(Decode(expected[k])*Mathf.Min(1,OpenBrushWaveformImport.Ceiling/Mathf.Max(sourcePeak,1e-6f))/OpenBrushWaveformImport.Ceiling));
   var actual=motion.Frames[f].GetPixel(x+2,y+2);for(int k=0;k<3;k++)maximum=Mathf.Max(maximum,Mathf.Abs(actual[k]-expected[k]));
   peak=Mathf.Max(peak,actual.r,actual.g,actual.b);if(actual.maxColorComponent>.1f)illuminated++;
   change+=Math.Abs(actual.r-motion.Frames[0].GetPixel(x+2,y+2).r);
  }
  Debug.Log("Waveform FFT="+fft+" GPU/scalar max="+maximum+" peak="+peak+" illuminated="+illuminated+" change="+change);
  Check(maximum<.015 && peak>(fft?.5f:.9f) && peak<=1 && illuminated>30,"bounded source vertex bloom, varying alpha, interpolated color frequencies and doubled faces agree with GPU");
  Check(change>10,"color-dependent waveform changes with time");
  Check(motion.Timeline==timeline && motion.FrameTimes.SequenceEqual(times) && motion.FramesPerSecond==60,"waveform shares the drawing clock at 60 fps");
  Check(motion.Frames.All(t=>t.format==TextureFormat.RGBA32 && t.isDataSRGB),"bounded frames use a gamma-encoded atlas");
  Check(motion.SourceMaterial.GetTag("OpenBrushBoundedWaveformAtlas",false,"")=="1" && Mathf.Abs(motion.SourceMaterial.GetColor("_TintColor").r-new Color(OpenBrushWaveformImport.Ceiling,OpenBrushWaveformImport.Ceiling,OpenBrushWaveformImport.Ceiling,1).gamma.r)<.001,"native multiplier uses the bounded atlas profile");
  Check(!child.GetComponent<MeshRenderer>().enabled && child.GetComponentsInChildren<MeshFilter>().Last().sharedMesh.triangles.Length==6,"coincident faces collapse once while preserving additive multiplicity");
  UnityEngine.Object.DestroyImmediate(root);
 }
}

