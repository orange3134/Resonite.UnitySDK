using System;
using System.IO;
using System.Text;
using UnityEngine;

// Optional progress channel for the desktop app; CLI conversion is unchanged.
public static class OpenBrushImportProgress {
    [Serializable] sealed class Entry {
        public string stage, detail;
        public int current, total;
    }
    static string path;
    static long lastTicks;
    public static void Start(string destination) {
        path=null;lastTicks=0;
        if(string.IsNullOrEmpty(destination))return;
        var full=Path.GetFullPath(destination);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        using(var stream=new FileStream(full,FileMode.CreateNew,FileAccess.Write,FileShare.Read)){}
        path=full;
    }
    public static void Write(string stage,string detail,int current=0,int total=0) {
        if(path==null)return;
        var now=DateTime.UtcNow.Ticks;
        if(total>0 && current>0 && current<total && now-lastTicks<TimeSpan.TicksPerSecond/4)return;
        lastTicks=now;
        File.AppendAllText(path,JsonUtility.ToJson(new Entry{stage=stage,detail=detail,current=current,total=total})+"\n",new UTF8Encoding(false));
    }
}
