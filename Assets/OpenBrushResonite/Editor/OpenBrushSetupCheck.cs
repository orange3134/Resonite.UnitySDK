using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Batch entry point: a receipt proves compilation finished and this method ran.
// Does not connect to Resonite, alter scenes or change user license settings.
public static class OpenBrushSetupCheck {
    [Serializable] sealed class Receipt {
        public bool completed=true;
        public string unityVersion=Application.unityVersion;
    }
    public static void Run() {
        try {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-openbrushSetupReceipt");
            if(index<0 || index+1>=args.Length)throw new ArgumentException("Missing setup receipt path");
            if(Shader.Find("OpenBrush/ExportedBrush")==null)throw new InvalidOperationException("Missing Open Brush shader");
            var output=Path.GetFullPath(args[index+1]);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output,JsonUtility.ToJson(new Receipt(),true));
            Debug.Log("OPENBRUSH_SETUP_READY");
            EditorApplication.Exit(0);
        } catch(Exception error) {
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
