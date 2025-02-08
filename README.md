# Tecalor/Stiebel Eltron Heatpump Bridge
---------------
## Beschreibung
Dieser Code implementiert eine Schnittstelle zu einer Tecalor/Stiebel Eltron Wärmepumpe über den CAN-Bus. Folgende Schnittstellen werden unterstützt:

* USBtin (Version HW10, SW00 - siehe Fischl.de) mit dem Protokoll von LAWICEL CANUSB
* Tecalor TTL 10 AC (Stibel Eltron WPL 10 AC) mit FEK und WPM3

Die Kommunikation mit der Wärmepumpe erfolgt über den CAN-Bus, sowohl lesend als auch schreibend. Die Ergebnisse werden im Speicher des HeatingMqttService gehalten und sofort an einen MQTT-Message-Broker weitergeleitet. Das Wording lehnt sich dabei stark an die FHEM-Wärmepumpen-Implementierung an (siehe auch unten). Durch die MQTT-Anbindung sind auch Integrationen in andere Hausautomatisierungssysteme möglich. Der HeatingMqttService ist ein .NET 8 Linux systemd Service und kann später neben FHEM betrieben werden.

<img src="doc/HeatingMqttService_overview.png" width="800">

## FHEM
[Installation und Visualisierung in FHEM](doc/fhem.md)

### Quellen
Dieses Programm basiert auf den Arbeiten von:
* Jürg <http://juerg5524.ch/>
* Immi (THZ-Modul)
* Radiator
* Robots <https://github.com/robots/Elster/>
  
## Warnung
Die Verwendung des Codes erfolgt auf eigene Gefahr. Es wird keine Gewährleistung für die Richtigkeit oder Vollständigkeit der Software übernommen. Der Autor haftet nicht für Schäden, die durch die Verwendung dieser Software entstehen, insbesondere nicht für Schäden an der Wärmepumpe. Also aufpassen, der nächste Winter wird kommen.

Use of this code is at your own risk. No warranty is given for the correctness or completeness of the software. The author is not liable for any damages that may arise from the use of this software, particularly not for damages to the heat pump. So be careful, winter is coming.

# Testaufbau und Entwicklung
---------------
Um im Echtbetrieb zu entwickeln, ohne mein FHEM-System zu beschädigen, habe ich die CAN-Bus-Daten an meinen PC weitergeleitet.

<img src="doc/testsetup.png" width="800">

Dieses Projekt wurde in Zusammenarbeit mit einer Künstlichen Intelligenz entwickelt, um die Vorteile des Extreme Programming in Kombination mit KI-Tools wie Codeium und Copilot zu erproben. Die KI-Tools werden genutzt, um Git, GitHub, VS Code sowie Übersetzungen zwischen Programmiersprachen und die Korrektur von Englisch nach Deutsch (und umgekehrt) zu unterstützen. Eine ausgezeichnete Möglichkeit, moderne Technologien zu integrieren.

## Vorschläge und offene Aufgaben

- [x] Implementierung des Lesens von Nachrichten auf dem Bus, die passiv gesendet werden
- [x] Implementieren von Schreiben auf den Bus und Abfragen von bestimmten Elster-Werten
- [x] ElsterValue aus einem ElsterCanFrame als Eigenschalft zur Verfügung stellen
- [x] Zeitstempel beim Protokollieren
- [x] Implementieren eines Bus-Scans pro Module / aller Module
- [x] Implementieren von einer Konfigurationsmöglichkeit, die Readingname, SenderCanID, Funktion oder ElsterValue und Abfragezyklus übernimmt. 
- [x] Implementation dieser zyklischen Abfragefunktion. Funktionen sind ggf. ausgewertete ElsterValue-Werte in Text. Ohne zyklische Abfragen werden passive Telegramme ausgewertet, also die, die so oder so gesendet werden.
- [x] Implementieren der Konfigurationen für MQTT-Ausleitung und zyklisches Abfragen von bestimmten Werten
- [x] Deployment auf FHEM 
- [x] Sammeln aller passiven Werte auf dem Bus
- [x] Framework-Dependent Deployment (FDD):dotnet publish -c Release -r linux-arm --self-contained false /p:PublishSingleFile=true /p:DebugType=none
- [x] module_scan als Parameter implementieren und in readme dokumentieren
- [x] Passive Telegramme per Parameter für einen bestimmten Zeitraum starten und in readme dokumentieren
- [ ] Can_Scan Module auf gültige Elster-Werte
- [ ] Implementieren der Sammelfehler- und Fehlerlisten-Funktion
- [ ] Fehlermeldung an ComfortSoft sollten ausgewerten werden: RemoteControl ->Write ComfortSoft FEHLERMELDUNG = 20805
- [ ] Zu prüfen: Werden drei CR gesendet nach dem öffnen um den internen USBtin-Puffer zu leeren
- [ ] Zu prüfen: Werden alle 300 - 500 ms F gesendet um auf Fehler zu prüfen--> Sollte unabhängig vom Bell-Error ermittelt werden.
- [ ] WP_DHC_Stufe implementieren (Relais)
- [ ] Implementieren der FEK-Funktionen: Setzen der Heizkurve, Raumeinfluss und Heizkuvenfußpunkt(vermutlich unmöglich)
- [ ] Implementieren der WPM-Funktionen: Auslesen der Temperaturen, Umschaltung auf Sommerbetrieb
- [ ] Implementieren der Warmwassersteuerung: Temperaturfestlegung für Extra Warmwasser (WE), Zeitpunktfestlegung (Wenn wärmster Zeitpunkt und angeschlossen an Heizungsvorgang)

