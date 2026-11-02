# Brennwerk 0.6.0

- Geführter Windows-11-Download: x64/Arm64, aktuelle Microsoft-Edition, Sprache,
  frischer offizieller ISO-Link und automatisch zugeordnete Microsoft-SHA-256.
- Microsoft-Seite im Programm bleibt als Alternative erhalten, ebenso Browser
  und Direktlink. Alte Assistentenlinks und Prüfsummen werden beim Umschalten verworfen.
- Linux-Assistent mit 27 Katalogeinträgen und automatischer Prüfung bei der Auswahl.
- Direkte Versions-, ISO- und Prüfsummenermittlung für Ubuntu LTS, Debian Netinstall,
  Fedora Workstation und Mint Cinnamon, MATE sowie Xfce.
- Weitere Distributionen mit automatischer Erreichbarkeitsprüfung ihrer offiziellen
  Seite. Deren ISO-Auswahl und Prüfanleitung erfolgen noch auf der Anbieterseite.
- Keine privaten Microsoft-APIs, keine fremden Downloadskripte, keine gespeicherten
  zeitlich begrenzten Downloadlinks im Programm.

Live geprüft: Windows x64 und Arm64 jeweils Deutsch, alle sechs direkten Linux-
Katalogabfragen, Wechsel zur Microsoft-Alternative und zurück, Verwerfen alter
Auswahlen sowie ISO-Antworten von Microsoft und Mint mit jeweils 64 KiB Testdaten.
Keine erneuten vollständigen ISO-Downloads, keine USB-Schreib- oder Bootversuche.
Die drei zusätzlich geprüften Seiten MX, Lubuntu und Raspberry Pi OS lehnten
hier automatische HTTP-Abfragen ab; ihre offiziellen Seiten bleiben aufrufbar.

Bei späteren wiederholten Live-Tests verweigerte Microsoft neue Links mit Code 715-123130. Der Assistent bricht dann mit der Anbieterantwort ab und lässt die Microsoft-Alternative verfügbar; ein erfolgreicher Linkabruf kann nicht erzwungen werden.
