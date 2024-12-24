# Tecalor/Stiebel Eltron Heatpump Interface
=============================================

## Beschreibung
---------------

Dieser Code implementiert eine Schnittstelle zu einer Tecalor/Stiebel Eltron Wärmepumpe über den CAN-Bus. Folgende Schnittstellen werden unterstützt:

* USBtin (Version HW10, SW00 - siehe Fischl.de) mit dem Protokoll von LAWICEL CANUSB
* Tecalor TTL 10 AC (Stibel Eltron WPL 10 AC) mit FEK und WPM3

Die Kommunikation mit der Wärmepumpe erfolgt über den CAN-Bus, sowohl lesend als auch schreibend. Die Ergebnisse werden im Speicher des AC10HeatingMqttService gehalten und sofort an einen MQTT-Message-Broker weitergeleitet. Das Wording lehnt sich dabei stark an die FHEM-Wärmepumpen-Implementierung an (siehe auch unten). Durch die MQTT-Anbindung sind auch Integrationen in andere Hausautomatisierungssysteme möglich. Der AC10HeatingMqttService ist ein .NET 8 Linux systemd Service und kann später neben FHEM betrieben werden.

### Testaufbau und Entwicklung
Um im Echtbetrieb zu entwickeln, ohne mein FHEM-System zu beschädigen, habe ich die CAN-Bus-Daten an meinen PC weitergeleitet.

<img src="doc/testsetup.png" width="800">

Dieses Projekt wurde in Zusammenarbeit mit einer Künstlichen Intelligenz entwickelt, um die Vorteile des Extreme Programming in Kombination mit KI-Tools wie Codeium und Copilot zu erproben. Die KI-Tools werden genutzt, um Git, GitHub, VS Code sowie Übersetzungen zwischen Programmiersprachen und die Korrektur von Englisch nach Deutsch (und umgekehrt) zu unterstützen. Eine ausgezeichnete Möglichkeit, moderne Technologien zu integrieren.


## Quellen
----------

Dieses Programm basiert auf der Arbeiten von:

* Jürg <http://juerg5524.ch/>
* Immi (THZ-Modul)
* Radiator


## Telegrammaufbau
----------

<img src="doc/telegram.png" width="800">

## Warnung
----------

Bitte verwenden Sie die `set`-Funktion nicht, um auf die Pumpe zu schreiben, es sei denn, Sie wissen, was Sie tun. Ein falscher Gebrauch der `set`-Funktion kann die Pumpe beschädigen.

## Installation
-------------

Die Konfiguration befindet sich normalerweise in der `appsettings.json` Datei. Konfigurationen können auch über den Befehl `dotnet run --AC10HeatingAdapterConfig:PortName="/dev/ttyACM0"` gesetzt werden. Das Programm basiert auf Microsoft .NET Core 8, was eine Voraussetzung ist.

Die Datei [AC10HeatingMqttService/AC10.service](AC10HeatingMqttService/AC10.service) muss in das Verzeichnis `/etc/systemd/system/` abgelegt werden.

Mit den Befehlen:

	sudo systemctl daemon-reload
	sudo systemctl enable AC10.service
	sudo systemctl start AC10.service

kann der Service aktiviert und gestartet werden. Log-Daten können mit dem Befehl:

	sudo journalctl -u AC10

angeschaut werden.

## Ideensammlung
----------------
[x] Implementierung des Lesens von Nachrichten auf dem Bus, die passiv gesendet werden
[ ] Implementieren von Schreiben auf den Bus und Abfragen von bestimmten Elster-Werten
[ ] Implementieren eines Bus-Scans pro Module / aller Module 
[ ] Fehlermeldung an ComfortSoft sollten ausgewerten werden: RemoteControl ->Write ComfortSoft FEHLERMELDUNG = 20805
[ ] Implementieren der FEK-Funktionen: Setzen der Heizkurve, Raumeinfluss und Heizkuvenfußpunkt(vermutlich unmöglich)
[ ] Implementieren der WPM-Funktionen: Auslesen der Temperaturen, Umschaltung auf Sommerbetrieb
[ ] Implementieren der Konfigurationen für MQTT-Ausleitung und zyklisches Abfragen von bestimmten Werten
[ ] Implementieren der Warmwassersteuerung: Temperaturfestlegung für Extra Warmwasser (WE), Zeitpunktfestlegung (Wenn wärmster Zeitpunkt und angeschlossen an Heizungsvorgang)


## Untersuchungen 22.12.24 - Setzen der Heizkurve auf 0.2 von der FEK
Eine Anpassung an der FEK (RemoteControl) für HK1 zeigt keinerlei Kommunikation auf dem Bus. Dies legt die Vermutung nahe, dass die FEK die vollständige Steuerung des HK1 übernimmt. Konkret wurde die Heizkurve auf 0.2 festgelegt. Da entweder der WPM die Steuerung des HKs übernimmt und nach der Installation der FEK alle Einstellungen bezüglich der Heizkurve aus diesem verschwunden sind, verwaltet und steuert nun wahrscheinlich die FEK alle Parameter und sendet nur die Ergebnisse an die anderen Module. Es ist fraglich, ob es überhaupt möglich ist, die Werte der FEK bezüglich der Heizkurve auszulesen oder diese extern zu schreiben. Letzteres wäre nur möglich, wenn mehrere FEKs erlaubt sind. 

Schlussfolgerung: Zunächst muss das Abfragen und Beschreiben des Busses umgesetzt und ein Scan auf die FEK durchgeführt werden.

```
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL = 23.9
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG = 20805
      Received frame 480 [7] A0005F02000000
      Manager ->Write on HeatingModule SPEICHERBEDARF = 512
      Received frame 180 [7] A0790C00190000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP = 2.5
      Received frame 180 [7] A079FA01D700EF
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL = 23.9
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG = 20805
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG = 20805
      Received frame 100 [7] 31001600000000
      Unknown_100h ->Read on Boiler RUECKLAUFISTTEMP
      Received frame 180 [7] 22001600E20000
      Boiler ->Respond on Unknown_100h RUECKLAUFISTTEMP = 22.6
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG = 20805
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG = 20805
      Received frame 480 [7] A100FA01990000
      Manager ->Read on HeatingModule SOFTWARE_NUMMER
      Received frame 500 [7] 9200FA01990043
      HeatingModule ->Respond on Manager SOFTWARE_NUMMER = 67
      Received frame 100 [7] 31000E00000000
      Unknown_100h ->Read on Boiler SPEICHERISTTEMP
      Received frame 180 [7] 22000E01D40000
      Boiler ->Respond on Unknown_100h SPEICHERISTTEMP = 46.8
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG = 20805
      Received frame 180 [7] A0790C00190000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP = 2.5
      Received frame 180 [7] A079FA01D700EF
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL = 23.9
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG = 20805
```

