using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Brennwerk;
public sealed record MediaRequest(string Source, string Destination, string Kind, int Title);
public static class MediaTools
{
    public static string? FindDvdfab()
    {
        var matches=new List<string>();
        foreach(string programRoot in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})
        {
            string root=Path.Combine(programRoot,"DVDFab");
            if(!Directory.Exists(root))continue;
            foreach(string directory in Directory.EnumerateDirectories(root,"DVDFab *"))
            {
                string exe=Path.Combine(directory,"DVDFab64.exe");if(File.Exists(exe))matches.Add(exe);
            }
        }
        return matches.OrderByDescending(p=>Version.TryParse(FileVersionInfo.GetVersionInfo(p).FileVersion,out var v)?v:new Version()).FirstOrDefault();
    }
    public static string[] DvdfabArguments(string source)
    {
        if(!File.Exists(source)&&!Directory.Exists(source))throw new IOException("Quelle fehlt.");
        return ["/MODE","OPENSOURCE","/SRC",Path.GetFullPath(source)];
    }
    public static void OpenDvdfab(string source)
    {
        string exe=FindDvdfab()??throw new IOException("DVDFab wurde nicht gefunden.");
        var psi=new ProcessStartInfo(exe){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(exe)!};
        foreach(string arg in DvdfabArguments(source))psi.ArgumentList.Add(arg);
        Process.Start(psi)?.Dispose();
    }
    public static string? FindVlc() => new[] {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"VideoLAN\VLC"),
        Path.Combine(AppContext.BaseDirectory, "VLC")
    }.FirstOrDefault(p => File.Exists(Path.Combine(p, "libvlc.dll")) && Directory.Exists(Path.Combine(p, "plugins")));
    public static List<string> OpticalDrives() => DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.CDRom).Select(d => d.Name).ToList();
    public static string Analyze(string source)
    {
        if (!Directory.Exists(source)) throw new IOException("Kein lesbares Medium. Disc einlegen und erneut prüfen.");
        var lines = new List<string> { "Quelle: " + Path.GetFullPath(source) };
        bool dvd = Directory.Exists(Path.Combine(source,"VIDEO_TS"));
        bool bd = Directory.Exists(Path.Combine(source,"BDMV"));
        lines.Add(dvd ? "Format: DVD-Video" : bd ? "Format: Blu-ray Video" : "Format: Daten-/Softwaremedium oder unbekannt");
        if (dvd) lines.Add("CSS-Status: nicht durch Dateiliste feststellbar. Der Export prüft den Lesezugriff über VLC.");
        if (bd)
        {
            lines.Add(Directory.Exists(Path.Combine(source,"AACS")) ? "AACS-Verzeichnis vorhanden: Hinweis auf AACS-Schutz." : "Kein AACS-Verzeichnis sichtbar; kein Beweis für ein ungeschütztes Medium.");
            lines.Add(Directory.Exists(Path.Combine(source,"BDSVM")) ? "BDSVM-Verzeichnis vorhanden: Hinweis auf BD+." : "Kein BD+-Verzeichnis sichtbar.");
        }
        // File names are only hints. Never identify a protection scheme as certain from these alone.
        var markers = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
            ["secdrv.sys"]="SafeDisc", ["clcd16.dll"]="SafeDisc", ["clcd32.dll"]="SafeDisc",
            ["cms16.dll"]="SafeDisc", ["cms_95.dll"]="SafeDisc", ["cms_nt.dll"]="SafeDisc",
            ["securom.ini"]="SecuROM", ["paul.dll"]="SecuROM", ["protect.dll"]="möglicher Schutztreiber"
        };
        var hints = new HashSet<string>(); int inspected=0;
        foreach (var file in EnumerateSafe(source))
        {
            inspected++; if (markers.TryGetValue(Path.GetFileName(file), out var hint)) hints.Add(hint + " (Dateihinweis: " + Path.GetFileName(file) + ")");
            if (inspected >= 20000) { lines.Add("Dateianalyse nach 20.000 Dateien begrenzt."); break; }
        }
        lines.AddRange(hints);
        if (!dvd && !bd) lines.Add("Dateinamen erkennen nicht jeden Kopierschutz. Keine Freischaltung, kein Entfernen von Lizenz- oder Onlineprüfungen.");
        lines.Add("VLC-Lesemodul: " + (FindVlc() ?? "nicht gefunden; VLC 3.x 64 Bit lokal installieren"));
        string? fab=FindDvdfab();
        lines.Add(fab==null?"DVDFab: nicht gefunden":"DVDFab "+FileVersionInfo.GetVersionInfo(fab).FileVersion+" vorhanden; Freischaltung nicht geprüft.");
        string keydb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "aacs", "KEYDB.cfg");
        lines.Add(File.Exists(keydb) ? "AACS-Schlüsseldatei im Benutzerprofil vorhanden; passender Disc-Schlüssel nicht geprüft." : "Keine AACS-Schlüsseldatei im üblichen Benutzerpfad gefunden.");
        return string.Join(Environment.NewLine, lines);
    }
    public static IEnumerable<string> EnumerateSafe(string root)
    {
        var pending=new Stack<string>(); pending.Push(root);
        while (pending.Count>0)
        {
            string current=pending.Pop();
            if ((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new IOException("Umgeleitete Ordner werden nicht unterstützt: " + current);
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                var flags=File.GetAttributes(entry);
                if ((flags&FileAttributes.ReparsePoint)!=0) throw new IOException("Verknüpfungen werden nicht unterstützt: " + entry);
                if ((flags&FileAttributes.Directory)!=0) pending.Push(entry); else yield return entry;
            }
        }
    }
    public static void CheckDestination(string source, string destination)
    {
        RejectRedirectedAncestors(source);RejectRedirectedAncestors(destination);
        string root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(source)) + Path.DirectorySeparatorChar;
        string target=Path.GetFullPath(destination);
        if (target.StartsWith(root,StringComparison.OrdinalIgnoreCase) || target.Equals(Path.TrimEndingDirectorySeparator(root),StringComparison.OrdinalIgnoreCase)) throw new IOException("Das Sicherungsziel muss außerhalb der Quelle liegen.");
    }
    static void RejectRedirectedAncestors(string path)
    {
        string? current=Path.GetFullPath(path);
        while(current!=null)
        {
            if((Directory.Exists(current)||File.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Umgeleitete Quell- oder Zielpfade werden nicht unterstützt: "+current);
            current=Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(current));
        }
    }
    public static async Task BackupFiles(string source,string destination,Action<string> log,CancellationToken token)
    {
        CheckDestination(source,destination);
        if (Directory.Exists(destination) || File.Exists(destination)) throw new IOException("Bitte einen noch nicht vorhandenen Zielordner wählen.");
        string root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(source));
        var files=EnumerateSafe(root).ToArray();
        if(files.Length==0) throw new IOException("Keine Dateien zum Sichern gefunden.");
        Directory.CreateDirectory(destination);
        int count=0;
        foreach(var file in files)
        {
            token.ThrowIfCancellationRequested();
            string relative=Path.GetRelativePath(root,file), target=Path.Combine(destination,relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            string stage=target+"."+Guid.NewGuid()+".partial";
            try
            {
                await using var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read,1048576,true);
                await using(var output=new FileStream(stage,FileMode.CreateNew,FileAccess.Write,FileShare.None,1048576,true)) { await input.CopyToAsync(output,token); await output.FlushAsync(token); }
                input.Position=0;
                byte[] expected=await SHA256.HashDataAsync(input,token);
                await using(var check=File.OpenRead(stage)) { byte[] actual=await SHA256.HashDataAsync(check,token); if(!expected.AsSpan().SequenceEqual(actual)) throw new IOException("Dateiprüfung fehlgeschlagen: "+relative); }
                File.Move(stage,target);
            }
            finally { if(File.Exists(stage)) File.Delete(stage); }
            count++; if(count%25==0 || count==files.Length) log($"{count} / {files.Length} Dateien kopiert und geprüft");
        }
        log("Dateisicherung beendet. Diese Kopie entfernt keinen Software-Kopierschutz und bildet keine physischen Disc-Merkmale ab.");
    }
    public static async Task Export(MediaRequest request,Action<string> log,CancellationToken token)
    {
        CheckDestination(request.Source,request.Destination);
        if(FindVlc()==null) throw new IOException("VLC 3.x (64 Bit) wurde nicht gefunden. Die Lesemodule werden aus der lokalen VLC-Installation geladen.");
        if(request.Kind is not ("dvd" or "bluray" or "test-file")) throw new IOException("Unbekannter Filmtyp.");
        string stage=request.Destination+"."+Guid.NewGuid()+".partial.mkv";
        var psi=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 };
        if(string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),"dotnet",StringComparison.OrdinalIgnoreCase)) psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"Brennwerk.dll"));
        psi.ArgumentList.Add("--media-worker");
        using var child=new Process {StartInfo=psi};
        try
        {
            child.Start();
            await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request with {Destination=stage})); child.StandardInput.Close();
            using var registration=token.Register(()=> {try {if(!child.HasExited) child.Kill(true);} catch {} });
            var errors=child.StandardError.ReadToEndAsync();
            while(await child.StandardOutput.ReadLineAsync() is {} line) log(line);
            await child.WaitForExitAsync(); token.ThrowIfCancellationRequested();
            string error=await errors;
            if(child.ExitCode!=0) throw new IOException(string.IsNullOrWhiteSpace(error)?"VLC konnte das Medium nicht vollständig lesen. Schutz, Schlüssel und Laufwerk prüfen.":error.Trim());
            if(!File.Exists(stage) || new FileInfo(stage).Length<1024) throw new IOException("Kein gültiger Filmexport erzeugt.");
            using(var file=File.OpenRead(stage)) { byte[] header=new byte[4]; if(file.Read(header)!=4 || !header.AsSpan().SequenceEqual(new byte[]{0x1a,0x45,0xdf,0xa3})) throw new IOException("Ausgabe hat keinen Matroska-Containerkopf."); }
            File.Move(stage,request.Destination,true);
            log("Filmtitel als MKV exportiert. Bitte Ton, Bild und Vollständigkeit durch Wiedergabe kontrollieren.");
        }
        finally
        {
            try { if(child.Id>0 && !child.HasExited) {child.Kill(true);child.WaitForExit(5000);} } catch {}
            if(File.Exists(stage)) File.Delete(stage);
        }
    }
}

