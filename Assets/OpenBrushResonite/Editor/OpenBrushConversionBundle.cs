using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

// A relocatable data bundle, not executable commands. Verify all referenced
// bytes before preparing a scene or connecting to a Resonite world.
public sealed class OpenBrushConversionBundle {
 [Serializable] public class Files {public string model,materials,timeline,disco,particles,electricity,hypergrid,dancefloor,keijiro;}
 [Serializable] public class Hash {public string path,sha256;}
 [Serializable] public class Data {public int schemaVersion;public Files files;public Hash[] hashes;}
 readonly Dictionary<string,string> paths=new Dictionary<string,string>();
 public bool Has(string argument)=>paths.ContainsKey(argument);
 public string Get(string argument)=>paths[argument];
 public static OpenBrushConversionBundle Load(string manifest) {
  manifest=Path.GetFullPath(manifest);var root=Path.GetDirectoryName(manifest);
  var data=JsonUtility.FromJson<Data>(File.ReadAllText(manifest));
  if(data==null || data.schemaVersion!=1 || data.files==null || data.hashes==null)throw new InvalidDataException("Expected an Open Brush bundle schemaVersion 1");
  var verified=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  foreach(var entry in data.hashes) {
   if(entry==null || string.IsNullOrEmpty(entry.sha256) || entry.sha256.Length!=64)throw new InvalidDataException("Missing bundle checksum");
   string path=Resolve(root,entry.path);
   if(!verified.Add(path))throw new InvalidDataException("Repeated bundle checksum path: "+entry.path);
   string hash;using(var sha=SHA256.Create())using(var file=File.OpenRead(path))hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
   if(!string.Equals(hash,entry.sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Bundle file changed or mismatched: "+entry.path);
  }
  var result=new OpenBrushConversionBundle();
  result.Add(root,verified,"-openbrushModel",data.files.model,true);
  result.Add(root,verified,"-openbrushManifest",data.files.materials,true);
  result.Add(root,verified,"-openbrushTimeline",data.files.timeline,true);
  result.Add(root,verified,"-openbrushDisco",data.files.disco,false);
  result.Add(root,verified,"-openbrushParticles",data.files.particles,false);
  result.Add(root,verified,"-openbrushElectricity",data.files.electricity,false);
  result.Add(root,verified,"-openbrushHyperGrid",data.files.hypergrid,false);
  result.Add(root,verified,"-openbrushDanceFloor",data.files.dancefloor,false);
  result.Add(root,verified,"-openbrushKeijiro",data.files.keijiro,false);
  return result;
 }
 void Add(string root,HashSet<string> verified,string key,string relative,bool required) {
  if(string.IsNullOrEmpty(relative)){if(required)throw new InvalidDataException("Missing required bundle input: "+key);return;}
  var path=Resolve(root,relative);if(!verified.Contains(path))throw new InvalidDataException("Bundle input is not checksummed: "+relative);paths.Add(key,path);
 }
 static string Resolve(string root,string relative) {
  if(string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || relative.IndexOf(':')>=0)throw new InvalidDataException("Bundle paths must be relative files");
  var path=Path.GetFullPath(Path.Combine(root,relative));
  if(!path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Bundle path escapes its directory");
  if(!File.Exists(path))throw new FileNotFoundException("Missing bundle input",path);
  // Do not let a junction/symlink silently bind a relocated bundle elsewhere.
  for(string check=path;!string.Equals(check,root,StringComparison.OrdinalIgnoreCase);check=Path.GetDirectoryName(check))
   if((File.GetAttributes(check)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Linked bundle files are not supported: "+relative);
  return path;
 }
}
