# Brennwerk 0.4.2 – Original-ISOs und signierter Updatekanal

Eine eigenständige Windows-App für Daten-Discs, ISO-Abbilder und bootfähige USB-Sticks.

## Windows- und Linux-ISOs direkt herunterladen

Im Modus **Windows → USB** auf **Windows-ISO von Microsoft holen** klicken.
Die offizielle Microsoft-Seite wird im Programm geöffnet. Dort die aktuelle
Windows-11-ISO und Sprache auswählen und den ISO-Download starten. Brennwerk
fragt nach dem Speicherort auf dem PC und übernimmt den Download. EXE-Dateien
wie der Installationsassistent werden dabei nicht ausgeführt. Eine von Microsoft
veröffentlichte SHA-256-Prüfsumme kann vor dem Download optional eingegeben werden.
Ohne diese Eingabe prüft die App Dateigröße und ISO-/UDF-Struktur und berechnet
SHA-256, vergleicht aber keine Microsoft-Vergleichsprüfsumme.

Die eingebaute Microsoft-Seite benötigt Microsoft Edge WebView2 Runtime. Falls
sie fehlt oder Microsoft die Seite nicht lädt, kann die offizielle Seite im
Browser geöffnet und ein dort erzeugter Microsoft-ISO-Link eingefügt werden.
Microsofts zeitlich begrenzte Links werden nicht fest eingebaut oder als
dauerhafter Kanal gespeichert. Edition und Sprache werden bewusst bei Microsoft
ausgewählt; die Anwendung startet weder ein Windows-Upgrade noch akzeptiert sie
automatisch Vertragsbedingungen.

Im Modus **Linux / IMG → USB** auf **Aktuelle Linux-ISO holen** klicken. Zur Wahl:

- Ubuntu Desktop: neueste verfügbare LTS-Punktversion, amd64.
- Debian: aktuelle Standard-Netinstall-ISO, amd64; während der Installation ist
  zusätzlich Internet für weitere Pakete erforderlich.
- Fedora Workstation: aktuelle stabile Veröffentlichung, x86_64; Betas werden
  übersprungen.

**Aktuelle Version suchen** lädt die offiziellen Releaseinformationen und die
passende SHA-256-Prüfsumme. Der ISO-Download bleibt auf den offiziellen Servern
der gewählten Anbieter; für Fedora wird der kanonische Fedora-Server verwendet.
Debians offizielle Weiterleitung zum schwedischen Speicherverbund
`ftp.acc.umu.se` und dessen Servern wird ebenfalls unterstützt; die Vergleichs-
prüfsumme stammt weiterhin aus dem offiziellen Debian-Releaseverzeichnis.
Nach erfolgreicher Hash- und ISO-Strukturprüfung wird die Datei automatisch in
den richtigen USB-Modus übernommen. Neue Versionen werden beim erneuten Suchen
ermittelt; es gibt keinen unbeaufsichtigten, wiederkehrenden Gigabyte-Download.

Downloads lassen sich abbrechen. Unvollständige Dateien werden entfernt, eine
vorhandene Zieldatei wird erst nach erfolgreicher Prüfung ersetzt. Die App
startet nach einem Download nicht automatisch das Löschen oder Schreiben eines
USB-Sticks. Dafür muss weiterhin ein Ziel ausgewählt und bestätigt werden.

## Updates

Die Schaltfläche **Updates** öffnet den GitHub-Updatekanal. Automatische Prüfung
beim Start ist aktiviert. Automatisches Installieren kann dort eingeschaltet
werden und wartet auf Leerlauf; standardmäßig wird die Installation manuell
gestartet. Jede neue Version benötigt eine gültige Brennwerk-Signatur und eine
passende SHA-256-Prüfsumme. Details zur Veröffentlichung: [UPDATE-KANAL.md](UPDATE-KANAL.md).

## Medien lesen und sichern

Im Hauptfenster links **Discs lesen / sichern** öffnen. Die drei Bereiche sind
Film-DVD, Film-Blu-ray und Software-/Spiele-Disc.

- **Medienanalyse:** Zeigt DVD-/Blu-ray-Strukturen, sichtbare AACS-/BD+-Hinweise
  und einzelne Dateihinweise auf Software-Schutz an. Diese Hinweise sind keine
  vollständige oder beweiskräftige Erkennung. Ein unbekannter Schutz wird nicht
  als entfernt oder unterstützt ausgegeben.
