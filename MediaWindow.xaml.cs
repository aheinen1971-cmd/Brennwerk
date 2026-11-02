using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Brennwerk;
public partial class MediaWindow : Window
{
    CancellationTokenSource? cancellation;
    bool busy;
    public MediaWindow(Window owner)
    {
        InitializeComponent(); Owner=owner; Resources.MergedDictionaries.Add(owner.Resources);
        Source.ItemsSource=MediaTools.OpticalDrives();
        Engine.Text="VLC: "+(MediaTools.FindVlc()!=null?"vorhanden":"fehlt")+" · DVDFab: "+(MediaTools.FindDvdfab()!=null?"vorhanden; Freischaltung nicht geprüft":"nicht gefunden");
        FabButton.IsEnabled=MediaTools.FindDvdfab()!=null;
    }
    void KindChanged(object sender,System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if(Notice==null) return;
        bool software=Kind.SelectedIndex==2, bluray=Kind.SelectedIndex==1;
        TitleOptions.Visibility=software?Visibility.Collapsed:Visibility.Visible;
        TitleNumber.Text=bluray?"0":"1"; TitleHint.Text=bluray?"0 = längster Titel; weitere Titel ab 1":"DVD: ab 1";
        ExportButton.Content=software?"Dateisicherung erstellen …":"Filmtitel als MKV exportieren …";
        FabButton.Visibility=software?Visibility.Collapsed:Visibility.Visible;
        Notice.Text=software?"Die Dateisicherung kopiert lesbare Dateien und prüft sie mit SHA-256. Sie entfernt keinen Software-Kopierschutz, ersetzt keine Lizenzprüfung und reproduziert keine besonderen Disc-Merkmale. Dateinamen liefern nur Hinweise auf Schutzverfahren.":bluray?"Blu-ray-Export benötigt eine passende lokale VLC/AACS-/BD+-Konfiguration und gegebenenfalls Disc-Schlüssel. Diese App enthält keine Schlüssel. Export eines Filmtitels als MKV; keine vollständige Disc-Kopie und keine Menüs.":"DVD-Export liest einen Filmtitel über die lokale VLC-Installation und speichert ihn als MKV. CSS kann gelesen werden, soweit VLC und das Laufwerk dies unterstützen. Kein vollständiges Disc-Abbild und keine Menükopie.";
    }
    void BrowseSource(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFolderDialog {Title="Disc-Wurzel oder Medienordner wählen"};
        if(dialog.ShowDialog()==true) Source.Text=dialog.FolderName;
    }
    void AddLog(string text)=>Dispatcher.Invoke(()=>{Log.AppendText(text+Environment.NewLine);Log.ScrollToEnd();});
    void SetBusy(bool value)
    {
        busy=value;Options.IsEnabled=!value;ExportButton.IsEnabled=!value;FabButton.IsEnabled=!value&&MediaTools.FindDvdfab()!=null;CancelButton.IsEnabled=value;Progress.IsIndeterminate=value;
        if(value) {Progress.Value=0;Status.Text="Vorgang läuft …";}
    }
    async void Analyze(object sender,RoutedEventArgs e)
    {
        if(busy) return;string source=Source.Text;
        SetBusy(true);CancelButton.IsEnabled=false;
        try {AddLog(await Task.Run(()=>MediaTools.Analyze(source)));Status.Text="Analyse abgeschlossen – Hinweise, keine vollständige Schutzdiagnose";}
        catch(Exception ex){AddLog("FEHLER: "+ex.Message);Status.Text="Quelle nicht lesbar";}
        finally {SetBusy(false);}
    }
    async void Export(object sender,RoutedEventArgs e)
    {
        if(busy) return;string source=Source.Text;
        if(!Directory.Exists(source)){MessageBox.Show(this,"Bitte eine lesbare Disc oder einen Medienordner auswählen.");return;}
        bool software=Kind.SelectedIndex==2;string destination;int title=0;
        if(software)
        {
            var dialog=new OpenFolderDialog {Title="Übergeordneten Sicherungsordner wählen"};
            if(dialog.ShowDialog()!=true)return;
            destination=Path.Combine(dialog.FolderName,"Disc-Sicherung-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..6]);
        }
        else
        {
            if(!int.TryParse(TitleNumber.Text,out title)||title<0||title>999||(Kind.SelectedIndex==0&&title==0)){MessageBox.Show(this,"Bitte eine gültige Titelnummer eingeben.");return;}
            string required=Kind.SelectedIndex==0?"VIDEO_TS":"BDMV";
            if(!Directory.Exists(Path.Combine(source,required))){MessageBox.Show(this,"Bitte die Disc-Wurzel mit dem Ordner "+required+" wählen.");return;}
            var dialog=new SaveFileDialog {Filter="Matroska-Film|*.mkv",FileName="Film.mkv",OverwritePrompt=true};
            if(dialog.ShowDialog()!=true)return;destination=dialog.FileName;
        }
        SetBusy(true);cancellation=new CancellationTokenSource();
        try
        {
            AddLog("Quelle: "+source);AddLog("Ziel: "+destination);
            if(software)await Task.Run(()=>MediaTools.BackupFiles(source,destination,AddLog,cancellation.Token));
            else await MediaTools.Export(new MediaRequest(source,destination,Kind.SelectedIndex==0?"dvd":"bluray",title),AddLog,cancellation.Token);
            Progress.Value=100;Status.Text=software?"Dateien gesichert und geprüft; kein Kopierschutz entfernt":"Export beendet – Wiedergabe und Vollständigkeit prüfen";
        }
        catch(OperationCanceledException){Status.Text="Abgebrochen";AddLog(software?"Abgebrochen. Bereits geprüfte Dateien verbleiben im Sicherungsordner.":"Abgebrochen. Unvollständige Filmausgabe verworfen.");}
        catch(Exception ex){Status.Text="Fehlgeschlagen";AddLog("FEHLER: "+ex.Message);}
        finally {cancellation.Dispose();cancellation=null;SetBusy(false);}
    }
    void Cancel(object sender,RoutedEventArgs e){cancellation?.Cancel();CancelButton.IsEnabled=false;Status.Text="Wird abgebrochen …";}
    void OpenFab(object sender,RoutedEventArgs e)
    {
        if(busy)return;
        try
        {
            MediaTools.OpenDvdfab(Source.Text);
            AddLog("Quelle an die vorhandene DVDFab-Installation übergeben. Kopiermodus und Ausgabe in DVDFab wählen; dort den Auftrag starten. Freischaltung wird von DVDFab verwaltet.");
            Status.Text="In DVDFab weiterarbeiten; keine abgeschlossene Kopie bestätigt";
        }
        catch(Exception ex){AddLog("FEHLER: "+ex.Message);}
    }
    void OnClosing(object? sender,System.ComponentModel.CancelEventArgs e)
    {
        if(busy){cancellation?.Cancel();e.Cancel=true;Status.Text="Bitte warten, bis der Vorgang beendet ist.";}
    }
}
