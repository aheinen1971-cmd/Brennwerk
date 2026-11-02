# Brennwerk 0.5.0

- Automatische Erkennung von Windows-Installationsdateien und partitionierten
  Hybrid-/IMG-Abbildern; passender USB-Modus wird automatisch gewählt.
- Windows: UEFI mit GPT/FAT32, BIOS/Legacy mit MBR/FAT32 oder beide mit MBR/FAT32.
- Fehlende BIOS-/UEFI-Startdateien werden vor der Löschbestätigung abgefangen.
  Der USB-Worker prüft diese Auswahl nochmals vor dem Löschen.
- BIOS-Bootcode ausschließlich über die vorhandene Windows-Systemdatei
  bootsect.exe; keine EXE aus der ausgewählten ISO wird gestartet.
- Linux-/IMG-Layout bleibt unverändert; kein nachträglicher Umbau des Bootverfahrens.
- Erweiterte Zusammenfassung: Quelldatei, Ziel, Seriennummer, Datenträgernummer,
  Bootverfahren, Partitionierung und Datenprüfung. Bestätigung mit exakter Nummer.
- Weiterhin eine einzelne EXE mit eingebautem USB-Modul, offiziellen ISO-Downloads
  und signiertem GitHub-Updatekanal.

Abbild-Erkennung, Boot-Auswahl, Schutzprüfungen und Oberfläche wurden ohne echte
USB-Schreibzugriffe geprüft. Die Windows-Dateistruktur-Test-ISO prüft die Erkennung,
enthält aber kein bootfähiges Betriebssystem. Echte USB-Schreib- und BIOS-/UEFI-
Bootversuche stehen aus. Windows-Hardwareanforderungen werden nicht verändert.
