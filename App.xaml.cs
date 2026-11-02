using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Brennwerk;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        if(e.Args.Length==2&&e.Args[0]=="--apply-update")
        {
            base.OnStartup(e);
            try{await Task.Run(()=>Updates.Apply(e.Args[1]));Shutdown(0);}
            catch(Exception ex){MessageBox.Show(ex.Message,"Brennwerk-Update fehlgeschlagen",MessageBoxButton.OK,MessageBoxImage.Error);Shutdown(1);}
            return;
        }
        if(e.Args.Length>=2 && e.Args[0]=="--usb-selftest")
        {
            base.OnStartup(e);var messages=new List<string>();int exitCode=0;
            try
            {
                await Backend.TestEmbeddedUsb(messages.Add);
                bool rejected=false;
                try { await Backend.WriteUsb("",new Target("test","Invalid",-1,"invalid",1,"invalid"),false,true,messages.Add); }
                catch(IOException){rejected=true;}
                if(!rejected)throw new IOException("Ungueltiger USB-Auftrag wurde nicht abgewiesen.");
                messages.Add("PASS: Embedded module and JSON transport tested without external PS1. Invalid device request rejected before any write.");
            }
            catch(Exception ex){messages.Add("FAIL: "+ex);exitCode=1;}
            File.WriteAllLines(e.Args[1],messages);Shutdown(exitCode);return;
        }
        if(e.Args.Length>=1 && e.Args[0]=="--media-worker")
        {
            base.OnStartup(e);
            Console.InputEncoding=new UTF8Encoding(false); Console.OutputEncoding=new UTF8Encoding(false);
            try
            {
                var request=System.Text.Json.JsonSerializer.Deserialize<MediaRequest>(Console.ReadLine() ?? "") ?? throw new IOException("Auftrag fehlt.");
                await Task.Run(()=>VlcReader.Run(request,Console.WriteLine));Shutdown(0);
            }
            catch(Exception ex){Console.Error.WriteLine(ex.Message);Shutdown(1);}
            return;
        }
        if (e.Args.Length >= 2 && e.Args[0] == "--selftest")
        {
            base.OnStartup(e);
            int code = 0; var log = new StringBuilder();
            string folder = Path.Combine(Path.GetTempPath(), "Brennwerk-test-" + Guid.NewGuid());
            try
            {
                Directory.CreateDirectory(Path.Combine(folder, "Quelle", "Unterordner"));
                File.WriteAllText(Path.Combine(folder, "Quelle", "Hallo.txt"), "Brennwerk Integrationstest – äöü");
                File.WriteAllBytes(Path.Combine(folder, "Quelle", "Unterordner", "Daten.bin"), Enumerable.Range(0, 65536).Select(x => (byte)x).ToArray());
                string iso = Path.Combine(folder, "Test.iso");
                await Optical.CreateIso(Path.Combine(folder, "Quelle"), iso, s => log.AppendLine(s));
                var bytes = File.ReadAllBytes(iso);
                if (bytes.Length % 2048 != 0 || Encoding.ASCII.GetString(bytes, 32769, 5) != "CD001") throw new IOException("ISO-9660-Deskriptor fehlt.");
                if (!Encoding.ASCII.GetString(bytes).Contains("NSR0")) throw new IOException("UDF-Deskriptor fehlt.");
                log.AppendLine("PASS: Echtes IMAPI-UDF/ISO-Abbild erstellt; Sektorausrichtung und Dateisystem-Deskriptoren geprueft.");
                bool rejected = false;
                try { await Optical.CreateIso(Path.Combine(folder, "Quelle"), Path.Combine(folder, "Quelle", "bad.iso"), _ => { }); } catch (IOException) { rejected = true; }
                if (!rejected) throw new Exception("Rekursives ISO-Ziel wurde nicht gesperrt.");
                log.AppendLine("PASS: ISO-Ziel im Quellordner gesperrt.");
                log.AppendLine($"PASS: Brennererkennung: {Optical.List().Count} Geraete; USB-Erkennung: {Backend.ListUsb().Count} geeignete Geraete.");
                var usbTestLog = new System.Collections.Concurrent.ConcurrentQueue<string>();
                await Backend.TestEmbeddedUsb(s => usbTestLog.Enqueue(s));
                foreach (string message in usbTestLog) log.AppendLine(message);
                log.AppendLine("PASS: Eingebautes USB-Modul ohne externe PS1-Datei gestartet und Schutztests bestanden.");
                string backup=Path.Combine(folder,"Sicherung");
                await MediaTools.BackupFiles(Path.Combine(folder,"Quelle"),backup,s=>log.AppendLine(s),CancellationToken.None);
                if(!File.ReadAllBytes(Path.Combine(backup,"Unterordner","Daten.bin")).AsSpan().SequenceEqual(File.ReadAllBytes(Path.Combine(folder,"Quelle","Unterordner","Daten.bin")))) throw new IOException("Sicherungsinhalt weicht ab.");
                rejected=false;try {MediaTools.CheckDestination(Path.Combine(folder,"Quelle"),Path.Combine(folder,"Quelle","nested"));} catch(IOException){rejected=true;}
                if(!rejected)throw new IOException("Sicherung in Quelle nicht gesperrt.");
                log.AppendLine("PASS: Verschachtelte Dateisicherung mit Hashpruefung, Inhalte und rekursiver Zielschutz.");
                string analysis=MediaTools.Analyze(Path.Combine(folder,"Quelle"));
                if(!analysis.Contains("unbekannt"))throw new IOException("Leere Schutzdiagnose.");
                log.AppendLine("PASS: Medienanalyse markiert unbekannten Schutz nicht als erkannt oder entfernt.");
                string[] fabArgs=MediaTools.DvdfabArguments(Path.Combine(folder,"Quelle"));
                if(fabArgs.Length!=4||fabArgs[1]!="OPENSOURCE"||fabArgs.Contains("/DEST")||fabArgs.Contains("/SILENT"))throw new IOException("DVDFab-Übergabe enthält unerwartete Auftragsparameter.");
                log.AppendLine("PASS: DVDFab-Erkennung und reine Quellenübergabe geprüft; kein Kopierauftrag gestartet.");
                if(MediaTools.FindVlc()!=null)
                {
                    string wave=Path.Combine(folder,"Ton ' ü.wav"),movie=Path.Combine(folder,"Export ' ü.mkv");
                    using(var writer=new BinaryWriter(File.Create(wave)))
                    {
                        const int samples=48000*3;
                        writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(48000);writer.Write(96000);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);
                        for(int n=0;n<samples;n++)writer.Write((short)(Math.Sin(n*2*Math.PI*440/48000)*12000));
                    }
                    var mediaLog=new System.Collections.Concurrent.ConcurrentQueue<string>();
                    await MediaTools.Export(new MediaRequest(wave,movie,"test-file",0),s=>mediaLog.Enqueue(s),CancellationToken.None);
                    foreach(string message in mediaLog)log.AppendLine(message);
                    log.AppendLine("PASS: Native VLC-Anbindung im isolierten Prozess, echter MKV-Export und Unicode-/Apostroph-Pfade.");
                    string bad=Path.Combine(folder,"Defekt.bin");File.WriteAllBytes(bad,new byte[4096]);
                    byte[] before=File.ReadAllBytes(movie);rejected=false;
                    try {await MediaTools.Export(new MediaRequest(bad,movie,"test-file",0),_=>{},CancellationToken.None);}catch(IOException){rejected=true;}
                    if(!rejected||!before.AsSpan().SequenceEqual(File.ReadAllBytes(movie)))throw new IOException("Fehlgeschlagener Export hat vorhandenes Ziel verändert.");
                    log.AppendLine("PASS: Fehlerhafter Filmeingang abgewiesen; vorhandene Zieldatei bleibt unverändert.");
                    string cancelled=Path.Combine(folder,"Abbruch.mkv");using var cts=new CancellationTokenSource();cts.Cancel();rejected=false;
                    try {await MediaTools.Export(new MediaRequest(wave,cancelled,"test-file",0),_=>{},cts.Token);}catch(OperationCanceledException){rejected=true;}
                    if(!rejected||File.Exists(cancelled)||Directory.GetFiles(folder,"Abbruch.mkv.*.partial.mkv").Length!=0)throw new IOException("Abbruch hat Ausgabereste hinterlassen.");
                    log.AppendLine("PASS: Filmexport-Abbruch beendet den Arbeitsprozess und verwirft Teilausgaben.");
                }
            }
            catch (Exception ex) { code = 1; log.AppendLine("FAIL: " + ex); }
            finally
            {
                // Exact GUID scratch directory created by this test.
                string fullFolder = Path.GetFullPath(folder);
                string tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
                if (fullFolder.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(fullFolder).StartsWith("Brennwerk-test-", StringComparison.Ordinal) && Directory.Exists(fullFolder)) Directory.Delete(fullFolder, true);
                File.WriteAllText(e.Args[1], log.ToString()); Shutdown(code);
            }
            return;
        }
        if (e.Args.Length >= 2 && e.Args[0] == "--preview")
        {
            base.OnStartup(e);
            var window = new MainWindow(); MainWindow = window; window.Show();
            await Task.Delay(2500); window.UpdateLayout();
            Window target=window;
            if(e.Args.Length>=3 && e.Args[2]=="media") {var mediaWindow=new MediaWindow(window);mediaWindow.Show();await Task.Delay(300);mediaWindow.UpdateLayout();target=mediaWindow;}
            var bitmap = new RenderTargetBitmap((int)target.ActualWidth, (int)target.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(target); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(e.Args[1])) encoder.Save(stream);
            Shutdown(); return;
        }
        base.OnStartup(e);
        MainWindow = new MainWindow(); MainWindow.Show();
    }
}
