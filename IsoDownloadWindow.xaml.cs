using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;

namespace Brennwerk;
public partial class IsoDownloadWindow : Window
{
    WebView2? browser;
    CancellationTokenSource? cancellation;
    IsoSource? available;
    bool working,initializing;
    readonly TaskCompletionSource<bool> microsoftReady=new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<bool> MicrosoftReady=>microsoftReady.Task;
    public IsoDownloadResult? Result {get;private set;}
    string SelectedProvider=>Provider.SelectedIndex switch{0=>"Windows",1=>"Ubuntu",2=>"Debian",_=>"Fedora"};
    public IsoDownloadWindow(Window owner,bool windows)
    {
        InitializeComponent();Owner=owner;Resources.MergedDictionaries.Add(owner.Resources);
        Provider.SelectedIndex=windows?0:1;
        MicrosoftLink.TextChanged+=(_,_)=>DownloadButton.IsEnabled=!working&&Provider.SelectedIndex==0&&!string.IsNullOrWhiteSpace(MicrosoftLink.Text);
        Loaded+=async(_,_)=>await Configure();Closed+=(_,_)=>{cancellation?.Cancel();browser?.Dispose();};
    }
    async void ProviderChanged(object sender,SelectionChangedEventArgs e){if(IsLoaded)await Configure();}
    async Task Configure()
    {
        available=null;DownloadButton.IsEnabled=false;Progress.Value=0;bool windows=Provider.SelectedIndex==0;
        WindowsOptions.Visibility=windows?Visibility.Visible:Visibility.Collapsed;BrowserBorder.Visibility=windows?Visibility.Visible:Visibility.Collapsed;LinuxInfo.Visibility=windows?Visibility.Collapsed:Visibility.Visible;
        FindButton.Visibility=windows?Visibility.Collapsed:Visibility.Visible;ReleaseInfo.Text="Oben auf „Aktuelle Version suchen“ klicken.";SourceInfo.Text="";
        Description.Text=windows?"Auf der Microsoft-Seite „Windows 11-Datenträgerimage (ISO)“ wählen, Sprache bestätigen und den ISO-Download starten. Brennwerk übernimmt den Download. Keine Installation auf diesem PC.":SelectedProvider=="Debian"?"Debian Netinstall lädt weitere Pakete während der späteren Installation aus dem Internet. Quelle und Prüfsumme stammen von Debian.":"Aktuelle stabile Linux-ISO direkt aus dem offiziellen Releasekatalog. Ubuntu verwendet die neueste verfügbare LTS-Version.";
        Status.Text=windows?"Microsoft-Auswahl öffnen …":"Bereit für die Versionssuche";
        if(windows)await EnsureMicrosoft();
    }
    async Task EnsureMicrosoft()
    {
        if(initializing)return;
        if(browser?.CoreWebView2!=null){Status.Text="Edition und Sprache auf der Microsoft-Seite wählen.";return;}
        initializing=true;
        try
        {
            browser=new WebView2();BrowserSlot.Content=browser;
            string folder=Path.Combine(Updates.StateDirectory,"MicrosoftDownloadWebView");
            var environment=await CoreWebView2Environment.CreateAsync(userDataFolder:folder);
            await browser.EnsureCoreWebView2Async(environment);
            browser.CoreWebView2.Settings.AreDevToolsEnabled=false;browser.CoreWebView2.Settings.IsStatusBarEnabled=false;
            browser.CoreWebView2.NavigationStarting+=(_,e)=>{if(!Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri)||!IsoDownloads.MicrosoftPageUri(uri)){e.Cancel=true;Status.Text="Es werden hier nur offizielle Microsoft-Seiten geöffnet.";}};
            browser.CoreWebView2.NewWindowRequested+=(_,e)=>{e.Handled=true;if(Uri.TryCreate(e.Uri,UriKind.Absolute,out var uri)&&IsoDownloads.MicrosoftPageUri(uri))browser.CoreWebView2.Navigate(uri.AbsoluteUri);};
            browser.CoreWebView2.DownloadStarting+=(_,e)=>
            {
                string url=e.DownloadOperation.Uri;e.Cancel=true;e.Handled=true;
                Dispatcher.BeginInvoke(new Action(async()=>
                {
                    if(working)return;
                    try{var source=IsoDownloads.MicrosoftSource(url,ExpectedHash.Text);MicrosoftLink.Text=url;await SaveAndDownload(source);}
                    catch(Exception ex){Status.Text="Download nicht gestartet";AddLog(ex.Message);}
                }));
            };
            browser.CoreWebView2.NavigationCompleted+=(_,e)=>
            {
                bool success=e.IsSuccess&&e.HttpStatusCode>=200&&e.HttpStatusCode<400;
                microsoftReady.TrySetResult(success);
                if(!working)Status.Text=success?"Edition und Sprache auf der Microsoft-Seite wählen.":"Microsoft-Seite nicht erreichbar. Browser und Direktlink sind als Alternative verfügbar.";
            };
            browser.CoreWebView2.Navigate(IsoDownloads.MicrosoftPage);
        }
        catch(Exception ex){microsoftReady.TrySetResult(false);Status.Text="Microsoft-Seite im Browser öffnen und ISO-Link einfügen.";AddLog("Eingebaute Microsoft-Seite nicht verfügbar: "+ex.Message);}
        finally{initializing=false;}
    }
    void AddLog(string text)=>Dispatcher.Invoke(()=>{Log.AppendText(text+Environment.NewLine);Log.ScrollToEnd();});
    void SetWorking(bool value)
    {
        working=value;Options.IsEnabled=!value;BrowserBorder.IsEnabled=!value;CancelButton.IsEnabled=value;
        DownloadButton.IsEnabled=!value&&(available!=null||Provider.SelectedIndex==0&&!string.IsNullOrWhiteSpace(MicrosoftLink.Text));
        Progress.IsIndeterminate=value;
    }
    async void FindLatest(object sender,RoutedEventArgs e)
    {
        if(working)return;cancellation=new CancellationTokenSource();SetWorking(true);Status.Text="Offiziellen Releasekatalog prüfen …";
        try
        {
            available=await IsoDownloads.LatestLinux(SelectedProvider,cancellation.Token);
            ReleaseInfo.Text=available.Title+"\nVersion "+available.Version;
            SourceInfo.Text="Datei: "+available.FileName+"\nQuelle: "+available.Url.Host+"\nSHA-256: "+available.Sha256;
            Status.Text="Aktuelle ISO gefunden. Speicherort auf dem PC wählen.";
        }
        catch(OperationCanceledException){Status.Text="Versionssuche abgebrochen";}
        catch(Exception ex){available=null;Status.Text="Versionssuche fehlgeschlagen";AddLog(ex.Message);}
        finally{cancellation.Dispose();cancellation=null;SetWorking(false);}
    }
    async void DownloadClick(object sender,RoutedEventArgs e)
    {
        if(working)return;
        try{var source=Provider.SelectedIndex==0?IsoDownloads.MicrosoftSource(MicrosoftLink.Text,ExpectedHash.Text):available??throw new IOException("Bitte zuerst die aktuelle Version suchen.");await SaveAndDownload(source);}
        catch(Exception ex){Status.Text="Download nicht gestartet";AddLog(ex.Message);}
    }
    async Task SaveAndDownload(IsoSource source)
    {
        if(working)return;SetWorking(true);
        var dialog=new SaveFileDialog{Title="ISO auf dem PC speichern",Filter="ISO-Abbild|*.iso",FileName=source.FileName,OverwritePrompt=true,AddExtension=true};
        bool accepted;try{accepted=dialog.ShowDialog()==true;}catch{SetWorking(false);throw;}
        if(!accepted){SetWorking(false);return;}
        cancellation=new CancellationTokenSource();Status.Text="ISO wird heruntergeladen …";
        try
        {
            AddLog("Quelle: "+source.Url.GetLeftPart(UriPartial.Path));
            Result=await IsoDownloads.Download(source,dialog.FileName,AddLog,p=>Dispatcher.Invoke(()=>{Progress.IsIndeterminate=false;Progress.Value=p;}),cancellation.Token);
            SetWorking(false);DialogResult=true;
        }
        catch(OperationCanceledException){Status.Text="Download abgebrochen; unvollständige Datei verworfen.";}
        catch(Exception ex){Status.Text="ISO nicht übernommen";AddLog(ex.Message);}
        finally{cancellation.Dispose();cancellation=null;SetWorking(false);}
    }
    void OpenMicrosoft(object sender,RoutedEventArgs e)
    {
        try{Process.Start(new ProcessStartInfo(IsoDownloads.MicrosoftPage){UseShellExecute=true})?.Dispose();}
        catch(Exception ex){Status.Text="Browser konnte nicht geöffnet werden";AddLog(ex.Message);}
    }
    void Cancel(object sender,RoutedEventArgs e){cancellation?.Cancel();Status.Text="Wird abgebrochen …";}
    void OnClosing(object? sender,System.ComponentModel.CancelEventArgs e){if(working){e.Cancel=true;cancellation?.Cancel();Status.Text="Download wird abgebrochen …";}}
}
