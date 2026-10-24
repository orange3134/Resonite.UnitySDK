using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
public static class OpenBrushLightWireChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("LightWire check: "+why);}
 static float Encode(float x)=>(float)(x<=.0031308?12.92*x:1.055*Math.Pow(x,1/2.4)-.055);
 public static void Run(){try{
  var root=new GameObject("LightWire reference");
  var child=new GameObject("LightWire_stroke_0000_part_000");child.transform.SetParent(root.transform,false);
  var a=new Color(.8f,.06f,.03f,1);var b=new Color(.04f,.14f,.9f,1);
  var mesh=new Mesh{vertices=new[]{Vector3.zero,Vector3.right,Vector3.up,Vector3.one},uv=new[]{Vector2.zero,new Vector2(10.5f,0),Vector2.up,new Vector2(10.5f,1)},colors=new[]{a,b,a,b},triangles=new[]{0,1,2,2,1,3},normals=Enumerable.Repeat(Vector3.back,4).ToArray()};
  child.AddComponent<MeshFilter>().sharedMesh=mesh;var mat=new Material(Shader.Find("OpenBrush/ExportedBrush"));mat.SetOverrideTag("OpenBrushGuid",OpenBrushProceduralImport.WireGuid);child.AddComponent<MeshRenderer>().sharedMaterial=mat;
  OpenBrushProceduralImport.Prepare(root);OpenBrushLightWireImport.Prepare(root);
  var motion=root.GetComponentsInChildren<OpenBrushTextureMotion>().Single();var surface=motion.SourceMaterial;
  var body=(Texture2D)surface.mainTexture;var spec=(Texture2D)surface.GetTexture("_MaskTex");var ao=(Texture2D)surface.GetTexture("_BumpMap");
  float max=0;int bulbSamples=0;string worst="";
  foreach(int f in new[]{0,5,20,45,69})for(int x=2;x<2046;x+=3){
   float u=x/2047f*10.5f,e=(float)Math.Sin((u*2%1)*Math.PI);bool bulb=e<.1f;float border=Math.Abs(e)<.01?0:1;
   var c=Color.Lerp(a,b,x/2047f);var expected=c*(.2f*(bulb?0:1)*border);var actual=body.GetPixel(x+2,9);
   for(int k=0;k<3;k++)max=Math.Max(max,Math.Abs(expected[k]-actual[k]));
   var reflect=spec.GetPixel(x+2,9);max=Math.Max(max,Math.Abs(reflect.r-(.3f-(bulb?.15f:0))*border));max=Math.Max(max,Math.Abs(reflect.a-(bulb?0:.3f)));
   max=Math.Max(max,Math.Abs(ao.GetPixel(x+2,9).g-(bulb?.03f:.365f)));
   float on=1-Mathf.Clamp01(Mathf.Abs((u*2+.5f)%7-(f/30f*3)%7)*1.5f);int index=(int)((u*2+.5f)%3);
   var tint=index==0?new Color(.2f,.2f,1):index==1?new Color(1,.2f,.2f):new Color(.2f,1,.2f);
   var lit=c*tint*on;float floor=new Vector3(lit.r,lit.g,lit.b).magnitude*.05f;var emission=motion.Frames[f].GetPixel(x+2,9);
   for(int k=0;k<3;k++){float expectedEmission=(bulb?Mathf.Pow(Mathf.Max(lit[k],floor),2.2f)*OpenBrushLightWireImport.EmissionGain:0);float error=Math.Abs(emission[k]-expectedEmission)/Mathf.Max(1,expectedEmission);if(error>max){max=error;worst="x="+x+" f="+f+" channel="+k+" expected="+expectedEmission+" actual="+emission[k];}}
   if(bulb && on>0)bulbSamples++;
  }
  Debug.Log("LightWire GPU/scalar max="+max+" lit samples="+bulbSamples+" worst="+worst);
  Check(max<.008 && bulbSamples>5,"source color, boundary masks, specular, smoothness, AO, tint and HDR pulse agree with GPU");
  Check(motion.Frames.Length==70 && motion.EmissionOnly,"one independent 7/3-second emission cycle replaces legacy sequence");
  Check(!child.GetComponent<MeshRenderer>().enabled && child.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)==1,"growth parent retained with one active surface");
  Check(body.isDataSRGB && spec.isDataSRGB && !ao.isDataSRGB && !motion.Frames[0].isDataSRGB && motion.Frames[0].format==TextureFormat.RGBAHalf,"native texture color profiles preserve data and floating-point emission");
  Check(OpenBrushLightWireImport.EmissionGain>2193 && OpenBrushLightWireImport.EmissionGain<2194,"source HDR boost retained");
  Debug.Log("OPENBRUSH_LIGHTWIRE_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
