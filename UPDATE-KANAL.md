# Signierter GitHub-Updatekanal

Standardprojekt: `aheinen1971-cmd/Brennwerk` (öffentlich).

Die App fragt `https://api.github.com/repos/aheinen1971-cmd/Brennwerk/releases/latest`
ab. Ein stabiles Release `vMAJOR.MINOR.PATCH` benötigt die Assets `Brennwerk.exe`
und `update.json`. Es werden nur HTTPS-Ziele von GitHub und dessen Release-Asset-
Servern verwendet. Ein anonymer Zugriff genügt; kein GitHub-Token wird in die App
eingebaut. Ältere und gleiche Versionen werden nicht installiert.

## Prüfung und Installation

Die UTF-8-JSON-Versionsbeschreibung ist als Base64-Payload mit einer RSA-PSS-
Signatur (SHA-256) verpackt. Der öffentliche Prüfschlüssel ist unveränderlich
in die jeweilige EXE eingebettet. Signiert sind Projekt, Version, Tag, Dateiname,
Dateigröße, SHA-256 und Versionshinweise. Die heruntergeladene EXE wird vor dem
Ersetzen erneut geprüft. Eine fremde Versionsbeschreibung mit demselben Dateinamen
reicht nicht aus.

Beim Start wird höchstens alle zwölf Stunden nach einer erfolgreichen Prüfung
erneut geprüft. Die manuelle Prüfung umgeht diese Wartezeit. Automatische
Installation ist zunächst ausgeschaltet. Wird sie eingeschaltet, lädt die App
neue Versionen und wartet auf Leerlauf: kein Schreibvorgang, kein offenes
Medienfenster und keine ausgewählte Quelle. Manuelle Installation ist im
Updatefenster möglich und startet die Anwendung neu.

Arbeitsdateien, Einstellungen und die Sicherung der alten EXE liegen unter
`%LOCALAPPDATA%\Brennwerk`. Neben der Desktop-EXE wird keine dauerhafte Zusatzdatei
benötigt. Ein kurzlebiger zweiter Prozess wartet auf das Beenden der App und
ersetzt die EXE atomar. Wenn Windows die neue EXE nicht starten kann, wird die
vorherige Datei wiederhergestellt. Ein späterer Absturz nach erfolgreichem
Prozessstart wird nicht automatisch erkannt; die vorherige Datei liegt im
jeweiligen Update-Arbeitsordner als `previous.exe`.

## Künftige Version veröffentlichen

1. Versionsnummer in `Brennwerk.csproj` erhöhen und `RELEASE-NOTES.md` aktualisieren.
2. Änderungen committen und den passenden Tag, z. B. `v0.3.1`, pushen.
3. Der Workflow baut und testet die EXE, signiert `update.json` und erstellt das
   Release einschließlich beider Dateien.

Der Workflow benötigt das Repository-Secret **BRENNWERK_UPDATE_KEY** mit dem
privaten PEM-Signierschlüssel. Dieser Schlüssel gehört niemals in ein Commit,
ein Release, eine Chatnachricht oder ein öffentliches Quellcodearchiv. Er wurde
separat lokal erzeugt und muss sicher gesichert werden. Ohne ihn können spätere
Versionen von bereits verteilten EXEs nicht verifiziert werden.

Das Signierwerkzeug unter `tools/ReleaseSigner` kann ein Release auch lokal signieren:

    dotnet run --project tools/ReleaseSigner -- sign Brennwerk.exe aheinen1971-cmd/Brennwerk 0.3.0 PRIVATE_PEM RELEASE-NOTES.md update.json

Der Signierer prüft, dass EXE-Version und Releaseversion übereinstimmen.
Der private Schlüssel bleibt außerhalb des Quellprojekts. Das öffentliche
`update-public.pem` gehört dagegen in das Repository.

## Tests

    dotnet run --project tools/UpdaterTests -c Release

Die Tests simulieren GitHub-Antworten ohne Netzwerkzugriff und prüfen gültige
Updates, ungültige Signaturen, fremde Projekte/Schlüssel/Dateinamen, Downgrades,
manipulierte und verkürzte Downloads, fremde/unsichere Weiterleitungen sowie
den Austausch einer Test-EXE mit Wiederherstellung nach einem Startfehler.
Es werden keine echten Medien oder installierten Apps ersetzt.