## Telegrammaufbau
<img src="doc/telegram.png" width="800">

## Installation
.net 8.0 installieren 
```
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x ./dotnet-install.sh
./dotnet-install.sh --version latest --runtime aspnetcore

echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools' >> ~/.bashrc
```

Die Konfiguration befindet sich normalerweise in der `appsettings.json` Datei. Konfigurationen können auch über den Befehl `dotnet run --HeatingAdapterConfig:PortName="/dev/ttyACM0"` gesetzt werden. Das Programm basiert auf Microsoft .NET Core 8, was eine Voraussetzung ist.

Die Datei [HeatingDaemon/HeatingDaemon.service](HeatingDaemon/HeatingDaemon.service) muss in das Verzeichnis `/etc/systemd/system/` abgelegt werden.

Mit den Befehlen:

	sudo systemctl daemon-reload
	sudo systemctl enable HeatingDaemon.service
	sudo systemctl start HeatingDaemon.service

kann der Service aktiviert und gestartet werden. Log-Daten können mit dem Befehl:

	sudo journalctl -u HeatingDaemon

angeschaut werden.

Für die spätere Konfiguration sind die folgenden Parameter des HeatingDaemon sehr nützlich. Dabei wird nicht der Daemon gestartet, sondern das Programm HeatingMqttService direkt. Dieses beendet sich auch, nachdem die Parameter verarbeitet wurden. Die Konfiguration aus der appsettings.json ist auch bei den Parametern wirksam, z.B. die Konfiguration für den standard sender can id.


Der Parameter `module_scan` dient zum Scannen der verfügbaren Module der Heizungsanlage:
```
HeatingMqttService --module_scan=[SenderCanID]

   SenderCanID: optional, default is standard CanId from appsettings.json. Hex-Value or module name (e.g. 700 or ExternalDevice

Example: HeatingMqttService --module_scan=default         (scan all modules with default sender can id)
OR       HeatingMqttService --module_scan=700             (use 700 as sender can id to scan all modules)
OR       HeatingMqttService --module_scan=ExternalDevice  (use 700 as sender can id to scan all modules)
```
<details><summary>Ausgabe von module_scan</summary>

