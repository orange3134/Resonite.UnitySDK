using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Preserve the normal fragment's order: 4*paintRGB*paintAlpha*texRGB*texAlpha.
// Convert the complete gamma framebuffer value to native linear color, rather
// than decoding paint and texture separately before their HDR multiplication.
public static class OpenBrushHyperGridSurface {
 public static float Native(float value)=>value>=1?value:value<=.04045f?value/12.92f:Mathf.Pow((value+.055f)/1.055f,2.4f);
 public static Color Shade(Color tex,Color paint) {
  float gain=4*paint.a*tex.a;
  return new Color(Native(gain*paint.r*tex.r),Native(gain*paint.g*tex.g),Native(gain*paint.b*tex.b),1);
 }
 public static Material Build(OpenBrushHyperGridImport.Part p,Material source,out Vector2[] uv) {
  var texture=source.mainTexture as Texture2D;
  if(texture==null || !texture.isReadable || source.mainTextureScale!=Vector2.one || source.mainTextureOffset!=Vector2.zero)throw new InvalidOperationException("HyperGrid requires its original readable sprite without a texture transform");
  var palette=new List<Color>();var map=new Dictionary<Color,int>();var entries=new int[p.colors.Length];
  for(int i=0;i<entries.Length;i++) {
   if(p.colors[i]!=p.colors[i/4*4])throw new InvalidOperationException("HyperGrid expects uniform original quad colors");
   if(!map.TryGetValue(p.colors[i],out int index)){index=palette.Count;palette.Add(p.colors[i]);map.Add(p.colors[i],index);}entries[i]=index;
  }
  // Power-of-two tiles and half-texture repeat gutters keep minified samples inside their color.
  int padX=texture.width/2,padY=texture.height/2;int columns=Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Sqrt(palette.Count))),rows=Mathf.NextPowerOfTwo((palette.Count+columns-1)/columns);
  int tw=texture.width+padX*2,th=texture.height+padY*2,w=columns*tw,h=rows*th;
  if(w>SystemInfo.maxTextureSize || h>SystemInfo.maxTextureSize || (long)w*h>16777216)throw new InvalidOperationException("HyperGrid color atlas exceeds 16M pixels; split the sketch");
  var pixels=texture.GetPixels();var tile=new Color[tw*th];
  var atlas=new Texture2D(w,h,TextureFormat.RGBAHalf,true,true){name="HyperGrid_CompleteFragment",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear};
  for(int n=0;n<palette.Count;n++) {
   for(int y=0;y<th;y++)for(int x=0;x<tw;x++) {
    // Source Repeat sampling at the original quad edges.
    int sx=(x-padX+texture.width)%texture.width,sy=(y-padY+texture.height)%texture.height;
    tile[y*tw+x]=Shade(pixels[sy*texture.width+sx],palette[n]);
   }
   atlas.SetPixels(n%columns*tw,n/columns*th,tw,th,tile);
  }
  atlas.Apply(true,false);uv=new Vector2[entries.Length];
  for(int i=0;i<uv.Length;i++) {int n=entries[i];uv[i]=new Vector2((n%columns*tw+padX+p.uv[i].x*texture.width)/w,(n/columns*th+padY+(1-p.uv[i].y)*texture.height)/h);}
  var material=new Material(source){name="HyperGrid - Original Fragment",mainTexture=atlas};material.SetColor("_TintColor",Color.white);
  return material;
 }
}