- **DVD-/Blu-ray-Export über VLC:** Liest einen Filmtitel über eine vorhandene
  VLC-3.x-Installation (64 Bit) und speichert ihn ohne absichtliche Neukodierung
  als MKV. DVD-Titel ab 1; Blu-ray 0 wählt den von VLC ermittelten Haupttitel,
  weitere Titel ab 1. Menüs und die ursprüngliche Disc-Struktur werden nicht
  übernommen. CSS funktioniert nur, soweit die VLC-Lesemodule und das Laufwerk
  das konkrete Medium lesen können. Blu-rays setzen passende AACS-/BD+-Module,
  Konfiguration und Schlüssel voraus. Keine Schlüssel sind enthalten. Ein
  Remux kann Spuren abhängig von der Container-Unterstützung verlieren; das
  Ergebnis muss durch Wiedergabe kontrolliert werden.
- **DVDFab-Anbindung:** Erkennt lokale DVDFab-Installationen. Der Knopf
  „In DVDFab weiterarbeiten“ öffnet die gewählte Quelle über den lokal
  dokumentierten Befehl `/MODE OPENSOURCE /SRC`. Kopiermodus, Ausgabe und
  Auftragsstart erfolgen danach in DVDFab. Es wird weder eine Lizenzprüfung
  umgangen noch eine Freischaltung vorausgesetzt. DVDFab selbst entscheidet,
  welche Medien und Verfahren es mit den vorhandenen Modulen lesen kann.
  Es werden keine DVDFab- oder VLC-Dateien mitgeliefert oder kopiert.
- **Software-/Spiele-Dateisicherung:** Kopiert lesbare Dateien in einen neuen
  Ordner und prüft sie per SHA-256. Entfernt keinen Kopierschutz und erzeugt
  keine garantiert lauffähige Spielkopie. Physische Disc-Merkmale, Subkanäle,
  absichtliche Lesefehler sowie Lizenz- und Onlineprüfungen sind nicht abgebildet.
- **Abbruch:** Filmexport und Dateisicherung können abgebrochen werden.
  Unvollständige Filmausgaben werden verworfen; bereits geprüfte Sicherungsdateien
  bleiben erhalten. Die Quellmedien werden nur gelesen.

Die Filmlesemodule laufen in einem getrennten Prozess. Getestet sind echtes
MKV-Remuxen mit einer selbst erzeugten Tondatei einschließlich Unicode- und
Apostroph-Dateinamen, Dateisicherung mit Inhaltsvergleich, Medienanalyse und
die Erstellung der DVDFab-Übergabeparameter. Eine echte geschützte DVD/Blu-ray
wurde nicht ausgelesen. Der DVDFab-Kopierprozess und seine Freischaltung sind
nicht praktisch getestet. Ein erfolgreicher Filmexport ist kein Beweis für
vollständige Schutzentfernung oder fehlerfreie Wiedergabe.

Bei der lokalen Prüfung meldeten Oberfläche und Programmdatei DVDFab 13.0.7.0;
der ältere Installationseintrag in Windows nannte noch 13.0.5.9.

## Start

Windows 10/11, 64 Bit. Nur Brennwerk.exe wird benötigt. Die Datei kann direkt
vom Desktop gestartet werden. Die Windows-Abfrage für Administratorrechte bestätigen:
Diese Rechte werden zum Einrichten und Beschreiben von USB-Datenträgern benötigt.
Die .NET-Laufzeit ist enthalten; keine separate Installation nötig.
Die USB-Funktionen sind in die EXE eingebettet. Eine separate usb.ps1-Datei
wird nicht benötigt oder daneben ausgepackt. Die Anwendung ist nicht signiert.

## Enthaltene Funktionen

- **Daten-Disc:** Einen Quellordner einschließlich Unterordnern auf eine leere CD,
  DVD oder Blu-ray schreiben. Der Ordnerinhalt wird an die Wurzel übernommen.
  Alternativ mit „Als ISO speichern“ ein UDF/ISO-Abbild erzeugen.
- **ISO brennen:** Ein vorhandenes ISO-Abbild auf einen passenden leeren Rohling
  schreiben. Kapazität wird geprüft; die Disc wird finalisiert. Die Bootfähigkeit
  stammt aus dem gewählten ISO-Abbild.
- **Windows → USB:** Windows-Installations-ISOs mit UEFI-Startdateien übertragen.
  Der USB-Datenträger erhält eine FAT32-Partition von maximal 31 GiB. Größere
  install.wim-Dateien werden vor dem Löschen des USB-Ziels mit Windows DISM lokal
  in SWM-Dateien geteilt. Dafür wird zusätzlicher freier Speicher auf dem PC
  benötigt (mindestens WIM-Größe plus 1 GiB). Restlicher Platz auf großen Sticks
  bleibt unpartitioniert. Legacy-BIOS wird nicht eingerichtet.
