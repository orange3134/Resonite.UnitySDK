using System;
using System.IO;
using System.Linq;
using UnityEngine;

// Normal MarbledRainbow graph, non-audio path: RGB=paint*base texture,
// alpha=base texture alpha (no paint alpha), specular=map*.21960784,
// smoothness=.8 independently of specular texture alpha, tangent-space normal.
public static class OpenBrushMarbledImport {
 public const string Guid="c86c058d-1bda-2e94-08db-f3d6a96ac4a1";
 public static Texture2D PackSpecular(Texture2D source) {
  if(source==null || !source.isReadable)throw new InvalidOperationException("Marbled Rainbow requires a readable specular map");
  var pixels=source.GetPixels();
  // With a SpecularMap, native PBS reads specularity AND smoothness from the
  // texture rather than multiplying it by the fallback SpecularColor field.
  for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(pixels[i].r*.21960784f,pixels[i].g*.21960784f,pixels[i].b*.21960784f,.8f);
  // Source runs in Gamma: the sampled RGB numbers are used directly by its
  // specular input. Preserve those numbers as native linear parameter data.
  var result=new Texture2D(source.width,source.height,TextureFormat.RGBA32,true,true){name="MarbledRainbow_Specular",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear};
  result.SetPixels(pixels);result.Apply(true,false);return result;
 }
 public static Mesh TextureAlphaOnly(Mesh source) {
  if(source==null || source.colors.Length!=source.vertexCount)throw new InvalidOperationException("Marbled Rainbow requires vertex RGBA");
  var mesh=UnityEngine.Object.Instantiate(source);mesh.name=source.name+"_TextureAlpha";
  var colors=mesh.colors;for(int i=0;i<colors.Length;i++)colors[i].a=1;mesh.colors=colors;return mesh;
 }
 public static void Prepare(GameObject root) {
  var targets=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial!=null && r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==Guid && r.sharedMaterial.GetTag("OpenBrushSpecularMap",false,"")!="1").ToArray();
  if(targets.Length==0)return;
  var image=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
  Texture2D packed;
  try {
   if(!image.LoadImage(File.ReadAllBytes("Assets/OpenBrushResonite/SourceTextures/MarbledRainbow_Specular.png")))throw new InvalidOperationException("Cannot load normal Marbled Rainbow specular texture");
   packed=PackSpecular(image);
  }finally{UnityEngine.Object.DestroyImmediate(image);}
  foreach(var renderer in targets) {
   var filter=renderer.GetComponent<MeshFilter>();filter.sharedMesh=TextureAlphaOnly(filter.sharedMesh);
  }
  foreach(var material in targets.Select(r=>r.sharedMaterial).Distinct()) {
   material.SetTexture("_MaskTex",packed);material.SetOverrideTag("OpenBrushSpecularMap","1");
  }
 }
}
