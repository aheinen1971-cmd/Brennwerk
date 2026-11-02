# Brennwerk 0.7.0

- Fest hinterlegte, schreibgeschützte Anbieter-Liste; HTTPS, exakte Anbieterhosts
  und bekannte www-Varianten werden vor Seitenaufrufen geprüft.
- Fremde Weiterleitungen, Lookalike-Domains, URL-Anmeldedaten, unzulässige Ports
  und HTTP-Rückstufungen werden vor dem Zugriff blockiert.
- Microsoft-Assistent nur auf den freigegebenen Windows-11-Downloadseiten;
  ISO-Transfers weiterhin über die gesondert freigegebenen Downloadserver.
- WebView2-Zertifikatsfehler werden ausdrücklich abgebrochen. Keine TLS-Ausnahmen.
- Neue Übersicht „Webseiten prüfen“ mit Status je Quelle, lesbaren Fehlerdetails,
  letzter Prüfung und nächstem Prüftermin.
- Automatische Prüfung alle sechs Stunden bei geöffnetem Programm. Überfällige
  Prüfungen werden beim Start nachgeholt; keine Hintergrundaufgabe bei geschlossenem Programm.
- Automatische Fehlerbehandlung: einmaliger Wiederholungsversuch an der bekannten
  Originaladresse; beschädigte oder manipulierte gespeicherte Ergebnisse werden verworfen.
- Bei blockierten/nicht erreichbaren Quellen wird zusätzlich nach einem signierten
  Programm-Update gesucht, sofern automatische Updateprüfung aktiviert ist.
  Installation bleibt von den bestehenden Updateeinstellungen und Leerlauf abhängig.

Live-Ergebnis: 28 Katalogeinträge geprüft, 25 bestätigt und 3 Anbieter mit HTTP 403.
Die Anbieterablehnungen werden nicht als repariert ausgegeben. Andere Domains,
Zertifikate oder beliebige neue Seiten werden niemals automatisch freigegeben.
Die Prüfung bestätigt Adresse, HTTPS und HTTP-Weiterleitungen, nicht sämtliche
Inhalte oder die Sicherheit eines Betriebssystems. Weitere Navigation in einem
externen Browser wird von dessen eigenen Schutzfunktionen verwaltet.