// Native VLC is isolated in a child process. No VLC binaries are redistributed.
public static class VlcReader
{
    const string Dll="brennwerk-vlc";
    static IntPtr handle;
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
    public static string Initialize()
    {
        if(handle==IntPtr.Zero)
        {
            string directory=MediaTools.FindVlc() ?? throw new IOException("VLC 3.x 64 Bit fehlt.");
            handle=LoadLibraryEx(Path.Combine(directory,"libvlc.dll"),IntPtr.Zero,0x1100);
            if(handle==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"VLC-Lesemodul konnte nicht geladen werden. 64-Bit-Installation prüfen.");
            NativeLibrary.SetDllImportResolver(typeof(VlcReader).Assembly,(name,_,_)=>name==Dll?handle:IntPtr.Zero);
            Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH",Path.Combine(directory,"plugins"));
        }
        string version=Marshal.PtrToStringUTF8(libvlc_get_version()) ?? "";
        if(!version.StartsWith("3.")) throw new IOException("Dieser Adapter unterstützt VLC 3.x. Gefundene Version: "+version);
        return version;
    }
    public static void Run(MediaRequest request,Action<string> log)
    {
        string version=Initialize(); log("VLC-Lesemodul: "+version);
        IntPtr instance=IntPtr.Zero, media=IntPtr.Zero, player=IntPtr.Zero;
        var args=new[]{"--ignore-config","--no-media-library","--no-video-title-show","--no-stats","--no-interact","--no-sub-autodetect-file"};
        var allocated=args.Select(Marshal.StringToCoTaskMemUTF8).ToArray(); IntPtr argv=Marshal.AllocCoTaskMem(IntPtr.Size*allocated.Length);
        try
        {
            Marshal.Copy(allocated,0,argv,allocated.Length); instance=libvlc_new(allocated.Length,argv);
            if(instance==IntPtr.Zero) throw new IOException("VLC konnte nicht initialisiert werden.");
            if(request.Title<0 || request.Title>999) throw new IOException("Ungültige Titelnummer.");
            string mrl;
            if(request.Kind=="test-file") mrl=new Uri(Path.GetFullPath(request.Source)).AbsoluteUri;
            else
            {
                if(request.Kind is not ("dvd" or "bluray")) throw new IOException("Ungültiger Medientyp.");
                if(!Directory.Exists(request.Source)) throw new IOException("Quelle fehlt.");
                string scheme=request.Kind=="dvd"?"dvdsimple":"bluray";
                // No anchor retains libbluray's longest-title selection. Explicit Blu-ray
                // choices use its zero-based list; DVD's title offset is 1.
                int selected=request.Kind=="bluray"?request.Title-1:request.Title;
                string pathUri=new Uri(Path.TrimEndingDirectorySeparator(Path.GetFullPath(request.Source))+Path.DirectorySeparatorChar).AbsoluteUri;
                mrl=scheme+pathUri[4..];
                if(request.Kind!="bluray" || request.Title!=0) mrl+="#"+selected+"-"+selected;
            }
            media=libvlc_media_new_location(instance,mrl);
            if(media==IntPtr.Zero) throw new IOException("Medium konnte nicht geöffnet werden.");
            string destination=Path.GetFullPath(request.Destination).Replace('\\','/');
            // The sout mini-language has its own quoting rules, independent of shell quoting.
            string escaped=destination.Replace("\\","\\\\").Replace("'","\\'");
            libvlc_media_add_option(media,":sout=#std{access=file,mux=mkv,dst='"+escaped+"'}");
            libvlc_media_add_option(media,":sout-all"); libvlc_media_add_option(media,":no-sout-keep");
            libvlc_media_add_option(media,":no-bluray-menu");
            libvlc_media_add_option(media,":sout-audio"); libvlc_media_add_option(media,":sout-video"); libvlc_media_add_option(media,":sout-spu");
            player=libvlc_media_player_new_from_media(media);
            if(player==IntPtr.Zero || libvlc_media_player_play(player)!=0) throw new IOException("VLC konnte den Lesevorgang nicht starten.");
            var clock=Stopwatch.StartNew(); long lastSize=-1; var changed=Stopwatch.StartNew(); bool ended=false;
            while(clock.Elapsed<TimeSpan.FromHours(12))
            {
                Thread.Sleep(500); int state=libvlc_media_player_get_state(player);
                if(state==7) throw new IOException("VLC-Lesefehler. Für geschützte Medien fehlen möglicherweise passende Schlüssel oder unterstützte Lesekomponenten.");
                if(state==6) {ended=true;break;}
                long size=File.Exists(request.Destination)?new FileInfo(request.Destination).Length:0;
                if(size!=lastSize) { lastSize=size;changed.Restart(); }
                if(changed.Elapsed>TimeSpan.FromMinutes(3)) throw new IOException("Seit drei Minuten keine Ausgabedaten. Lesevorgang wurde beendet.");
                if((int)clock.Elapsed.TotalSeconds%10==0) log($"Export: {size/1048576.0:F1} MiB · {libvlc_media_player_get_position(player)*100:F0} %");
            }
            if(!ended) throw new IOException("Zeitgrenze für den Filmexport erreicht.");
            libvlc_media_player_stop(player);
        }
        finally
        {
            if(player!=IntPtr.Zero) {libvlc_media_player_stop(player);libvlc_media_player_release(player);}
            if(media!=IntPtr.Zero) libvlc_media_release(media);
            if(instance!=IntPtr.Zero) libvlc_release(instance);
            Marshal.FreeCoTaskMem(argv); foreach(var pointer in allocated) Marshal.FreeCoTaskMem(pointer);
        }
    }
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr libvlc_get_version();
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr libvlc_new(int count,IntPtr arguments);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_release(IntPtr instance);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr libvlc_media_new_location(IntPtr instance,[MarshalAs(UnmanagedType.LPUTF8Str)] string location);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_media_add_option(IntPtr media,[MarshalAs(UnmanagedType.LPUTF8Str)] string option);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_media_release(IntPtr media);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern IntPtr libvlc_media_player_new_from_media(IntPtr media);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int libvlc_media_player_play(IntPtr player);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_media_player_stop(IntPtr player);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern void libvlc_media_player_release(IntPtr player);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern int libvlc_media_player_get_state(IntPtr player);
    [DllImport(Dll,CallingConvention=CallingConvention.Cdecl)] static extern float libvlc_media_player_get_position(IntPtr player);
}
