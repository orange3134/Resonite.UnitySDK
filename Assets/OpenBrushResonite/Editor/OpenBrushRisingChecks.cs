using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UniGLTF;
public static class OpenBrushRisingChecks {
 static void Check(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
 public static void Run(){try {
  var reference=new Material(Shader.Find("Hidden/OpenBrushRisingReference"));
  var rt=new RenderTexture(1,1,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);rt.Create();
  var read=new Texture2D(1,1,TextureFormat.RGBAFloat,false,true);float maxError=0;
  for(int i=0;i<64;i++) {
   var center=new Vector3(-7+i*.3f,17+Mathf.Sin(i),Mathf.Cos(i));float seed=(i+.37f)/65,seconds=i*.793f;
   reference.SetVector("_CenterSeed",new Vector4(center.x,center.y,center.z,seed));reference.SetFloat("_Seconds",seconds);
   Graphics.Blit(Texture2D.whiteTexture,rt,reference);RenderTexture.active=rt;read.ReadPixels(new Rect(0,0,1,1),0,0);read.Apply();RenderTexture.active=null;
   var pixel=read.GetPixel(0,0);float error=Vector3.Distance(new Vector3(pixel.r,pixel.g,pixel.b),OpenBrushRisingAppearance.Displacement(center,seed,seconds));
   maxError=Mathf.Max(maxError,error);Check(error<.00001f,"source CPU/GPU displacement at "+seconds+": "+error);
  }
  var tex=new Color(.8f,.2f,.1f,.5f);var black=OpenBrushRisingAppearance.Shade(tex,Color.black,0);
  Check(black==OpenBrushRisingAppearance.Shade(tex,Color.black,.99f),"White reflection does not dim with particle lifetime");
  var red=OpenBrushRisingAppearance.Shade(tex,Color.red,0);var dim=OpenBrushRisingAppearance.Shade(tex,Color.red,.9f);
  Check(red.r>1 && red.r>dim.r && red.g==black.g && red.b==black.b,"Paint fades independently, preserving HDR and white reflection");
  const string dir="D:/Document/VerseTV/open-brush-versetv/Support/Exports/parity/bundle-rising-v1/";
  var timing=JsonUtility.FromJson<OpenBrushGrowthImport.Data>(File.ReadAllText(dir+"timeline.json"));
  using(var data=new AutoGltfFileParser(dir+"drawing.glb").Parse())using(var importer=new ImporterContext(data)) {
   var instance=importer.Load();instance.ShowMeshes();var root=instance.gameObject;
   OpenBrushMaterialImport.Prepare(root,JsonUtility.FromJson<OpenBrushSmokeImport.Manifest>(File.ReadAllText(dir+"materials.json")),false);
   OpenBrushGrowthImport.Prepare(root,timing,null);
   OpenBrushParticleImport.Prepare(root,JsonUtility.FromJson<OpenBrushParticleImport.Data>(File.ReadAllText(dir+"particles.json")),timing);
   var motion=root.GetComponentInChildren<OpenBrushParticleMotion>();Check(motion!=null,"Imported particle motion");
   Check(motion.Particles.Select(p=>new Vector3(p.color.r,p.color.g,p.color.b)).Distinct().Count()==5,"All five original colors survive extraction");
   Check(root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial.GetTag("OpenBrushGuid",false,"")==OpenBrushParticleImport.RisingGuid).All(r=>!r.enabled),"Original static quads suppressed");
   var p=motion.Particles[0];int birth=Mathf.CeilToInt(p.playBirth*60);
   Check(motion.Frames[birth-1].normals[0].x==0&&Mathf.Abs(motion.Frames[birth].normals[0].x-p.size)<1e-7,"Correct birth and full billboard width");
   Check(motion.Frames[0].normals.All(n=>n.x==0),"No future particles during lead-in");
   Check(motion.Frames[birth+30].vertices[0]!=motion.Frames[birth+90].vertices[0],"Upward motion continues after appearance");
   Check(motion.Frames[birth+30].uv[0]!=motion.Frames[birth+90].uv[0],"Fading phase selects a different atlas tile");
   Check(motion.Frames.Last().colors.All(c=>c==Color.white),"No double tint or phase alpha");
   Check(motion.BakedTexture.mipmapCount>1&&!motion.BakedTexture.isDataSRGB,"Linear HDR atlas has mipmaps");
   Debug.Log("RISING_FIXTURE particles="+motion.Particles.Length+" frames="+motion.Frames.Length+" atlas="+motion.BakedTexture.width+"x"+motion.BakedTexture.height);
  }
  Debug.Log("OPENBRUSH_RISING_CHECKS_PASSED maxGpuErrorMeters="+maxError);EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}

