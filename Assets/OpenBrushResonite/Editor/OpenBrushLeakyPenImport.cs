using System;
using System.Linq;
using UnityEngine;

// Audited normal LeakyPen graph: RGB is paint only, alpha is paint.a times
// AlphaMask(uv * (.3,.5)).a times DiffuseTexture(uv).r, with cutoff .5.
// Both inputs are the same main.png whose alpha channel is entirely one.
// Thus moving R into native albedo alpha is exact at texture samples and leaves
// vertex alpha multiplication and thresholding to the native PBS material.
public static class OpenBrushLeakyPenImport {
 public const string Guid="ddda8745-4bb5-ac54-88b6-d1480370583e";
 public static Texture2D BakeMask(Texture2D source) {
  if(source==null || !source.isReadable)throw new InvalidOperationException("LeakyPen needs its readable normal-material image");
  var pixels=source.GetPixels();
  if(pixels.Any(c=>c.a!=1))throw new InvalidOperationException("LeakyPen texture is not the audited opaque-alpha source. Refresh its legacy export textures; custom alpha masks need separate UV sampling.");
  for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(1,1,1,pixels[i].r);
  var output=new Texture2D(source.width,source.height,TextureFormat.RGBA32,true,true){name="LeakyPen_SourceRedMask",filterMode=source.filterMode,wrapModeU=source.wrapModeU,wrapModeV=source.wrapModeV};
  output.SetPixels(pixels);output.Apply(true,false);return output;
 }
 public static void Prepare(GameObject root) {
  var materials=root.GetComponentsInChildren<MeshRenderer>(true).Select(r=>r.sharedMaterial).Where(m=>m!=null && m.GetTag("OpenBrushGuid",false,"")==Guid).Distinct().ToArray();
  foreach(var material in materials) {
   if(material.GetTag("OpenBrushLeakyMask",false,"")=="1")continue;
   material.mainTexture=BakeMask(material.mainTexture as Texture2D);material.mainTextureScale=Vector2.one;material.mainTextureOffset=Vector2.zero;material.SetOverrideTag("OpenBrushLeakyMask","1");
  }
 }
}
