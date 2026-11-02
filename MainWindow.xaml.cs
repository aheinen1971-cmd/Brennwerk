using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Brennwerk;
public partial class MainWindow : Window
{
    bool busy;
    bool selectingSource;
    UsbImage? selectedImage;
    string SelectedBoot => (BootChoice.SelectedItem as ComboBoxItem)?.Tag as string ?? "UEFI";
    public bool InstallingUpdate {get;private set;}
    PreparedUpdate? pendingUpdate;
    bool checkingUpdates;
    readonly System.Windows.Threading.DispatcherTimer updateTimer=new(){Interval=TimeSpan.FromSeconds(30)};
    readonly System.Windows.Threading.DispatcherTimer websiteTimer=new(){Interval=TimeSpan.FromMinutes(1)};
    bool checkingWebsites;
    void OpenWebsites(object sender,RoutedEventArgs e){if(!busy)new WebsiteWindow(this).ShowDialog();}
    async Task CheckWebsites()
    {
        if(checkingWebsites||busy||InstallingUpdate)return;checkingWebsites=true;
        try
        {
            var previous=WebsiteSafety.Load(WebsiteMonitor.ReportPath);
            if(previous!=null){int warnings=previous.Results.Count(r=>r.State!="Verified");WebsiteButton.Content=warnings==0?"Webseiten: geprüft":$"Webseiten: {warnings} Hinweise";}
            if(!WebsiteMonitor.IsDue())return;
            var report=await WebsiteMonitor.Refresh();int problems=report.Results.Count(r=>r.State!="Verified");
            WebsiteButton.Content=problems==0?"Webseiten: geprüft":$"Webseiten: {problems} Hinweise";
            AddLog($"Webseitenprüfung abgeschlossen: {report.Results.Count(r=>r.State=="Verified")} Adressen bestätigt, {problems} Hinweise. Nächste Prüfung in 6 Stunden.");
            if(report.Results.Any(r=>r.State is "Blocked" or "Unavailable"))await CheckUpdatesOnStartup(true);
        }
        catch(OperationCanceledException){}
        catch(Exception ex){AddLog("Webseitenprüfung: "+ex.Message);}
        finally{checkingWebsites=false;}
    }
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
    async Task CheckUpdatesOnStartup(bool force=false)
    {
        var settings=Updates.LoadSettings();
        if(checkingUpdates||!settings.AutoCheck||!force&&DateTime.UtcNow-settings.LastCheckUtc<TimeSpan.FromHours(12))return;
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
    async void GetIso(object sender,RoutedEventArgs e)
    {
        if(busy)return;
        var window=new IsoDownloadWindow(this,WindowsMode.IsChecked==true);
        if(window.ShowDialog()==true&&window.Result is {} result)
        {
            await SelectImage(result.Path);
            AddLog(result.PublisherHashVerified?"Original-ISO heruntergeladen und mit der Anbieter-Prüfsumme bestätigt.":"Original-ISO von Microsoft geladen; keine Anbieter-Vergleichsprüfsumme angegeben.");
        }
    }
    void OpenMedia(object sender,RoutedEventArgs e) { if(!busy) new MediaWindow(this).ShowDialog(); }
    public MainWindow()
    {
        InitializeComponent();Title="Brennwerk · Version "+Updates.VersionText;AppVersion.Text="Version "+Updates.VersionText+"\n\nLokal brennen.\nUpdates über GitHub.";
        updateTimer.Tick+=(_,_)=>{try{if(pendingUpdate!=null&&CanAutoInstall())InstallPreparedUpdate(pendingUpdate);}catch(Exception ex){updateTimer.Stop();AddLog("Update: "+ex.Message);}};
        websiteTimer.Tick+=async(_,_)=>await CheckWebsites();
        Loaded += async (_, _) => {await Refresh();if(!Environment.GetCommandLineArgs().Contains("--preview")){websiteTimer.Start();_ = CheckWebsites();await CheckUpdatesOnStartup();}};
        Closed+=(_,_)=>{updateTimer.Stop();websiteTimer.Stop();WebsiteMonitor.Cancel();};
    }
    void AddLog(string text) => Dispatcher.Invoke(() => { Log.AppendText($"{DateTime.Now:HH:mm:ss}  {text}{Environment.NewLine}"); Log.ScrollToEnd(); });
    async void ModeChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || selectingSource) return;
        ConfigureMode();
        selectedImage = null;
        ImagePath.Clear(); ImageInfo.Text = "Noch keine Quelle ausgewählt.";
        UpdateBootInfo();
        await Refresh();
    }
    void ConfigureMode()
    {
        bool data = DataMode.IsChecked == true, win = WindowsMode.IsChecked == true;
        Heading.Text = data ? "Daten auf Disc brennen" : IsDisc ? "ISO auf Disc brennen" : win ? "Windows-USB erstellen" : "Linux / IMG auf USB schreiben";
        ModeHint.Text = data ? "Einen Ordner samt Unterordnern brennen oder als Daten-ISO speichern." : IsDisc ? "ISO auf CD, DVD oder Blu-ray brennen; die Bootfähigkeit bleibt erhalten." : win ? "Windows-Installations-ISO automatisch erkennen und das Boot-Verfahren passend zum PC wählen." : "Hybrid-ISO oder bootfähiges IMG unverändert auf USB schreiben. Der Abbildtyp wird automatisch geprüft.";
        Notice.Text = IsDisc ? "Nur leere Rohlinge. Die Disc wird abgeschlossen. Eine Lesekontrolle der Disc ist in dieser Version noch nicht enthalten." : "Alle Partitionen und Dateien des USB-Ziels werden gelöscht. Vor dem Start Modell, Größe und Datenträgernummer prüfen.";
        Verify.Visibility = IsDisc ? Visibility.Collapsed : Visibility.Visible; BrowseButton.Content = data ? "Ordner wählen …" : "Datei wählen …";
        SaveIsoButton.Visibility = data ? Visibility.Visible : Visibility.Collapsed;
        StartButton.Content = IsDisc ? "Disc brennen" : "USB-Stick erstellen";
        GetIsoButton.Visibility=IsDisc?Visibility.Collapsed:Visibility.Visible;GetIsoButton.Content=win?"Windows-ISO von Microsoft holen …":"Aktuelle Linux-ISO holen …";
        TargetHint.Text = IsDisc ? "Leeren Rohling einlegen und Brenner auswählen. Kapazität wird vor dem Schreiben geprüft." : "Nur USB-Datenträger; System-, Start- und schreibgeschützte Laufwerke sind gesperrt.";
        BootOptions.Visibility = win ? Visibility.Visible : Visibility.Collapsed;
        BootInfo.Visibility = IsDisc ? Visibility.Collapsed : Visibility.Visible;
    }
    void BootChanged(object sender, SelectionChangedEventArgs e) { if(IsLoaded)UpdateBootInfo(); }
    void UpdateBootInfo()
    {
        if(IsDisc) return;
        try { BootInfo.Text = selectedImage is null ? "Das Abbild wird nach der Auswahl schreibgeschützt untersucht. Vor dem Schreiben erscheint eine Zusammenfassung." : UsbPlan.Create(selectedImage, selectedImage.Windows ? SelectedBoot : "Image").Description; }
        catch(Exception ex) { BootInfo.Text = ex.Message; }
    }
    public async Task SelectImage(string path)
    {
        if(busy)return;
        if(IsDisc) { ImagePath.Text=path;ImageInfo.Text=$"{new FileInfo(path).Length/1073741824.0:F2} GiB · {Path.GetFileName(path)}";return; }
        SetBusy(true);
        selectedImage=null; ImagePath.Clear();ImageInfo.Text="Abbild wird schreibgeschützt untersucht …";
        try
        {
            var image=await UsbImages.Inspect(path);
            if(!image.Windows&&!image.Hybrid)throw new IOException(image.Description);
            selectingSource=true;
            try
            {
                if(image.Windows)WindowsMode.IsChecked=true;else RawMode.IsChecked=true;
                ConfigureMode();
                BootChoice.SelectedIndex=image.Efi?0:1;
            }
            finally { selectingSource=false; }
            selectedImage=image;ImagePath.Text=image.Path;
            ImageInfo.Text=$"{image.Bytes/1073741824.0:F2} GiB · {image.Description}";
            Targets.SelectedIndex=-1;
            UpdateBootInfo();AddLog("Abbild erkannt: "+image.Description);
        }
        catch(Exception ex) { ImageInfo.Text="Abbild nicht ausgewählt: "+ex.Message;AddLog(ex.Message); }
        finally { SetBusy(false);Progress.IsIndeterminate=false;Status.Text="Bitte Ziel auswählen";UpdateBootInfo(); }
    }
    async void Browse(object sender, RoutedEventArgs e)
    {
        if (DataMode.IsChecked == true)
        {
            var dialog = new OpenFolderDialog();
            if (dialog.ShowDialog() == true) { ImagePath.Text = dialog.FolderName; ImageInfo.Text = "Der Inhalt dieses Ordners wird an die Wurzel der Daten-Disc übernommen."; }
        }
        else
        {
            var dialog = new OpenFileDialog { Filter = !IsDisc ? "Abbilder (*.iso;*.img)|*.iso;*.img" : "ISO-Abbilder (*.iso)|*.iso", CheckFileExists = true };
            if (dialog.ShowDialog() == true) await SelectImage(dialog.FileName);
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
        UsbPlan? plan=null;
        if(!disc)
        {
            SetBusy(true);
            try
            {
                var image=await UsbImages.Inspect(path);
                if(image.Windows!=win)throw new IOException("Der Abbildtyp passt nicht mehr zum gewählten Modus. Bitte die Quelle erneut auswählen.");
                plan=UsbPlan.Create(image,win?SelectedBoot:"Image");
                if(image.Bytes>target.Size)throw new IOException("Das Abbild ist größer als der ausgewählte USB-Datenträger.");
            }
            catch(Exception ex) { Status.Text="USB-Auftrag nicht gestartet";MessageBox.Show(this,ex.Message,"USB-Auftrag nicht gestartet");return; }
            finally { SetBusy(false);Progress.IsIndeterminate=false; }
            if(!ConfirmUsb(target,plan,verify))return;
        }
        SetBusy(true); string? temp = null;
        try
        {
            AddLog($"Start: {path} → {target.Label}");
            if (data) { temp = Path.Combine(Path.GetTempPath(), "Brennwerk-" + Guid.NewGuid() + ".iso"); await Optical.CreateIso(path, temp, AddLog); path = temp; }
            if (disc) await Optical.Burn(path, target.Id, AddLog);
            else await Backend.WriteUsb(path, target, win, verify, AddLog, plan!.BootMode);
            Progress.IsIndeterminate = false; Progress.Value = 100; Status.Text = "Abgeschlossen";
            AddLog(disc ? "Disc geschrieben und abgeschlossen. Keine Lesekontrolle durchgeführt." : verify ? "USB-Stick geschrieben und Datenprüfung erfolgreich." : "USB-Stick geschrieben; Datenprüfung abgewählt.");
            MessageBox.Show(this, "Der Vorgang wurde erfolgreich abgeschlossen.", "Brennwerk");
        }
        catch (Exception ex) { Status.Text = "Fehlgeschlagen"; AddLog("FEHLER: " + ex.Message); MessageBox.Show(this, ex.Message + "\n\nNach einem Schreibfehler kann das Zielmedium unvollständig sein.", "Vorgang fehlgeschlagen", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { if (temp != null) { try { File.Delete(temp); } catch { } } SetBusy(false); }
    }
    bool ConfirmUsb(Target target, UsbPlan plan, bool verify)
    {
        var dialog = new Window { Owner = this, Title = "USB-Auftrag prüfen und Ziel löschen", Width = 650, SizeToContent=SizeToContent.Height, MaxHeight=700, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = System.Windows.Media.Brushes.White };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = plan.Summary(target,verify)+$"\n\nZum Bestätigen eingeben: LÖSCHEN {target.Number}", TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.Black });
        var input = new TextBox { Margin = new Thickness(0, 16, 0, 16) }; panel.Children.Add(input);
        var button = new Button { Content = "Löschen und schreiben", IsEnabled = false };
        input.TextChanged += (_, _) => button.IsEnabled = input.Text == $"LÖSCHEN {target.Number}";
        button.Click += (_, _) => dialog.DialogResult = true; panel.Children.Add(button);
        dialog.Content = new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto}; return dialog.ShowDialog() == true;
    }
    void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (busy&&!InstallingUpdate) { e.Cancel = true; MessageBox.Show(this, "Bitte warte, bis der Schreibvorgang beendet ist.", "Vorgang läuft"); }
    }
}