- **Linux / IMG → USB:** Partitionierte Hybrid-ISOs und vollständige Datenträger-
  IMGs direkt schreiben. Eine Partitionstabelle mit MBR-Signatur ist erforderlich.
  Gewöhnliche optische ISOs und einzelne Dateisystem-Abbilder werden abgewiesen.
  Ob ein akzeptiertes Abbild tatsächlich startet, hängt vom Abbild und Ziel-PC ab.
- **USB-Datenprüfung:** Standardmäßig aktiviert. Im Windows-Modus werden alle
  kopierten Dateien einschließlich SWM-Teilen mit SHA-256 verglichen. Im direkten
  Schreibmodus werden die Abbildbytes vom Datenträger zurückgelesen und verglichen.

## Bedienung

1. Links die Aufgabe wählen.
2. Quellordner oder Abbilddatei auswählen.
3. Ziel ausdrücklich auswählen; es gibt keine automatische Zielauswahl.
4. Starten. Für USB die angezeigte Datenträgernummer prüfen und
   „LÖSCHEN Nummer“ eintippen. Alle Partitionen und Dateien dieses Ziels gehen verloren.
5. Bis zum Abschluss warten. Während des Schreibens nicht beenden, abstecken oder
   den PC ausschalten. Danach USB-Stick sicher entfernen und zum Booten neu verbinden.

USB-Festplatten können ebenfalls angezeigt werden. Modell und Größe prüfen.
System-, Start-, schreibgeschützte und offline befindliche Laufwerke sind gesperrt.
Vor dem Schreiben werden Nummer, Gerätekennung, Seriennummer und Größe erneut
verglichen. Das Quellabbild und die Anwendung dürfen nicht auf dem USB-Ziel liegen.
Für USB-Abbilder werden lokale Quelllaufwerke vorausgesetzt.

## Stand und Grenzen

Dies ist eine erste Testversion, keine abschließend auf Hardware geprüfte Ausgabe.
Erfolgreich geprüft wurden der Programm-Build, die Oberfläche, die Erkennung von
Laufwerken, echte ISO-Erstellung über Windows IMAPI und die USB-Schutzprüfungen.
Es wurden während der Entwicklung keine echten Discs gebrannt und keine echten
USB-Datenträger überschrieben. Windows-Setup-Sticks, große WIM-Dateien und das
Booten müssen deshalb noch mit entbehrlichen Testmedien praktisch geprüft werden.

Die Fortschrittsleiste zeigt die laufende Arbeit, keinen exakten Gesamtprozentsatz.
Im direkten USB-Modus erscheinen Schreibprozente im Protokoll. Ein laufender
Disc-/USB-Schreibvorgang lässt sich innerhalb der App nicht abbrechen. Die neuen
Lesefunktionen besitzen dagegen einen Abbruchknopf. Nach einem Fehler kann ein
USB-Ziel unvollständig eingerichtet und ein Rohling unbrauchbar sein.

Noch nicht enthalten: Lesekontrolle gebrannter Discs, Brenngeschwindigkeitswahl,
Multisession, Löschen von RW-Medien, Audio-CDs, Video-Authoring, direktes Kopieren
von Discs innerhalb der App, universelle Kopierschutz-Umgehung,
Software-Freischaltung, Projektverwaltung und geplante Backups. Daten-ISOs sind
nicht automatisch bootfähig. Verknüpfungen und umgeleitete Quellordner werden abgewiesen.

## Quellcode bauen und prüfen

Windows mit .NET 10 SDK:

    dotnet build -c Release
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File usb.ps1 -SelfTest
    dotnet bin/Release/net10.0-windows/Brennwerk.dll --selftest test-result.txt

Die Tests verwenden temporäre Dateien und Geräteabfragen. Sie schreiben nicht
auf physische Laufwerke. Für eine eigenständige Ausgabe:

    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist

## Technische Grundlagen

Windows WPF/.NET 10, IMAPI2/IMAPI2FS für optische Medien und Daten-ISOs,
Windows Storage PowerShell und DISM für Windows-USB, Win32-Volumesperren für
direktes Schreiben. Keine fremden Brennprogramme werden heruntergeladen.

Orientierung an Aufgabenstruktur und Arbeitsabläufen gängiger Brennprogramme;
eigene Oberfläche und eigener Code, ohne Ashampoo-Grafiken oder Komponenten.
