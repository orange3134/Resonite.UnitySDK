using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

// Evaluate texture * interpolated paint * interpolated alpha in source Gamma
// space, coalescing exact duplicate faces before converting emission to linear.
// Non-coincident overlaps and arbitrary backgrounds still blend natively.
public static class OpenBrushSoftFragmentImport {
 public const string Feather="a555b809-2017-46cb-ac26-e63173d8f45e",Drafting="492b36ff-b337-436a-ba5f-1e87ee86747e";
 public const string Dark="61820308-bae0-48d4-8288-a7d4e6734d2f";
 const int Padding=4;
 sealed class Page {public string Key;public Chart[] Charts;public int Width,Height;}
 sealed class Chart {public OpenBrushHighlighterImport.Face Face;public int W,H,X,Y;}
 // Mip generation averages neighboring charts. Keep unrelated paint colors in
 // different images; otherwise a white chart draws bright seams on a dark one.
 // Varying RGB faces receive their own page rather than sharing a color bucket.
 public static string PaintGroup(OpenBrushHighlighterImport.Face f) {
  var c=f.colors[0];
  if(f.colors.Any(v=>v.r!=c.r || v.g!=c.g || v.b!=c.b))return "gradient:"+f.key;
  return string.Join(",",new[]{c.r,c.g,c.b}.Select(v=>v.ToString("R",CultureInfo.InvariantCulture)));
 }
 public static Color Sample(Texture2D texture,OpenBrushHighlighterImport.Face face,float s,float t,Vector2 scale,Vector2 offset) {
  s=Mathf.Max(0,s);t=Mathf.Max(0,t);if(s+t>1){s=Mathf.Clamp01((s-t+1)*.5f);t=1-s;}
  var uv=Vector2.Scale(face.uv[0]*(1-s-t)+face.uv[1]*s+face.uv[2]*t,scale)+offset;
  var paint=face.colors[0]*(1-s-t)+face.colors[1]*s+face.colors[2]*t;
  var tex=OpenBrushTextureSampling.SampleRaw(texture,uv.x,uv.y);
  return OpenBrushAdditiveImport.Shade(tex,paint,face.copies);
 }
 public static Color SampleDark(OpenBrushHighlighterImport.Face face,float s,float t) {
  s=Mathf.Max(0,s);t=Mathf.Max(0,t);if(s+t>1){s=Mathf.Clamp01((s-t+1)*.5f);t=1-s;}
  var uv=face.uv[0]*(1-s-t)+face.uv[1]*s+face.uv[2]*t;
  var c=face.colors[0]*(1-s-t)+face.colors[1]*s+face.colors[2]*t;
  // Audited normal Dark.mat: Opacity=1, AlphaMultiply=.33, SoftMultiply=1,
  // EdgeFadeoff=.157. Gamma-framebuffer multiplication is approximated in
  // native linear blending; exact response still depends on the background.
  float edge=Mathf.Clamp01(Mathf.Min(uv.y,1-uv.y)/.157f),k=Mathf.Clamp01(c.a*.33f*edge);
  return new Color(DarkMultiplier(Mathf.Pow(Mathf.Lerp(1,c.r,k),face.copies)),
   DarkMultiplier(Mathf.Pow(Mathf.Lerp(1,c.g,k),face.copies)),
   DarkMultiplier(Mathf.Pow(Mathf.Lerp(1,c.b,k),face.copies)),1);
 }
 // Decode the desired result relative to a bright-gray destination. Decoding the
 // multiplier alone implicitly assumes white and over-darkens darker scenes.
 // A destination-independent native blend cannot exactly match Gamma blending
 // on every background, so this remains a documented approximation.
 public static float DarkMultiplier(float factor)=>Mathf.GammaToLinearSpace(.8f*factor)/Mathf.GammaToLinearSpace(.8f);
 static void Orient(OpenBrushHighlighterImport.Face face,Vector2 texels) {
  // Put the shortest UV edge on the first chart axis; thin ribbon triangles
  // then need a thin chart instead of a full source-sized square per triangle.
  int a=0,b=1,c=2;float best=float.PositiveInfinity;
  for(int i=0;i<3;i++)for(int j=i+1;j<3;j++) {
   float length=Vector2.Scale(face.uv[j]-face.uv[i],texels).sqrMagnitude;
   if(length<best){best=length;a=i;b=j;c=3-i-j;}
  }
  var order=new[]{a,b,c};face.indices=order.Select(i=>face.indices[i]).ToArray();face.uv=order.Select(i=>face.uv[i]).ToArray();face.colors=order.Select(i=>face.colors[i]).ToArray();
 }
 public static void Prepare(GameObject root,long pixelLimit=33554432) {
  if(pixelLimit<1 || pixelLimit>33554432)throw new ArgumentOutOfRangeException(nameof(pixelLimit));
  var renderers=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled && r.sharedMaterial!=null && r.sharedMaterial.GetTag("OpenBrushSoftFragments",false,"")!="1" && new[]{Feather,Drafting,Dark}.Contains(r.sharedMaterial.GetTag("OpenBrushGuid",false,""))).ToArray();
  foreach(var group in renderers.GroupBy(r=>r.sharedMaterial)) {
   var source=group.Key;var texture=source.mainTexture as Texture2D;
   bool dark=source.GetTag("OpenBrushGuid",false,"")==Dark;
   if(!dark && (texture==null||!texture.isReadable))throw new InvalidOperationException("Soft fragment bake needs a readable normal texture");
   var inputs=group.Select(r=>new{Renderer=r,Mesh=r.GetComponent<MeshFilter>().sharedMesh,Faces=OpenBrushHighlighterImport.Faces(r.GetComponent<MeshFilter>().sharedMesh)}).ToArray();
   var texels=dark ? new Vector2(64,64) : Vector2.Scale(new Vector2(texture.width,texture.height),source.mainTextureScale);
   foreach(var face in inputs.SelectMany(i=>i.Faces))Orient(face,texels);
   var pages=inputs.SelectMany(i=>i.Faces).GroupBy(PaintGroup).Select(paintGroup=>new Page {
    Key=paintGroup.Key,Charts=paintGroup.GroupBy(f=>f.key).Select(g=>{
     var f=g.First();var u=Vector2.Scale(f.uv[1]-f.uv[0],texels);var v=Vector2.Scale(f.uv[2]-f.uv[0],texels);
     return new Chart{Face=f,W=Mathf.Max(32,Mathf.CeilToInt(Mathf.Max(Mathf.Abs(u.x),Mathf.Abs(u.y)))+1),H=Mathf.Max(32,Mathf.CeilToInt(Mathf.Max(Mathf.Abs(v.x),Mathf.Abs(v.y)))+1)};
    }).OrderByDescending(c=>c.H).ThenByDescending(c=>c.W).ToArray()
   }).ToArray();
   if(pages.SelectMany(p=>p.Charts).Any(c=>c.W>2048||c.H>2048))throw new InvalidOperationException("Soft fragment UV span needs subdivision before baking");
   // Plan all paint-isolated pages before allocating textures. Reduce sampling
   // density together, keeping geometry, color isolation, padding and mipmaps.
   int reductions=0;
   while(true) {
    long allocatedPixels=0;bool fits=true;
    foreach(var page in pages) {
     var charts=page.Charts;
     int minWidth=((charts.Max(c=>c.W+2*Padding)+63)/64)*64;
     int width=minWidth;long bestArea=long.MaxValue;
     for(int trial=minWidth;trial<=SystemInfo.maxTextureSize;trial+=64) {
      int tx=0,ty=0,tr=0;
      foreach(var c in charts){int w=c.W+2*Padding,h=c.H+2*Padding;if(tx+w>trial){tx=0;ty+=tr;tr=0;}tx+=w;tr=Mathf.Max(tr,h);}
      int th=((ty+tr+3)/4)*4;long area=(long)trial*th;
      if(th<=SystemInfo.maxTextureSize && area<bestArea){width=trial;bestArea=area;}
     }
     int x=0,y=0,row=0;
     foreach(var c in charts){int w=c.W+2*Padding,h=c.H+2*Padding;if(x+w>width){x=0;y+=row;row=0;}c.X=x+Padding;c.Y=y+Padding;x+=w;row=Mathf.Max(row,h);}
     page.Width=width;page.Height=((y+row+3)/4)*4;
     allocatedPixels+=(long)page.Width*page.Height;
     fits &= page.Width<=SystemInfo.maxTextureSize && page.Height<=SystemInfo.maxTextureSize;
    }
    if(fits && allocatedPixels<=pixelLimit)break;
    var all=pages.SelectMany(p=>p.Charts).ToArray();
    if(all.All(c=>c.W<=16 && c.H<=16))throw new InvalidOperationException("Soft fragment color pages exceed pixel budget even at minimum detail: "+source.name);
    foreach(var chart in all){chart.W=Mathf.Max(16,chart.W/2);chart.H=Mathf.Max(16,chart.H/2);}
    reductions++;
   }
   if(reductions>0) {
    string adjustment=source.name+" soft fragment sampling reduced in "+reductions+" steps to fit "+pixelLimit+" pixels (minimum 16 samples per face axis); all faces, paint groups and timing retained. Fine details may be softer.";
    OpenBrushAtlasBudget.Adjustments.Add(adjustment);Debug.LogWarning(adjustment);
   }
   foreach(var page in pages) {
   var charts=page.Charts;int width=page.Width,height=page.Height;
   var faceKeys=new HashSet<string>(charts.Select(c=>c.Face.key));
   var pixels=new Color[width*height];var lookup=charts.ToDictionary(c=>c.Face.key);
   if(dark)for(int i=0;i<pixels.Length;i++)pixels[i]=Color.white; // Neutral multiply padding.
   foreach(var c in charts)for(int py=-Padding;py<c.H+Padding;py++)for(int px=-Padding;px<c.W+Padding;px++)pixels[(c.Y+py)*width+c.X+px]=dark ? SampleDark(c.Face,px/(float)(c.W-1),py/(float)(c.H-1)) : Sample(texture,c.Face,px/(float)(c.W-1),py/(float)(c.H-1),source.mainTextureScale,source.mainTextureOffset);
   var atlas=new Texture2D(width,height,TextureFormat.RGBAHalf,true,true){name=source.name+"_GammaPressure_"+page.Key,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};atlas.SetPixels(pixels);atlas.Apply(true,false);
   var material=new Material(source){name=source.name+"_SoftFragments",mainTexture=atlas,mainTextureScale=Vector2.one,mainTextureOffset=Vector2.zero};material.SetColor("_TintColor",Color.white);material.SetOverrideTag("OpenBrushSoftFragments","1");
   foreach(var input in inputs) {
    var selected=input.Faces.Where(f=>faceKeys.Contains(f.key)).ToArray();if(selected.Length==0)continue;
    var original=input.Mesh.vertices;var vertices=new List<Vector3>();var uv=new List<Vector2>();
    foreach(var face in selected) {
     var c=lookup[face.key];vertices.AddRange(face.indices.Select(i=>original[i]));
     uv.Add(new Vector2((c.X+.5f)/width,(c.Y+.5f)/height));uv.Add(new Vector2((c.X+c.W-.5f)/width,(c.Y+.5f)/height));uv.Add(new Vector2((c.X+.5f)/width,(c.Y+c.H-.5f)/height));
    }
    var mesh=new Mesh{name=input.Mesh.name+"_SoftFragments",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.colors=Enumerable.Repeat(Color.white,vertices.Count).ToArray();mesh.triangles=Enumerable.Range(0,vertices.Count).ToArray();mesh.RecalculateBounds();
    var child=new GameObject("Source Gamma Pressure");child.transform.SetParent(input.Renderer.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=material;input.Renderer.enabled=false;
   }
   Debug.Log("SOFT_FRAGMENT_BAKE "+source.name+" paint="+page.Key+" charts="+charts.Length+" atlas="+width+"x"+height+" format=RGBAHalf");
   }
  }
 }
}