```
info: HeatingDaemon.HeatingAdapter[0] scan on CAN-id: 700
info: HeatingDaemon.HeatingAdapter[0] list of valid can id's:
info: HeatingDaemon.HeatingAdapter[0]
info: HeatingDaemon.HeatingAdapter[0]
info: HeatingDaemon.HeatingAdapter[0]   000 (C306 = 195-06)
info: HeatingDaemon.HeatingAdapter[0]   100 (8000 = 392-03)
info: HeatingDaemon.HeatingAdapter[0]   180 (8000 = 128-00)
info: HeatingDaemon.HeatingAdapter[0]   301 (C306 = 195-06)
info: HeatingDaemon.HeatingAdapter[0]   302 (8000 = 128-00)
info: HeatingDaemon.HeatingAdapter[0]   480 (8000 = 128-00)
info: HeatingDaemon.HeatingAdapter[0]   500 (4310 = 67-16)
info: HeatingDaemon.HeatingAdapter[0]   601 (8000 = 128-00)
info: HeatingDaemon.HeatingAdapter[0]   602 (8000 = 128-00)
info: HeatingDaemon.HeatingAdapter[0]   680 (8000 = 128-00)
info: HeatingDaemon.HeatingAdapter[0]
info: HeatingDaemon.HeatingAdapter[0] Scanning for Elster modules finished
info: HeatingDaemon.HeatingAdapter[0] Found Elster module:          Direct (000) = Device-ID:   C306 | SW-Nr: N/A    | SW-Ver: N/A
info: HeatingDaemon.HeatingAdapter[0] Found Elster module:     FES_COMFORT (100) = Device-ID:   8000 | SW-Nr: 392    | SW-Ver: 3
info: HeatingDaemon.HeatingAdapter[0] Found Elster module:          Boiler (180) = Device-ID:   8000 | SW-Nr: N/A    | SW-Ver: N/A
info: HeatingDaemon.HeatingAdapter[0] Found Elster module:   RemoteControl (301) = Device-ID:   C306 | SW-Nr: N/A    | SW-Ver: N/A
info: HeatingDaemon.HeatingAdapter[0] Found Elster module:  RemoteControl2 (302) = Device-ID:   8000 | SW-Nr: N/A    | SW-Ver: N/A
info: HeatingDaemon.HeatingAdapter[0] Found Elster module:         Manager (480) = Device-ID:   8000 | SW-Nr: N/A    | SW-Ver: N/A
info: HeatingDaemon.HeatingAdapter[0] Found Elster module:   HeatingModule (500) = Device-ID:   4310 | SW-Nr: N/A    | SW-Ver: N/A
info: HeatingDaemon.HeatingAdapter[0] Found Elster module:           Mixer (601) = Device-ID:   8000 | SW-Nr: N/A    | SW-Ver: N/A
info: HeatingDaemon.HeatingAdapter[0] Found Elster module:          Mixer2 (602) = Device-ID:   8000 | SW-Nr: N/A    | SW-Ver: N/A
info: HeatingDaemon.HeatingAdapter[0] Found Elster module:     ComfortSoft (680) = Device-ID:   8000 | SW-Nr: N/A    | SW-Ver: N/A
```
</details>



Mit dem Parameter `msg_scan` können alle passiven CAN-Telegramme, also Telegramme die permanent zwischen den einzelnen Modulen der Heizungsanlage gesendet werden,
gesammelt und am Ende mit ihrer Häufigkeit protokolliert werden. Diese Telegramme können auch über den MQTT-Broker ausgeleitet werden, ohne den CAN-Bus mit
weiteren Anfragen zu belasten. Stattdessen können diese Telegramme mit dem ScheduleType `Passive` in der appsettings.json angegeben werden. Um alle passiven Telegramme zu erfassen,
muss eine bestimmte Zeit lang gesammelt werden, standardmäßig 10 Stunden, was aber auch per Parameter geändert werden kann.

```
Syntax:
HeatingMqttService --msg_scan=[timespan]

   timespan: optional, collection time span in ISO 8601 format (e.g. PT10h)

Example: HeatingMqttService --msg_scan=PT10h        (collect all telegrams with an elster value for 10 hours)
OR       HeatingMqttService --msg_scan=             (collect all telegrams with an elster value for 10 hours)

```
<details><summary>Ausgabe von module_scan</summary>

