using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

internal static class Launcher
{
    [STAThread]
    static void Main()
    {
        var root = AppDomain.CurrentDomain.BaseDirectory;
        var python = Path.Combine(root, ".venv-openbrush", "Scripts", "pythonw.exe");
        var script = Path.Combine(root, "Support", "ImporterApp", "app.py");
        try
        {
            if (!File.Exists(python) || !File.Exists(script))
                throw new FileNotFoundException("SDKフォルダー内で起動してください。変換用Pythonとアプリが必要です。");
            Process.Start(new ProcessStartInfo(python, "\"" + script + "\"") {
                WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true
            });
        }
        catch (Exception error)
        {
            MessageBox.Show(error.Message, "Open Brush → Resonite", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
