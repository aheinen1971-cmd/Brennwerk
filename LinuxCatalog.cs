namespace Brennwerk;
public sealed record LinuxDistribution(string Id,string Name,string Page,bool Direct,string Edition)
{
    public override string ToString()=>Name;
}
public static class LinuxCatalog
{
    public static readonly LinuxDistribution[] Items=[
        new("Ubuntu","Ubuntu Desktop LTS","https://ubuntu.com/download/desktop",true,"Desktop LTS · Intel/AMD 64 Bit"),
        new("Debian","Debian","https://www.debian.org/distrib/",true,"Netinstall · Intel/AMD 64 Bit"),
        new("Fedora","Fedora Workstation","https://fedoraproject.org/workstation/download",true,"Workstation · Intel/AMD 64 Bit"),
        new("Mint","Linux Mint Cinnamon","https://linuxmint.com/download.php",true,"Cinnamon · Intel/AMD 64 Bit"),
        new("MintMate","Linux Mint MATE","https://linuxmint.com/download.php",true,"MATE · Intel/AMD 64 Bit"),
        new("MintXfce","Linux Mint Xfce","https://linuxmint.com/download.php",true,"Xfce · Intel/AMD 64 Bit"),
        new("Arch","Arch Linux","https://archlinux.org/download/",false,"Rollende Veröffentlichung"),
        new("Manjaro","Manjaro","https://manjaro.org/products/download/x86",false,"Edition auf der offiziellen Seite wählen"),
        new("Endeavour","EndeavourOS","https://endeavouros.com/",false,"Offizielle Downloadseite"),
        new("OpenSUSE","openSUSE","https://get.opensuse.org/",false,"Leap oder Tumbleweed wählen"),
        new("Kali","Kali Linux","https://www.kali.org/get-kali/",false,"Installer oder Live-Ausgabe wählen"),
        new("Pop","Pop!_OS","https://system76.com/pop/",false,"Passende Grafikausgabe wählen"),
        new("Zorin","Zorin OS","https://zorin.com/os/download/",false,"Ausgabe auf der offiziellen Seite wählen"),
        new("Elementary","elementary OS","https://elementary.io/",false,"Offizielle Downloadseite"),
        new("MX","MX Linux","https://mxlinux.org/download-links/",false,"Desktop-Ausgabe wählen"),
        new("Tails","Tails","https://tails.net/install/",false,"Installationsanleitung und Abbild wählen"),
        new("Kubuntu","Kubuntu","https://kubuntu.org/getkubuntu/",false,"Ubuntu mit KDE Plasma"),
        new("Xubuntu","Xubuntu","https://xubuntu.org/download/",false,"Ubuntu mit Xfce"),
        new("Lubuntu","Lubuntu","https://lubuntu.me/downloads/",false,"Ubuntu mit LXQt"),
        new("UbuntuMate","Ubuntu MATE","https://ubuntu-mate.org/download/",false,"Ubuntu mit MATE"),
        new("Neon","KDE neon","https://neon.kde.org/download",false,"KDE-Plasma-Desktop"),
        new("Rocky","Rocky Linux","https://rockylinux.org/download",false,"Server-Ausgabe wählen"),
        new("Alma","AlmaLinux","https://almalinux.org/get-almalinux/",false,"Server-Ausgabe wählen"),
        new("Gentoo","Gentoo","https://www.gentoo.org/downloads/",false,"Abbild und Architektur wählen"),
        new("Nix","NixOS","https://nixos.org/download/",false,"Desktop oder minimale ISO wählen"),
        new("Alpine","Alpine Linux","https://alpinelinux.org/downloads/",false,"Standard oder Extended wählen"),
        new("Raspberry","Raspberry Pi OS","https://www.raspberrypi.com/software/operating-systems/",false,"Abbild für Raspberry Pi; andere Zielarchitektur")
    ];
}