```
info: HeatingDaemon.HeatingAdapter[0] Passive Elster Telegrams:
info: HeatingDaemon.HeatingAdapter[0]   30x  RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
info: HeatingDaemon.HeatingAdapter[0]   25x  RemoteControl ->Respond on ExternalDevice RAUMSOLLTEMP_I 19.9
info: HeatingDaemon.HeatingAdapter[0]   24x  RemoteControl ->Respond on ExternalDevice RAUMSOLLTEMP_NACHT 18.0
info: HeatingDaemon.HeatingAdapter[0]   6x  Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP 4.2
info: HeatingDaemon.HeatingAdapter[0]   6x  Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 23.7
info: HeatingDaemon.HeatingAdapter[0]   3x  HeatingModule ->Respond on Manager SOFTWARE_NUMMER 67 (0x0043)
info: HeatingDaemon.HeatingAdapter[0]   3x  HeatingModule ->Respond on Manager WPVORLAUFIST 24.4
info: HeatingDaemon.HeatingAdapter[0]   3x  Manager ->Write on HeatingModule HEIZSYSTEMTEMP_GEWICHTET 220 (0x00DC)
info: HeatingDaemon.HeatingAdapter[0]   3x  HeatingModule ->Respond on Manager RUECKLAUFISTTEMP 19.0
info: HeatingDaemon.HeatingAdapter[0]   3x  Manager ->Write on HeatingModule INTEGRAL_REGELABWEICHUNG_RELATIV 0 (0x0000)
info: HeatingDaemon.HeatingAdapter[0]   3x  HeatingModule ->Respond on Manager VERDICHTER 0.8
info: HeatingDaemon.HeatingAdapter[0]   3x  Boiler ->Respond on FES_COMFORT RUECKLAUFISTTEMP 22.0
info: HeatingDaemon.HeatingAdapter[0]   3x  Manager ->Write on HeatingModule SPEICHERBEDARF 0 (0x0000)
info: HeatingDaemon.HeatingAdapter[0]   2x  RemoteControl ->Respond on ExternalDevice PROGRAMMSCHALTER Tagbetrieb
info: HeatingDaemon.HeatingAdapter[0]   2x  FES_COMFORT ->Respond on ExternalDevice PROGRAMMSCHALTER Absenkbetrieb
info: HeatingDaemon.HeatingAdapter[0]   2x  Boiler ->Respond on FES_COMFORT AUSSENTEMP 4.2
info: HeatingDaemon.HeatingAdapter[0]   2x  Boiler ->Respond on FES_COMFORT SPEICHERISTTEMP 46.8
info: HeatingDaemon.HeatingAdapter[0]   1x  RemoteControl ->Respond on ExternalDevice SOFTWARE_NUMMER 195 (0x00C3)
info: HeatingDaemon.HeatingAdapter[0]   1x  RemoteControl ->Respond on ExternalDevice SOFTWARE_VERSION 6 (0x0006)
info: HeatingDaemon.HeatingAdapter[0]   1x  RemoteControl ->Write on Mixer FEUCHTE 40.8
info: HeatingDaemon.HeatingAdapter[0]   1x  RemoteControl ->Write on Mixer RAUMISTTEMP 20.0
info: HeatingDaemon.HeatingAdapter[0]   1x  Boiler ->Respond on RemoteControl GERAETE_ID 128-00
info: HeatingDaemon.HeatingAdapter[0]   1x  Boiler ->Write on RemoteControl_Broadcast MAX_HYSTERESE 0
```
</details>


Mit dem Parameter `can_scan` können die einzelnen Module (oder bestimmte Werte) der Heizungsanlage abgefragt werden, 
um zu ermitteln auf welche Elster-Index-Werte diese reagiert.

```
HeatingMqttService --can_scan=[SenderCanID] ReceiverCanID[.ElsterIndex[.NewElsterValue]]

   SenderCanID: optional, default is standard CanId from appsettings.json. Hex-Value or module name (e.g. 700 or ExternalDevice)
   ReceiverCanID: mandatory, hex-Value or module name (e.g. 301 or RemoteControl)
   ElsterIndex: optional to read or write a single value. Hex-Value or elster index name (e.g. 000b or GERAETE_ID)
   NewElsterValue: optional to write a single value. Hex-Value (e.g. 0f00)

Example: HeatingMqttService --can_scan=180               (scan all elster indices from 0000 to 1fff)
OR       HeatingMqttService --can_scan=700 180           (use 700 as sender can id to scan all elster indices
OR       HeatingMqttService --can_scan=700 180.0126      (read minutes at elster index 0126)
OR       HeatingMqttService --can_scan=700 180.0126.0f00 (set minutes to 15)
OR       HeatingMqttService --can_scan=700 Boiler.MINUTE (read minutes at elster index 0126)
```


## Konfiguration
In der `appsettings.json` Datei kann eine passive Abfrage beispielsweise konfiguriert werden, die ausgelöst wird, wenn der Boiler ein Telegramm an die FES_COMFORT sendet:
```
"HeatingMqttServiceConfig": {
    "CyclicReadingsQuery": [
      {
        {
        "ReadingName": "T_Aussentemperatur",
        "SenderCanID": "Boiler",
        "ReceiverCanID": "FES_COMFORT",
        "Function": "GetElsterValue",
        "ScheduleType": "Passive",
        "IntervalInSeconds": 0,
        "SendCondition": "OnValueChange",
        "ElsterIndex": "AUSSENTEMP"
        },
  ...
```

