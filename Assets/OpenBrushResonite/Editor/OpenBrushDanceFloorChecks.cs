using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushDanceFloorChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);Debug.Log("DanceFloor check: "+why);}
 public static void Run(){try {
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-dancefloor-v1/";
  var raw=JsonUtility.FromJson<OpenBrushDanceFloorImport.Data>(File.ReadAllText(dir+"dancefloor.json"));
  Check(raw.parts.Length==1 && raw.parts[0].positions.Length==200,"five exported strokes / fifty original quads");
  var p=raw.parts[0];Check(p.colors.Distinct().Count()==5,"five source colors including dark purple");
  Check(p.sourceBirths.Distinct().Count()==1 && p.births.Distinct().Count()>20,"loaded-sketch shader phase differs from drawing reveal timestamps");
  foreach(float x in new[]{-.137f,-.001f,.001f,.139f}) {
   var pos=new Vector3(x,1.713f,-.013f);var normal=new Vector3(.2f,.3f,.9327379f);
   var result=OpenBrushDanceFloorImport.Position(pos,normal,.729f);
   Check(Math.Abs(result.x-(-Math.Ceiling(-pos.x*50)/50+normal.x*.00729))<1e-6 && Math.Abs(result.y-(Math.Ceiling(pos.y*50)/50+normal.y*.00729))<1e-6,"signed canvas quantization and centimetre normal displacement");
  }
  Check(OpenBrushDanceFloorImport.Phase(.999f,0)==59 && OpenBrushDanceFloorImport.Phase(1,0)==0 && OpenBrushDanceFloorImport.Phase(2.5f,.25f)==15,"one-second reset and retained source phase offsets");
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   OpenBrushMaterialImport.Prepare(root,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json")),false);
   var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
   OpenBrushGrowthImport.Prepare(root,timing,null);OpenBrushDanceFloorImport.Prepare(root,raw,timing);
   var motions=root.GetComponentsInChildren<OpenBrushGeometryMotion>();Check(motions.Length==1,"one source-batch sequence");
   var motion=motions[0];Check(motion.FrameTimes[0]==0 && motion.FrameTimes.Last()==timing.duration && motion.FrameTimes.Zip(motion.FrameTimes.Skip(1),(a,b)=>b>a).All(v=>v),"strictly increasing finite playback coverage");
   Check(root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushDanceFloorImport.Guid).All(r=>!r.enabled),"static originals disabled");
   Check(motion.Frames[0].triangles.Distinct().Count()==1 && motion.Frames.Last().triangles.Length==300,"no future geometry at zero; all fifty quads at end");
   int low=Array.FindIndex(motion.FrameTimes,t=>Math.Abs(t-4)<.00001),high=Array.FindIndex(motion.FrameTimes,t=>Math.Abs(t-4.9)<.00001);
   Check(low>=0 && high>=0 && !motion.Frames[low].vertices.SequenceEqual(motion.Frames[high].vertices) && !motion.Frames[low].uv.SequenceEqual(motion.Frames[high].uv),"shape and complete-fragment phase both animate after drawing");
   var atlas=(Texture2D)motion.SourceMaterial.mainTexture;
   var native=new FrooxEngine.UnlitMaterial();OpenBrushMaterialConverter.ConfigureUnlit(motion.SourceMaterial,OpenBrushProfiles.Find(OpenBrushDanceFloorImport.Guid),native);
   Check(native.TintColor.profile==ColorProfile.sRGB && Mathf.Abs(native.TintColor.color.r-Mathf.Pow(4,1/2.2f))<.001f && native.RenderQueue==3000 && native.BlendMode==FrooxEngine.BlendMode.Opaque,"bounded HDR multiplier keeps native sRGB transfer and original opaque queue");
   Check(atlas.isDataSRGB && atlas.mipmapCount>1 && atlas.width==4896 && atlas.height==4624,"full-resolution five-color 60-phase atlas with filtered gutters");
   float error=0;
   // Inspect interior texels from native-upload pixels against the independently
   // evaluated source fragment and the native gamma decode + gain.
   var originalData=new AutoGltfFileParser(dir+"drawing.glb").Parse();
   using(originalData)using(var sourceImporter=new ImporterContext(originalData)) {
    var sourceInstance=sourceImporter.Load();var tex=(Texture2D)sourceInstance.gameObject.GetComponentInChildren<MeshRenderer>().sharedMaterial.mainTexture;
    var palette=p.colors.Distinct().ToArray();
    for(int c=0;c<5;c++)foreach(int phase in new[]{0,15,30,45,59})foreach(int x in new[]{8,63,127,200}) {
     var t=tex.GetPixel(x,127);int tile=c*60+phase;var pixel=atlas.GetPixel(tile%18*272+8+x,tile/18*272+8+127).linear*4;
     for(int channel=0;channel<3;channel++) {double expected=4*palette[c][channel]*palette[c].a*t[channel]*t.a*Math.Pow(phase/60.0,3);expected=expected>=1?expected:expected<=.04045?expected/12.92:Math.Pow((expected+.055)/1.055,2.4);error=Mathf.Max(error,(float)Math.Abs(expected-pixel[channel]));}
    }
   }
   Check(error<.035,"native texture transfer matches complete source fragment; maximum="+error);
  }
  Debug.Log("OPENBRUSH_DANCEFLOOR_CHECKS_PASSED");EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}


