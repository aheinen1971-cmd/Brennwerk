using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace Brennwerk;
public static class Optical
{
    static dynamic Create(string name) => Activator.CreateInstance(Type.GetTypeFromProgID(name, true)!)!;
    static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
    public static List<Target> List()
    {
        var result = new List<Target>(); dynamic? master = null;
        try
        {
            master = Create("IMAPI2.MsftDiscMaster2");
            if (!(bool)master.IsSupportedEnvironment) return result;
            for (int i = 0; i < (int)master.Count; i++)
            {
                dynamic? recorder = null;
                try
                {
                    string id = master[i]; recorder = Create("IMAPI2.MsftDiscRecorder2"); recorder.InitializeDiscRecorder(id);
                    string paths = string.Join(" ", ((Array)recorder.VolumePathNames).Cast<object>());
                    result.Add(new Target(id, $"{paths} · {recorder.VendorId} {recorder.ProductId}"));
                }
                finally { Release(recorder); }
            }
        }
        finally { Release(master); }
        return result;
    }
    public static Task CreateIso(string folder, string destination, Action<string> log) => Sta(() =>
    {
        // Stage beside the final file; replace only after a complete image is written.
        string stage = destination + "." + Guid.NewGuid() + ".partial";
        dynamic? image = null, root = null, result = null; object? stream = null;
        try
        {
            ValidateFolder(folder);
            if (Path.GetFullPath(destination).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("ISO-Ziel liegt im Quellordner.");
            log("Datenordner wird als UDF/ISO-Abbild vorbereitet …");
            image = Create("IMAPI2FS.MsftFileSystemImage"); image.FreeMediaBlocks = 0x7fffffff;
            image.FileSystemsToCreate = 5; image.VolumeName = "BRENNWERK";
            root = image.Root; root.AddTree(Path.GetFullPath(folder), false);
            result = image.CreateResultImage(); stream = result.ImageStream;
            var source = (IStream)stream;
            using (var output = new FileStream(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                source.Stat(out var stat, 1); long remaining = stat.cbSize;
                var buffer = new byte[1024 * 1024]; IntPtr count = Marshal.AllocCoTaskMem(4);
                try
                {
                    while (remaining > 0)
                    {
                        source.Read(buffer, (int)Math.Min(buffer.Length, remaining), count);
                        int read = Marshal.ReadInt32(count); if (read <= 0) throw new EndOfStreamException("Unvollständiger ISO-Datenstrom.");
                        output.Write(buffer, 0, read); remaining -= read;
                    }
                    output.Flush(true);
                }
                finally { Marshal.FreeCoTaskMem(count); }
            }
            File.Move(stage, destination, true); log($"ISO erstellt: {new FileInfo(destination).Length / 1073741824.0:F2} GiB");
        }
        finally { Release(stream); Release(result); Release(root); Release(image); if (File.Exists(stage)) File.Delete(stage); }
    });
    public static void ValidateFolder(string folder)
    {
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Quellordner fehlt.");
        var pending = new Stack<string>(); pending.Push(folder);
        while (pending.Count > 0)
        {
            string current = pending.Pop();
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Verknüpfungen und umgeleitete Ordner bitte aus der Zusammenstellung entfernen: " + current);
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Verknüpfungen werden nicht unterstützt: " + entry);
                if ((attributes & FileAttributes.Directory) != 0) pending.Push(entry);
            }
        }
    }
    public static Task Burn(string path, string id, Action<string> log) => Sta(() =>
    {
        dynamic? recorder = null, writer = null; object? stream = null; bool acquired = false;
        try
        {
            if (!Path.GetExtension(path).Equals(".iso", StringComparison.OrdinalIgnoreCase)) throw new IOException("Für Disc-Brennen bitte eine ISO-Datei wählen.");
            recorder = Create("IMAPI2.MsftDiscRecorder2"); recorder.InitializeDiscRecorder(id);
            writer = Create("IMAPI2.MsftDiscFormat2Data"); writer.Recorder = recorder; writer.ClientName = "Brennwerk";
            recorder.AcquireExclusiveAccess(false, "Brennwerk"); acquired = true;
            if (!(bool)writer.IsRecorderSupported(recorder) || !(bool)writer.IsCurrentMediaSupported(recorder)) throw new IOException("Brenner oder eingelegtes Medium wird nicht unterstützt.");
            if (!(bool)writer.MediaHeuristicallyBlank) throw new IOException("Bitte ein leeres Medium einlegen. Automatisches Löschen und Multisession werden nicht unterstützt.");
            Marshal.ThrowExceptionForHR(SHCreateStreamOnFileEx(path, 0x20, 0, false, IntPtr.Zero, out stream));
            ((IStream)stream).Stat(out var stat, 1); long length = stat.cbSize;
            if (length == 0 || length % 2048 != 0) throw new IOException("Die ISO-Größe muss ein Vielfaches von 2048 Byte sein.");
            if (length / 2048 > (long)writer.FreeSectorsOnMedia) throw new IOException("Die ISO-Datei ist größer als die Kapazität des eingelegten Mediums.");
            writer.ForceMediaToBeClosed = true;
            log("Disc wird geschrieben und finalisiert. Dieser Schritt kann mehrere Minuten dauern.");
            writer.Write(stream); log("Schreiben abgeschlossen.");
        }
        finally { if (acquired) { try { recorder!.ReleaseExclusiveAccess(); } catch { } } Release(stream); Release(writer); Release(recorder); }
    });
    static Task Sta(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { try { action(); tcs.SetResult(); } catch (Exception ex) { tcs.SetException(ex); } });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); return tcs.Task;
    }
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    static extern int SHCreateStreamOnFileEx(string path, uint mode, uint attributes, bool create, IntPtr reserved, [MarshalAs(UnmanagedType.Interface)] out object stream);
}
