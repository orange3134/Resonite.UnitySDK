using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
public static class OpenBrushBundleChecks {
 static string Hash(string file){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","").ToLowerInvariant();}
 static void Check(bool ok,string reason){if(!ok)throw new InvalidOperationException(reason);Debug.Log("Bundle check: "+reason);}
 static void Reject(Action action,string reason){try{action();}catch(InvalidDataException){Debug.Log("Bundle check: rejected "+reason);return;}catch(IOException){Debug.Log("Bundle check: rejected "+reason);return;}throw new InvalidOperationException("Accepted "+reason);}
 public static void Run(){
  string parent=Path.GetFullPath(".local"),root=Path.Combine(parent,"BundleChecks-"+Guid.NewGuid().ToString("N"));
  try{
   Directory.CreateDirectory(root);
   var data=new OpenBrushConversionBundle.Data{schemaVersion=1,files=new OpenBrushConversionBundle.Files{model="drawing.glb",materials="materials.json",timeline="timeline.json",electricity="electricity.json"}};
   string[] names={"drawing.glb","materials.json","timeline.json","electricity.json"};
   foreach(string name in names)File.WriteAllText(Path.Combine(root,name),"bundle checksum fixture "+name);
   data.hashes=names.Select(n=>new OpenBrushConversionBundle.Hash{path=n,sha256=Hash(Path.Combine(root,n))}).ToArray();
   string manifest=Path.Combine(root,"bundle.json");File.WriteAllText(manifest,JsonUtility.ToJson(data));
   var bundle=OpenBrushConversionBundle.Load(manifest);Check(bundle.Has("-openbrushElectricity") && !bundle.Has("-openbrushDisco"),"only available auxiliary files are exposed");
   string moved=Path.Combine(root,"relocated folder");Directory.CreateDirectory(moved);
   foreach(string name in names.Concat(new[]{"bundle.json"}))File.Move(Path.Combine(root,name),Path.Combine(moved,name));
   manifest=Path.Combine(moved,"bundle.json");bundle=OpenBrushConversionBundle.Load(manifest);
   Check(bundle.Get("-openbrushModel")==Path.Combine(moved,"drawing.glb"),"relocation resolves paths from the bundle, independent of working directory");
   File.AppendAllText(Path.Combine(moved,"electricity.json"),"changed");Reject(()=>OpenBrushConversionBundle.Load(manifest),"mismatched auxiliary bytes");
   File.WriteAllText(Path.Combine(moved,"electricity.json"),"bundle checksum fixture electricity.json");
   data.files.model="../outside.glb";File.WriteAllText(Path.Combine(root,"outside.glb"),"outside");File.WriteAllText(manifest,JsonUtility.ToJson(data));Reject(()=>OpenBrushConversionBundle.Load(manifest),"directory escape");
   data.files.model=Path.Combine(root,"outside.glb");File.WriteAllText(manifest,JsonUtility.ToJson(data));Reject(()=>OpenBrushConversionBundle.Load(manifest),"absolute file reference");
   data.files.model="drawing.glb";data.hashes=data.hashes.Where(h=>h.path!="drawing.glb").ToArray();File.WriteAllText(manifest,JsonUtility.ToJson(data));Reject(()=>OpenBrushConversionBundle.Load(manifest),"input without checksum");
   Debug.Log("OPENBRUSH_BUNDLE_CHECKS_PASSED");EditorApplication.Exit(0);
  }catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
  finally{
   // This directory was created above under the project-local test directory.
   string resolved=Path.GetFullPath(root);
   if(Path.GetDirectoryName(resolved)==parent && Path.GetFileName(resolved).StartsWith("BundleChecks-",StringComparison.Ordinal) && Directory.Exists(resolved))Directory.Delete(resolved,true);
  }
 }
}
