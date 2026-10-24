// Slice.shader and ColorSpace.cginc equations: Tilt Brush Authors, Apache-2.0.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public static class OpenBrushSliceImport {
 public const string Guid="8d1510b2-1fc5-4c90-8db7-4c07eecfd849";
 public const int Resolution=64,Padding=2;
 // Source's chroma/luma conversion, evaluated per texel, not per mesh corner.
 public static Color Sample(Vector3 uvw) {
  float hue=(uvw.z*5)%6;
  var rgb=new Vector3(Mathf.Clamp01(-1+Mathf.Abs(hue-3)),Mathf.Clamp01(2-Mathf.Abs(hue-2)),Mathf.Clamp01(2-Mathf.Abs(hue-4)));
  // g_max_luma in the original cginc is an unbound uniform, not a static constant.
  // Unity initializes it to zero. Confirmed against the normal runtime Slice material.
  float luma=0,chroma=uvw.x,y=uvw.y;
  if(y<luma)chroma*=y/luma;else if(y<1)chroma*=(1-y)/(1-luma);
  rgb=(rgb-Vector3.one*luma)*chroma+Vector3.one*y;
  return new Color(Mathf.Clamp01(rgb.x),Mathf.Clamp01(rgb.y),Mathf.Clamp01(rgb.z),1);
 }
 static int Key(float w)=>checked((int)Math.Round(w*1000000));
 public static void Prepare(GameObject root) {
  var targets=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==Guid).ToArray();
  if(targets.Length==0)return;
  var inputs=new Dictionary<MeshRenderer,Vector3[]>();var keys=new HashSet<int>();
  foreach(var r in targets) {
   if(r.transform.Find("Slice Color Surface")!=null)throw new InvalidOperationException("Slice already prepared");
   var mesh=r.GetComponent<MeshFilter>()?.sharedMesh;
   if(mesh==null || mesh.subMeshCount!=1 || mesh.GetVertexAttributeDimension(UnityEngine.Rendering.VertexAttribute.TexCoord2)!=3)throw new InvalidOperationException("Slice needs original UVW; regenerate this bundle");
   var uvw=new List<Vector3>();mesh.GetUVs(2,uvw);
   if(uvw.Count!=mesh.vertexCount)throw new InvalidOperationException("Slice UVW count mismatch");
   var tri=mesh.triangles;
   for(int i=0;i<tri.Length;i+=3)if(Mathf.Abs(uvw[tri[i]].z-uvw[tri[i+1]].z)>1e-5f || Mathf.Abs(uvw[tri[i]].z-uvw[tri[i+2]].z)>1e-5f)throw new InvalidOperationException("Slice distance varies inside a cross-section");
   foreach(var v in uvw)keys.Add(Key(v.z));inputs.Add(r,uvw.ToArray());
  }
  var ordered=keys.OrderBy(k=>k).ToArray();var charts=ordered.Select((k,i)=>new{k,i}).ToDictionary(v=>v.k,v=>v.i);
  int pitch=Resolution+Padding*2,columns=Mathf.CeilToInt(Mathf.Sqrt(keys.Count)),rows=Mathf.CeilToInt(keys.Count/(float)columns),width=columns*pitch,height=rows*pitch;
  if(width>SystemInfo.maxTextureSize || height>SystemInfo.maxTextureSize || (long)width*height>16777216)throw new InvalidOperationException("Slice atlas exceeds 16M texels; split sketch");
  var pixels=new Color[width*height];
  for(int i=0;i<ordered.Length;i++)for(int y=0;y<pitch;y++)for(int x=0;x<pitch;x++) {
   float u=Mathf.Clamp01((x-Padding)/(float)(Resolution-1)),v=Mathf.Clamp01((y-Padding)/(float)(Resolution-1));
   pixels[(i/columns*pitch+y)*width+i%columns*pitch+x]=Sample(new Vector3(u,v,ordered[i]/1000000f));
  }
  var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,false){name="Slice Chroma Luma Atlas",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,anisoLevel=8};
  texture.SetPixels(pixels);texture.Apply(false,false);
  var material=new Material(targets[0].sharedMaterial){name="Slice - Original Color Sections",mainTexture=texture,mainTextureScale=Vector2.one,mainTextureOffset=Vector2.zero};material.SetColor("_TintColor",Color.white);
  foreach(var r in targets) {
   var mesh=UnityEngine.Object.Instantiate(r.GetComponent<MeshFilter>().sharedMesh);
   mesh.name+="_SliceAtlas";mesh.uv=inputs[r].Select(v=>{int index=charts[Key(v.z)];return new Vector2((index%columns*pitch+Padding+.5f+v.x*(Resolution-1))/width,(index/columns*pitch+Padding+.5f+v.y*(Resolution-1))/height);}).ToArray();
   mesh.colors=Enumerable.Repeat(Color.white,mesh.vertexCount).ToArray();
   var child=new GameObject("Slice Color Surface");child.transform.SetParent(r.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;r.enabled=false;
  }
  Debug.Log("SLICE_BAKE sections="+keys.Count+" surfaces="+targets.Length+" atlas="+width+"x"+height);
 }
}
