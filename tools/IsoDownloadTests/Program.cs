using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Brennwerk;

int checks=0;
void Check(bool condition,string name){if(!condition)throw new Exception(name);checks++;Console.WriteLine("PASS: "+name);}
async Task Reject(Func<Task> action,string name){bool blocked=false;try{await action();}catch(Exception ex)when(ex is IOException or HttpRequestException or OperationCanceledException){blocked=true;}Check(blocked,name);}
Check(IsoDownloads.TrustedUri("Windows",new Uri("https://software.download.prss.microsoft.com/dbazure/Win.iso?token=example")),"official Microsoft download accepted");
Check(!IsoDownloads.TrustedUri("Windows",new Uri("https://software.download.prss.microsoft.com.evil.invalid/Win.iso")),"Microsoft lookalike domain rejected");
Check(!IsoDownloads.TrustedUri("Ubuntu",new Uri("http://releases.ubuntu.com/test.iso")),"unencrypted download rejected");
Check(!IsoDownloads.TrustedUri("Debian",new Uri("https://user:password@cdimage.debian.org/test.iso")),"URL credentials rejected");
Check(!IsoDownloads.TrustedUri("Fedora",new Uri("https://dl.fedoraproject.org:8443/test.iso")),"unexpected port rejected");
await Reject(()=>Task.Run(()=>IsoDownloads.MicrosoftSource("https://download.microsoft.com/MediaCreationTool.exe",null)),"Microsoft executables are not run or treated as ISOs");
await Reject(()=>Task.Run(()=>IsoDownloads.MicrosoftSource("https://download.microsoft.com/Windows.iso","bad")),"invalid publisher checksum rejected before download");
Check(IsoDownloads.UbuntuVersion("<a href='24.04/'>LTS</a><a href='26.04/'>LTS</a><a href='26.10/'>new</a>")=="26.04","newest stable Ubuntu LTS folder selected");
Check(IsoDownloads.IsoName("<a href='ubuntu-26.04-desktop-amd64.iso'></a><a href='ubuntu-26.04.1-desktop-amd64.iso'></a><a href='ubuntu-26.04.1-live-server-amd64.iso'></a>","Ubuntu")=="ubuntu-26.04.1-desktop-amd64.iso","latest Ubuntu desktop point release selected");
Check(IsoDownloads.IsoName("<a href='debian-13.7.0-amd64-netinst.iso'></a><a href='debian-edu-13.7.0-amd64-netinst.iso'></a>","Debian")=="debian-13.7.0-amd64-netinst.iso","standard Debian netinstall selected instead of alternate editions");
byte[] iso=new byte[65536];Encoding.ASCII.GetBytes("CD001").CopyTo(iso,32769);string hash=Convert.ToHexString(SHA256.HashData(iso));
Check(IsoDownloads.Checksum(hash.ToLowerInvariant()+" *sample.iso\n","sample.iso")==hash,"official SHA256SUMS format parsed");
await Reject(()=>Task.Run(()=>IsoDownloads.Checksum(hash+"  other.iso","sample.iso")),"checksum for wrong ISO rejected");
string json="[{\"version\":\"45 Beta\",\"arch\":\"x86_64\",\"variant\":\"Workstation\"},{\"version\":\"44\",\"arch\":\"x86_64\",\"variant\":\"Workstation\",\"link\":\"https://download.fedoraproject.org/pub/Fedora.iso\",\"sha256\":\""+hash+"\",\"size\":\"65536\"}]";
var fedora=IsoDownloads.FedoraSource(json);Check(fedora.Version=="44"&&fedora.Url.Host=="dl.fedoraproject.org","Fedora beta skipped and canonical Fedora server selected");
string folder=Path.Combine(Path.GetTempPath(),"Brennwerk-iso-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
try
{
    string destination=Path.Combine(folder,"test.iso");var source=new IsoSource("Ubuntu","Test","26.04.1",new Uri("https://releases.ubuntu.com/26.04/test.iso"),"test.iso",hash,iso.Length);
    using var good=new HttpClient(new Fixture(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(iso)}));
    var downloaded=await IsoDownloads.Download(source,destination,_=>{},_=>{},transport:good);
    Check(downloaded.PublisherHashVerified&&File.ReadAllBytes(destination).AsSpan().SequenceEqual(iso),"ISO download, SHA-256 and filesystem validation succeed");
    byte[] previous=File.ReadAllBytes(destination),bad=(byte[])iso.Clone();bad[300]^=1;
    using var corrupt=new HttpClient(new Fixture(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(bad)}));
    await Reject(()=>IsoDownloads.Download(source,destination,_=>{},_=>{},transport:corrupt),"changed ISO rejected by publisher checksum");
    Check(previous.AsSpan().SequenceEqual(File.ReadAllBytes(destination)),"failed download preserves existing destination");
    using var shortened=new HttpClient(new Fixture(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(iso[..32768])}));
    await Reject(()=>IsoDownloads.Download(source,destination,_=>{},_=>{},transport:shortened),"truncated ISO rejected");
    using var html=new HttpClient(new Fixture(_=>new(HttpStatusCode.OK){Content=new StringContent("<html>expired link</html>",Encoding.UTF8,"text/html")}));
    await Reject(()=>IsoDownloads.Download(source,destination,_=>{},_=>{},transport:html),"HTML error page rejected instead of saving ISO");
    using var redirect=new HttpClient(new Fixture(_=>{var r=new HttpResponseMessage(HttpStatusCode.Redirect);r.Headers.Location=new Uri("https://untrusted.invalid/test.iso");return r;}));
    await Reject(()=>IsoDownloads.Download(source,destination,_=>{},_=>{},transport:redirect),"redirect outside official provider rejected");
    byte[] invalid=new byte[65536];var invalidSource=source with{Sha256=Convert.ToHexString(SHA256.HashData(invalid))};
    using var noIso=new HttpClient(new Fixture(_=>new(HttpStatusCode.OK){Content=new ByteArrayContent(invalid)}));
    await Reject(()=>IsoDownloads.Download(invalidSource,destination,_=>{},_=>{},transport:noIso),"file without ISO/UDF descriptors rejected despite matching hash");
    using var cancel=new CancellationTokenSource();cancel.Cancel();
    await Reject(()=>IsoDownloads.Download(source,destination,_=>{},_=>{},cancel.Token,good),"canceled download exits without replacing destination");
    Check(Directory.GetFiles(folder,"*.partial").Length==0,"partial downloads removed");
    using var catalog=new HttpClient(new Fixture(request=>new(HttpStatusCode.OK){Content=new StringContent(request.RequestUri!.AbsolutePath switch{"/"=>"<a href='26.04/'>Ubuntu LTS</a>","/26.04/"=>"<a href='ubuntu-26.04.1-desktop-amd64.iso'>ISO</a>",_=>hash+" *ubuntu-26.04.1-desktop-amd64.iso"})}));
    Check((await IsoDownloads.LatestLinux("Ubuntu",transport:catalog)).Version=="26.04.1","release discovery joins official directory and checksum");
    if(args.Contains("--live"))
    {
        foreach(string provider in new[]{"Ubuntu","Debian","Fedora"})
        {
            var live=await IsoDownloads.LatestLinux(provider);Check(live.Sha256?.Length==64&&live.FileName.EndsWith(".iso"),"live official "+provider+" catalogue: "+live.Version);Console.WriteLine("Source: "+live.Url.GetLeftPart(UriPartial.Path));
        }
    }
}
finally
{
    string full=Path.GetFullPath(folder),prefix=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))+Path.DirectorySeparatorChar;
    if(full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(full).StartsWith("Brennwerk-iso-tests-",StringComparison.Ordinal))Directory.Delete(full,true);
}
Console.WriteLine(checks+" ISO download checks passed. No real ISO or USB device was downloaded/written.");
sealed class Fixture(Func<HttpRequestMessage,HttpResponseMessage> response):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>Task.FromResult(response(request));
}
