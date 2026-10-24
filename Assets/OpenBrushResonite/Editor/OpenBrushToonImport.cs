using System;
using System.Collections.Generic;
using UnityEngine;
// Body RGB and outline pressure coordinates are baked from original vertex
// attributes before growth slicing by prepare-openbrush-model / animate-brush-glb.
public static class OpenBrushToonImport {
    public const string Guid="4391385a-df73-4396-9e33-31e4e4930b27";
    public static void Prepare(GameObject root) {
        var cache=new Dictionary<Material,Material>();
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) {
            var source=renderer.sharedMaterial;
            if(source==null || source.GetTag("OpenBrushGuid",false,"")!=Guid)continue;
            if(renderer.transform.Find("Toon - Native Outline")!=null)throw new InvalidOperationException("Toon already prepared");
            if(!cache.TryGetValue(source,out var material)) {
                var mask=new Texture2D(256,1,TextureFormat.RGBAFloat,false,true){name="Toon_PressureOutline",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
                var colors=new Color[256];for(int i=0;i<256;i++){float v=(float)i/255;colors[i]=new Color(v,v,v,1);}mask.SetPixels(colors);mask.Apply(false,false);
                material=new Material(source){name="Toon_NativeOutline"};material.SetOverrideTag("OpenBrushToonOutline","1");material.SetTexture("_MaskTex",mask);cache.Add(source,material);
            }
            var child=new GameObject("Toon - Native Outline");child.transform.SetParent(renderer.transform,false);
            child.AddComponent<MeshFilter>().sharedMesh=renderer.GetComponent<MeshFilter>().sharedMesh;
            child.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
