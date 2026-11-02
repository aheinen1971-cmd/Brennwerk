using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Brennwerk;
public partial class MainWindow : Window
{
    bool busy;
    public bool InstallingUpdate {get;private set;}
    PreparedUpdate? pendingUpdate;
    bool checkingUpdates;
    readonly System.Windows.Threading.DispatcherTimer updateTimer=new(){Interval=TimeSpan.FromSeconds(30)};
    void OpenUpdates(object sender,RoutedEventArgs e){if(!busy)new UpdateWindow(this).ShowDialog();}
    bool UpdateIdle => !busy && !InstallingUpdate && OwnedWindows.Count==0 && string.IsNullOrWhiteSpace(ImagePath.Text);
    bool CanAutoInstall()
    {
        var settings=Updates.LoadSettings();return UpdateIdle&&settings.AutoInstall&&pendingUpdate!=null&&string.Equals(settings.Repository,pendingUpdate.Update.Payload.Repository,StringComparison.OrdinalIgnoreCase);
    }
    public void InstallPreparedUpdate(PreparedUpdate prepared)
    {
        if(busy||OwnedWindows.Cast<Window>().Any(w=>w is not UpdateWindow))throw new IOException("Bitte zuerst andere Aufgaben und Medienfenster beenden.");
        Updates.BeginInstall(prepared);InstallingUpdate=true;updateTimer.Stop();Application.Current.Shutdown();
    }
    async Task CheckUpdatesOnStartup()
    {
        var settings=Updates.LoadSettings();
        if(checkingUpdates||!settings.AutoCheck||DateTime.UtcNow-settings.LastCheckUtc<TimeSpan.FromHours(12))return;
        checkingUpdates=true;
        try
        {
            var available=await Updates.Check(settings.Repository);settings.LastCheckUtc=DateTime.UtcNow;Updates.SaveSettings(settings);
            if(available==null)return;
            UpdateButton.Content="Update "+available.Payload.Version;AddLog("Update "+available.Payload.Version+" verfügbar.");
            var latestSettings=Updates.LoadSettings();
            if(latestSettings.AutoInstall&&string.Equals(latestSettings.Repository,available.Payload.Repository,StringComparison.OrdinalIgnoreCase)){pendingUpdate=await Updates.Download(available,AddLog);updateTimer.Start();if(CanAutoInstall())InstallPreparedUpdate(pendingUpdate);}
        }
        catch(Exception ex){AddLog("Update-Kanal: "+ex.Message);}
        finally{checkingUpdates=false;}
    }
    bool IsDisc => DiscMode.IsChecked == true || DataMode.IsChecked == true;
    void GetIso(object sender,RoutedEventArgs e)
    {
        if(busy)return;
        var window=new IsoDownloadWindow(this,WindowsMode.IsChecked==true);
        if(window.ShowDialog()==true&&window.Result is {} result)
        {
            if(result.Provider=="Windows")WindowsMode.IsChecked=true;else RawMode.IsChecked=true;
            ImagePath.Text=result.Path;ImageInfo.Text=$"{new FileInfo(result.Path).Length/1073741824.0:F2} GiB · {Path.GetFileName(result.Path)}";
            AddLog(result.PublisherHashVerified?"Original-ISO heruntergeladen und mit der Anbieter-Prüfsumme bestätigt.":"Original-ISO von Microsoft geladen; keine Anbieter-Vergleichsprüfsumme angegeben.");
        }
    }
    void OpenMedia(object sender,RoutedEventArgs e) { if(!busy) new MediaWindow(this).ShowDialog(); }
    public MainWindow()
    {
        InitializeComponent();Title="Brennwerk · Version "+Updates.VersionText;AppVersion.Text="Version "+Updates.VersionText+"\n\nLokal brennen.\nUpdates über GitHub.";
        updateTimer.Tick+=(_,_)=>{try{if(pendingUpdate!=null&&CanAutoInstall())InstallPreparedUpdate(pendingUpdate);}catch(Exception ex){updateTimer.Stop();AddLog("Update: "+ex.Message);}};
        Loaded += async (_, _) => {await Refresh();if(!Environment.GetCommandLineArgs().Contains("--preview"))await CheckUpdatesOnStartup();};
        Closed+=(_,_)=>updateTimer.Stop();
    }
    void AddLog(string text) => Dispatcher.Invoke(() => { Log.AppendText($"{DateTime.Now:HH:mm:ss}  {text}{Environment.NewLine}"); Log.ScrollToEnd(); });
    async void ModeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        bool data = DataMode.IsChecked == true, win = WindowsMode.IsChecked == true;
        Heading.Text = data ? "Daten auf Disc brennen" : IsDisc ? "ISO auf Disc brennen" : win ? "Windows-USB erstellen" : "Linux / IMG auf USB schreiben";
        ModeHint.Text = data ? "Einen Ordner samt Unterordnern brennen oder als Daten-ISO speichern." : IsDisc ? "ISO auf CD, DVD oder Blu-ray brennen; die Bootfähigkeit bleibt erhalten." : win ? "Windows-Installations-ISO für UEFI-PCs. Große install.wim wird automatisch geteilt." : "Hybrid-ISO oder bootfähiges IMG unverändert auf USB schreiben. Keine Windows-ISO verwenden.";
        Notice.Text = IsDisc ? "Nur leere Rohlinge. Die Disc wird abgeschlossen. Eine Lesekontrolle der Disc ist in dieser Version noch nicht enthalten." : "Alle Partitionen und Dateien des USB-Ziels werden gelöscht. Vor dem Start Modell, Größe und Datenträgernummer prüfen.";
        Verify.Visibility = IsDisc ? Visibility.Collapsed : Visibility.Visible; BrowseButton.Content = data ? "Ordner wählen …" : "Datei wählen …";
        SaveIsoButton.Visibility = data ? Visibility.Visible : Visibility.Collapsed;
        StartButton.Content = IsDisc ? "Disc brennen" : "USB-Stick erstellen";
        GetIsoButton.Visibility=IsDisc?Visibility.Collapsed:Visibility.Visible;GetIsoButton.Content=win?"Windows-ISO von Microsoft holen …":"Aktuelle Linux-ISO holen …";
        TargetHint.Text = IsDisc ? "Leeren Rohling einlegen und Brenner auswählen. Kapazität wird vor dem Schreiben geprüft." : "Nur USB-Datenträger; System-, Start- und schreibgeschützte Laufwerke sind gesperrt.";
        ImagePath.Clear(); ImageInfo.Text = "Noch keine Quelle ausgewählt.";
        await Refresh();
    }
    void Browse(object sender, RoutedEventArgs e)
    {
        if (DataMode.IsChecked == true)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true) { ImagePath.Text = dialog.FolderName; ImageInfo.Text = "Der Inhalt dieses Ordners wird an die Wurzel der Daten-Disc übernommen."; }
        }
        else
        {
            var dialog = new OpenFileDialog { Filter = RawMode.IsChecked == true ? "Abbilder (*.iso;*.img)|*.iso;*.img" : "ISO-Abbilder (*.iso)|*.iso", CheckFileExists = true };
            if (dialog.ShowDialog() == true) { ImagePath.Text = dialog.FileName; ImageInfo.Text = $"{new FileInfo(dialog.FileName).Length / 1073741824.0:F2} GiB · {Path.GetFileName(dialog.FileName)}"; }
        }
    }
    async void RefreshClick(object sender, RoutedEventArgs e) => await Refresh();
    async Task Refresh()
    {
        if (busy) return;
        Settings.IsEnabled = false; Navigation.IsEnabled = false; StartButton.IsEnabled = false; SaveIsoButton.IsEnabled = false;
        bool disc = IsDisc;
        try
        {
            Targets.ItemsSource = null;
            var targets = await Task.Run(() => disc ? Optical.List() : Backend.ListUsb());
            Targets.ItemsSource = targets;
            Status.Text = targets.Count == 0 ? "Kein passendes Laufwerk" : "Bitte Ziel auswählen";
        }
        catch (Exception ex) { AddLog(ex.Message); Status.Text = "Laufwerkerkennung fehlgeschlagen"; }
        finally { Settings.IsEnabled = true; Navigation.IsEnabled = true; StartButton.IsEnabled = true; SaveIsoButton.IsEnabled = true; }
    }
    void SetBusy(bool value)
    {
        busy = value; Settings.IsEnabled = !value; Navigation.IsEnabled = !value; StartButton.IsEnabled = !value; SaveIsoButton.IsEnabled = !value;
        Progress.IsIndeterminate = value; if (value) { Progress.Value = 0; Status.Text = "Vorgang läuft …"; }
    }
    async void SaveIso(object sender, RoutedEventArgs e)
    {
        if (busy || !Directory.Exists(ImagePath.Text)) { MessageBox.Show(this, "Bitte zuerst einen Datenordner wählen."); return; }
        var dialog = new SaveFileDialog { Filter = "ISO-Abbild|*.iso", FileName = "Daten.iso", OverwritePrompt = true };
        if (dialog.ShowDialog() != true) return;
        string source = ImagePath.Text;
        if (Path.GetFullPath(dialog.FileName).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(source)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, "Die ISO bitte außerhalb des Quellordners speichern."); return; }
        SetBusy(true);
        try { await Optical.CreateIso(source, dialog.FileName, AddLog); Status.Text = "ISO gespeichert"; Progress.Value = 100; }
        catch (Exception ex) { Status.Text = "Fehlgeschlagen"; AddLog(ex.Message); MessageBox.Show(this, ex.Message, "ISO konnte nicht erstellt werden"); }
        finally { SetBusy(false); }
    }
    async void Start(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        bool data = DataMode.IsChecked == true, disc = IsDisc, win = WindowsMode.IsChecked == true, verify = Verify.IsChecked == true;
        string path = ImagePath.Text;
        if (!(data ? Directory.Exists(path) : File.Exists(path))) { MessageBox.Show(this, "Bitte zuerst eine gültige Quelle auswählen."); return; }
        if (Targets.SelectedItem is not Target target) { MessageBox.Show(this, "Bitte ein Ziellaufwerk auswählen."); return; }
        if (disc)
        {
            if (MessageBox.Show(this, $"Quelle: {path}\nBrenner: {target.Label}\n\nDisc jetzt beschreiben und abschließen?", "Brennen bestätigen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        }
        else if (!ConfirmUsb(target, path)) return;
        SetBusy(true); string? temp = null;
        try
        {
            AddLog($"Start: {path} → {target.Label}");
            if (data) { temp = Path.Combine(Path.GetTempPath(), "Brennwerk-" + Guid.NewGuid() + ".iso"); await Optical.CreateIso(path, temp, AddLog); path = temp; }
            if (disc) await Optical.Burn(path, target.Id, AddLog);
            else await Backend.WriteUsb(path, target, win, verify, AddLog);
            Progress.IsIndeterminate = false; Progress.Value = 100; Status.Text = "Abgeschlossen";
            AddLog(disc ? "Disc geschrieben und abgeschlossen. Keine Lesekontrolle durchgeführt." : verify ? "USB-Stick geschrieben und Datenprüfung erfolgreich." : "USB-Stick geschrieben; Datenprüfung abgewählt.");
            MessageBox.Show(this, "Der Vorgang wurde erfolgreich abgeschlossen.", "Brennwerk");
        }
        catch (Exception ex) { Status.Text = "Fehlgeschlagen"; AddLog("FEHLER: " + ex.Message); MessageBox.Show(this, ex.Message + "\n\nNach einem Schreibfehler kann das Zielmedium unvollständig sein.", "Vorgang fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { if (temp != null) { try { File.Delete(temp); } catch { } } SetBusy(false); }
    }
    bool ConfirmUsb(Target target, string path)
    {
        var dialog = new Window { Owner = this, Title = "USB-Ziel vollständig löschen", Width = 580, Height = 390, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = System.Windows.Media.Brushes.White };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = $"ALLE DATEN AUF DIESEM ZIEL WERDEN GELÖSCHT:\n\n{target.Label}\nSeriennummer: {target.Serial}\n\nAbbild: {Path.GetFileName(path)}\n\nZum Bestätigen eingeben: LÖSCHEN {target.Number}", TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Black });
        var input = new TextBox { Margin = new Thickness(0, 16, 0, 16) }; panel.Children.Add(input);
        var button = new Button { Content = "Löschen und schreiben", IsEnabled = false };
        input.TextChanged += (_, _) => button.IsEnabled = input.Text == $"LÖSCHEN {target.Number}";
        button.Click += (_, _) => dialog.DialogResult = true; panel.Children.Add(button);
        dialog.Content = panel; return dialog.ShowDialog() == true;
    }
    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (busy&&!InstallingUpdate) { e.Cancel = true; MessageBox.Show(this, "Bitte warte, bis der Schreibvorgang beendet ist.", "Vorgang läuft"); }
    }
}