| Key |Beschreibung   |
|---|---|
|ReadingName |Text, der als Name des Readings verwendet wird, z.B. in FHEM oder im MQTT-Topic|
|ReceiverCanID |CAN-ID des Empfaengermoduls, an die das Telegram gesendet wird. Kann eine Hex-Zahl oder ein vordefinierter Modul-Bezeichner sein|
|Function|Nur GetElsterValue, um den Elster-Wert abzufragen und zu erhalten|
|ScheduleType|Angabe, wann das Telegram ausgewertet werden soll. Siehe Tabelle ScheduleType|
|IntervalInSeconds|Anzahl der Sekunden zwischen den Abfragen eines Elster-Wertes. Erforderlich nur, wenn ScheduleType auf Periodic gesetzt ist|
|SendCondition|Gibt an, wann ein Wert weiter an den MQTT-Broker gesendet werden soll. Es gibt aktuell 2 Werte: "OnValueChange" und "Always"|
|ElsterIndex|Kann ein Hex-Wert oder ein vordefinierter und bekannter Elster-Wert-Bezeichner (siehe [KElsterTableInc](HeatingDaemon/HeatingAdapter/ElsterProtocol/KElsterTableInc.cs)) sein|

|Vordefinierte Module-Bezeichner| Hex-Wert|Beschreibung
|---|---|----|
|Direct|0x000|Direkte Steuerung, wenn z.B ein FEK installiert ist. Kaum abfragen möglich, aber z.b. die Fehlerliste|
|FES_COMFORT|0x100|FES-Comfort-Modul, auch TCR-Comfort genannt. Hier ist das Display vom WPM |
|Boiler|0x180|Boiler-Modul|
|AtezModule|0x280|Vermutlich Solar-Heizmodul für Warmwasser|
|RemoteControl|0x301|Heizungs-Fernversteller oder Fernbedienung (FEK bei Stiebel Eltron, FET bei Tecalor) - Heizkreis 1|
|RemoteControl|0x302|Heizungs-Fernversteller oder Fernbedienung (FEK bei Stiebel Eltron, FET bei Tecalor) - Heizkreis 2|
|RemoteControl_Broadcast|0x379|Telegram mit Informationen für alle Fernbedienungen bzw. für alle Heizkreise|
|RoomThermostat|0x400|Raumtermostat. Ich vermute den Heizungs-Fernversteller FE7|
|Manager|0x480|Der Manager|
|HeatingModule|0x500|Heizungskontroll-Modul|
|BusCoupler|0x580|Vermutlich das Internet-Service-Gateway (ISG)|
|Mixer|0x601|Mischer für HK1|
|Mixer|0x601|Mischer für HK2|
|Mixer|0x601|Mischer für HK3|
|Mixer_Broadcast|0x679|Telegram mit Informationen für alle Mischer bzw. für alle Heizkreise|
|ComfortSoft|0x680|Anschluss über ein PC mit der Software ComfortSoft PC (ComfortSoft). Warnung: Nicht mit WPM3 nutzen|
|ExternalDevice|0x700|Externes Modul. Wird für diese als Standard für diese Software verwendet|
|Dcf|0x780|DCF-Modul, falls vorhanden um Winter und Sommerzeit Funktion zu realisieren|

|ScheduleType|Bescheibung|
|---|---|
|AtStartup|Die Leseabfrage wird beim Start der Anwendung einamlig ausgeführt|
|Periodic|Die Leseabfrage wird periodisch ausgeführt. Ein Intervall in Sekunden ist erforderlich|
|Passive|Die Leseabfrage wird nicht ausgeführt, sondern nur Telegramme, die durch anderer Busteilnehmen erzeugt wurden, werden verarbeitet|

|SendCondition|Beschreibung|
|---|---|
|OnEveryRead|Bei jedem Lesen, wird ein Wert an die MQTT-Bridge geschickt|
|OnValueChange| Nur bei Wertänderung, wird ein Wert an die MQTT-Bridge geschickt|

## Untersuchungen 

[Protokoll-Analyse und Wärmepumpen-Funktionsuntersuchung](doc/audits.md)
