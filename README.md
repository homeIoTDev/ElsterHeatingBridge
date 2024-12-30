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

- [x] Implementierung des Lesens von Nachrichten auf dem Bus, die passiv gesendet werden
- [ ] Implementieren von Schreiben auf den Bus und Abfragen von bestimmten Elster-Werten
- [ ] ElsterValue aus einem ElsterCanFrame als Eigenschalft zur Verfügung stellen
- [ ] Zeitstempel beim Protokollieren
- [ ] Implementieren eines Bus-Scans pro Module / aller Module 
- [ ] Fehlermeldung an ComfortSoft sollten ausgewerten werden: RemoteControl ->Write ComfortSoft FEHLERMELDUNG = 20805
- [ ] Implementieren der FEK-Funktionen: Setzen der Heizkurve, Raumeinfluss und Heizkuvenfußpunkt(vermutlich unmöglich)
- [ ] Implementieren der WPM-Funktionen: Auslesen der Temperaturen, Umschaltung auf Sommerbetrieb
- [ ] Implementieren der Konfigurationen für MQTT-Ausleitung und zyklisches Abfragen von bestimmten Werten
- [ ] Implementieren der Warmwassersteuerung: Temperaturfestlegung für Extra Warmwasser (WE), Zeitpunktfestlegung (Wenn wärmster Zeitpunkt und angeschlossen an Heizungsvorgang)

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


## Untersuchungen 30.12.24 - Neustart der Steuerung (stromlos)
Eigentlich sollte die Heizung auf der FEK in den Ferienbetrieb wechseln, tut sie aber nicht. Test, ob sie nach Neustart tut. Dabei wurde der Neustart aufgezeichnet.
Ergebnis: Nö -> Das Koffersymbol fehlt in der Anzeige der FEK

´´´
      IsSystemd: False
      AC10MqttAdapter initialized with configuration.
      Starting MQTT broker monitoring loop.
      UsbTinCanBusAdapter initialized with configuration.
      Starting UsbTinCanBusAdapter for port /dev/pts/0 115200,8,None,One...
      Enqueuing MQTT message to topic 'AC10/CAN_Channel' with payload 'undefined'...
      Starte Keyboard Input Service...
      MqttClient: Connected
      Successfully connected to MQTT Broker Unspecified/broker.hivemq.com:1883.
      Successfully opened serial port /dev/pts/0.
      Sending MQTT message to topic 'AC10/CAN_Channel' with payload 'undefined'...
      Reading from serial port /dev/pts/0...
      Starting CANBusInit...
      Enqueuing MQTT message to topic 'AC10/CAN_Channel' with payload 'config mode'...
      Tx serial port: '\r' length: 1
      Sending MQTT message to topic 'AC10/CAN_Channel' with payload 'config mode'...
      Response to '\r': Timeout
      Tx serial port: '\r' length: 1
      Response to '\r': Timeout
      Tx serial port: 'C\r' length: 2
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      Response to 'C\r': Timeout
      Tx serial port: 'V\r' length: 2
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't180760792300000000' length: 19
      Received frame 180 [7] 60792300000000
      Boiler ->Write on RemoteControl_Broadcast MAX_HYSTERESE 0
      Rx serial port: 't4807A0005F00000000' length: 19
      Received frame 480 [7] A0005F00000000
      Manager ->Write on HeatingModule SPEICHERBEDARF 0 (0x0000)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F4' length: 19
      Received frame 180 [7] A079FA01D700F4
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.4
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't100731001600000000' length: 19
      Received frame 100 [7] 31001600000000
      Unknown_100h ->Read on Boiler RUECKLAUFISTTEMP
      Rx serial port: 't180722001600E70000' length: 19
      Received frame 180 [7] 22001600E70000
      Boiler ->Respond on Unknown_100h RUECKLAUFISTTEMP 23.1
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't50079600FE01000000' length: 19
      Received frame 500 [7] 9600FE01000000
      HeatingModule ->System on Manager INITIALISIERUNG 1
      Rx serial port: 't4807A100FA01D60000' length: 19
      Received frame 480 [7] A100FA01D60000
      Manager ->Read on HeatingModule WPVORLAUFIST
      Rx serial port: 't50079200FA01D600FF' length: 19
      Received frame 500 [7] 9200FA01D600FF
      HeatingModule ->Respond on Manager WPVORLAUFIST 25.5
      Rx serial port: 't4807A1001600000000' length: 19
      Received frame 480 [7] A1001600000000
      Manager ->Read on HeatingModule RUECKLAUFISTTEMP
      Response to 'V\r': Timeout
      Tx serial port: 'S1\r' length: 3
      Rx serial port: 't4807A100FA07A80000' length: 19
      Received frame 480 [7] A100FA07A80000
      Manager ->Read on HeatingModule VERDICHTER
      Rx serial port: 't4807A000FA02CA00E7' length: 19
      Received frame 480 [7] A000FA02CA00E7
      Manager ->Write on HeatingModule MATERIALNUMMER_HIGH 231 (0x00E7)
      Rx serial port: 't4807A000FA09170000' length: 19
      Received frame 480 [7] A000FA09170000
      Manager ->Write on HeatingModule INTEGRAL_REGELABWEICHUNG_RELATIV 0 (0x0000)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't4807A700FE01000000' length: 19
      Received frame 480 [7] A700FE01000000
      Manager ->RespondSystem on HeatingModule INITIALISIERUNG 1
      Rx serial port: 't500792001600BE0000' length: 19
      Received frame 500 [7] 92001600BE0000
      HeatingModule ->Respond on Manager RUECKLAUFISTTEMP 19.0
      Rx serial port: 't50079200FA07A80008' length: 19
      Received frame 500 [7] 9200FA07A80008
      HeatingModule ->Respond on Manager VERDICHTER 0.8
      Response to 'S1\r': Error
      Tx serial port: 'O\r' length: 2
      Rx serial port: '\a' length: 1
      Rx serial port: '\a' length: 1
      Rx serial port: '' length: 0
      Response to 'O\r': OK
      Enqueuing MQTT message to topic 'AC10/CAN_Channel' with payload 'opened'...
      Tx serial port: 'F\r' length: 2
      Sending MQTT message to topic 'AC10/CAN_Channel' with payload 'opened'...
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Enqueuing MQTT message to topic 'AC10/HW_Version' with payload '01'...
      Response to 'F\r': OK
      Enqueuing MQTT message to topic 'AC10/SW_Version' with payload '00'...
      Rx serial port: 'V0100' length: 5
      Rx serial port: '' length: 0
      Rx serial port: '' length: 0
      Enqueuing MQTT message to topic 'AC10/CAN_Error_Text' with payload 'Bit 3 - Data overrun (DOI)'...
      Enqueuing MQTT message to topic 'AC10/CAN_Error' with payload '08'...
      CAN Error: Bit 3 - Data overrun (DOI) (8)
      Rx serial port: 'F08' length: 3
      Sending MQTT message to topic 'AC10/HW_Version' with payload '01'...
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Sending MQTT message to topic 'AC10/SW_Version' with payload '00'...
      Rx serial port: 't4807A0005F00000000' length: 19
      Received frame 480 [7] A0005F00000000
      Manager ->Write on HeatingModule SPEICHERBEDARF 0 (0x0000)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F3' length: 19
      Received frame 180 [7] A079FA01D700F3
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.3
      Sending MQTT message to topic 'AC10/CAN_Error_Text' with payload 'Bit 3 - Data overrun (DOI)'...
      Sending MQTT message to topic 'AC10/CAN_Error' with payload '08'...
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't4807A100FA01D60000' length: 19
      Received frame 480 [7] A100FA01D60000
      Manager ->Read on HeatingModule WPVORLAUFIST
      Rx serial port: 't4807A1001600000000' length: 19
      Received frame 480 [7] A1001600000000
      Manager ->Read on HeatingModule RUECKLAUFISTTEMP
      Rx serial port: 't50079200FA01D600FE' length: 19
      Received frame 500 [7] 9200FA01D600FE
      HeatingModule ->Respond on Manager WPVORLAUFIST 25.4
      Rx serial port: 't4807A100FA07A80000' length: 19
      Received frame 480 [7] A100FA07A80000
      Manager ->Read on HeatingModule VERDICHTER
      Rx serial port: 't4807A000FA02CA00EC' length: 19
      Received frame 480 [7] A000FA02CA00EC
      Manager ->Write on HeatingModule MATERIALNUMMER_HIGH 236 (0x00EC)
      Rx serial port: 't500792001600CD0000' length: 19
      Received frame 500 [7] 92001600CD0000
      HeatingModule ->Respond on Manager RUECKLAUFISTTEMP 20.5
      Rx serial port: 't4807A000FA09170000' length: 19
      Received frame 480 [7] A000FA09170000
      Manager ->Write on HeatingModule INTEGRAL_REGELABWEICHUNG_RELATIV 0 (0x0000)
      Rx serial port: 't180760790E01AE0000' length: 19
      Received frame 180 [7] 60790E01AE0000
      Boiler ->Write on RemoteControl_Broadcast SPEICHERISTTEMP 43.0
      Rx serial port: 't50079200FA07A80008' length: 19
      Received frame 500 [7] 9200FA07A80008
      HeatingModule ->Respond on Manager VERDICHTER 0.8
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't100731000C00000000' length: 19
      Received frame 100 [7] 31000C00000000
      Unknown_100h ->Read on Boiler AUSSENTEMP
      Rx serial port: 't180722000CFFFD0000' length: 19
      Received frame 180 [7] 22000CFFFD0000
      Boiler ->Respond on Unknown_100h AUSSENTEMP -0.3
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F3' length: 19
      Received frame 180 [7] A079FA01D700F3
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.3
      Rx serial port: 't100731001600000000' length: 19
      Received frame 100 [7] 31001600000000
      Unknown_100h ->Read on Boiler RUECKLAUFISTTEMP
      Rx serial port: 't180722001600EC0000' length: 19
      Received frame 180 [7] 22001600EC0000
      Boiler ->Respond on Unknown_100h RUECKLAUFISTTEMP 23.6
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't100731000E00000000' length: 19
      Received frame 100 [7] 31000E00000000
      Unknown_100h ->Read on Boiler SPEICHERISTTEMP
      Rx serial port: 't180722000E01AF0000' length: 19
      Received frame 180 [7] 22000E01AF0000
      Boiler ->Respond on Unknown_100h SPEICHERISTTEMP 43.1
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't4807A100FA01990000' length: 19
      Received frame 480 [7] A100FA01990000
      Manager ->Read on HeatingModule SOFTWARE_NUMMER
      Rx serial port: 't4807A0005F00000000' length: 19
      Received frame 480 [7] A0005F00000000
      Manager ->Write on HeatingModule SPEICHERBEDARF 0 (0x0000)
      Rx serial port: 't50079200FA01990043' length: 19
      Received frame 500 [7] 9200FA01990043
      HeatingModule ->Respond on Manager SOFTWARE_NUMMER 67 (0x0043)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F3' length: 19
      Received frame 180 [7] A079FA01D700F3
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.3
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't4807A100FA01D60000' length: 19
      Received frame 480 [7] A100FA01D60000
      Manager ->Read on HeatingModule WPVORLAUFIST
      Rx serial port: 't4807A1001600000000' length: 19
      Received frame 480 [7] A1001600000000
      Manager ->Read on HeatingModule RUECKLAUFISTTEMP
      Rx serial port: 't50079200FA01D600FD' length: 19
      Received frame 500 [7] 9200FA01D600FD
      HeatingModule ->Respond on Manager WPVORLAUFIST 25.3
      Rx serial port: 't4807A100FA07A80000' length: 19
      Received frame 480 [7] A100FA07A80000
      Manager ->Read on HeatingModule VERDICHTER
      Rx serial port: 't4807A000FA02CA00EB' length: 19
      Received frame 480 [7] A000FA02CA00EB
      Manager ->Write on HeatingModule MATERIALNUMMER_HIGH 235 (0x00EB)
      Rx serial port: 't4807A000FA09170000' length: 19
      Received frame 480 [7] A000FA09170000
      Manager ->Write on HeatingModule INTEGRAL_REGELABWEICHUNG_RELATIV 0 (0x0000)
      Rx serial port: 't500792001600CD0000' length: 19
      Received frame 500 [7] 92001600CD0000
      HeatingModule ->Respond on Manager RUECKLAUFISTTEMP 20.5
      Rx serial port: 't50079200FA07A80008' length: 19
      Received frame 500 [7] 9200FA07A80008
      HeatingModule ->Respond on Manager VERDICHTER 0.8
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't100731000C00000000' length: 19
      Received frame 100 [7] 31000C00000000
      Unknown_100h ->Read on Boiler AUSSENTEMP
      Rx serial port: 't180722000CFFFD0000' length: 19
      Received frame 180 [7] 22000CFFFD0000
      Boiler ->Respond on Unknown_100h AUSSENTEMP -0.3
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F3' length: 19
      Received frame 180 [7] A079FA01D700F3
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.3
      Rx serial port: 't100731001600000000' length: 19
      Received frame 100 [7] 31001600000000
      Unknown_100h ->Read on Boiler RUECKLAUFISTTEMP
      Rx serial port: 't180722001600EC0000' length: 19
      Received frame 180 [7] 22001600EC0000
      Boiler ->Respond on Unknown_100h RUECKLAUFISTTEMP 23.6
      Rx serial port: 't100731000E00000000' length: 19
      Received frame 100 [7] 31000E00000000
      Unknown_100h ->Read on Boiler SPEICHERISTTEMP
      Rx serial port: 't180722000E01B00000' length: 19
      Received frame 180 [7] 22000E01B00000
      Boiler ->Respond on Unknown_100h SPEICHERISTTEMP 43.2
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017C0017501910000' length: 19
      Received frame 301 [7] C0017501910000
      RemoteControl ->Write on Mixer FEUCHTE 40.1
      Rx serial port: 't3017C0011100CA0000' length: 19
      Received frame 301 [7] C0011100CA0000
      RemoteControl ->Write on Mixer RAUMISTTEMP 20.2
      Rx serial port: 't301731000BC3060000' length: 19
      Received frame 301 [7] 31000BC3060000
      RemoteControl ->Read on Boiler GERAETE_ID
      Rx serial port: 't180762010B80000000' length: 19
      Received frame 180 [7] 62010B80000000
      Boiler ->Respond on RemoteControl GERAETE_ID -128-00
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't4807A100FA01990000' length: 19
      Received frame 480 [7] A100FA01990000
      Manager ->Read on HeatingModule SOFTWARE_NUMMER
      Rx serial port: 't180760792300000000' length: 19
      Received frame 180 [7] 60792300000000
      Boiler ->Write on RemoteControl_Broadcast MAX_HYSTERESE 0
      Rx serial port: 't4807A0005F00000000' length: 19
      Received frame 480 [7] A0005F00000000
      Manager ->Write on HeatingModule SPEICHERBEDARF 0 (0x0000)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F3' length: 19
      Received frame 180 [7] A079FA01D700F3
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.3
      Rx serial port: 't50079200FA01990043' length: 19
      Received frame 500 [7] 9200FA01990043
      HeatingModule ->Respond on Manager SOFTWARE_NUMMER 67 (0x0043)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't4807A100FA01D60000' length: 19
      Received frame 480 [7] A100FA01D60000
      Manager ->Read on HeatingModule WPVORLAUFIST
      Rx serial port: 't4807A1001600000000' length: 19
      Received frame 480 [7] A1001600000000
      Manager ->Read on HeatingModule RUECKLAUFISTTEMP
      Rx serial port: 't4807A100FA07A80000' length: 19
      Received frame 480 [7] A100FA07A80000
      Manager ->Read on HeatingModule VERDICHTER
      Rx serial port: 't4807A000FA02CA00EB' length: 19
      Received frame 480 [7] A000FA02CA00EB
      Manager ->Write on HeatingModule MATERIALNUMMER_HIGH 235 (0x00EB)
      Rx serial port: 't4807A000FA09170000' length: 19
      Received frame 480 [7] A000FA09170000
      Manager ->Write on HeatingModule INTEGRAL_REGELABWEICHUNG_RELATIV 0 (0x0000)
      Rx serial port: 't50079200FA01D600FD' length: 19
      Received frame 500 [7] 9200FA01D600FD
      HeatingModule ->Respond on Manager WPVORLAUFIST 25.3
      Rx serial port: 't500792001600CD0000' length: 19
      Received frame 500 [7] 92001600CD0000
      HeatingModule ->Respond on Manager RUECKLAUFISTTEMP 20.5
      Rx serial port: 't50079200FA07A80008' length: 19
      Received frame 500 [7] 9200FA07A80008
      HeatingModule ->Respond on Manager VERDICHTER 0.8
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't100731000C00000000' length: 19
      Received frame 100 [7] 31000C00000000
      Unknown_100h ->Read on Boiler AUSSENTEMP
      Rx serial port: 't180722000CFFFD0000' length: 19
      Received frame 180 [7] 22000CFFFD0000
      Boiler ->Respond on Unknown_100h AUSSENTEMP -0.3
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F3' length: 19
      Received frame 180 [7] A079FA01D700F3
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.3
      Rx serial port: 't100731001600000000' length: 19
      Received frame 100 [7] 31001600000000
      Unknown_100h ->Read on Boiler RUECKLAUFISTTEMP
      Rx serial port: 't180722001600EB0000' length: 19
      Received frame 180 [7] 22001600EB0000
      Boiler ->Respond on Unknown_100h RUECKLAUFISTTEMP 23.5
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't100731000E00000000' length: 19
      Received frame 100 [7] 31000E00000000
      Unknown_100h ->Read on Boiler SPEICHERISTTEMP
      Rx serial port: 't180722000E01AF0000' length: 19
      Received frame 180 [7] 22000E01AF0000
      Boiler ->Respond on Unknown_100h SPEICHERISTTEMP 43.1
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't4807A100FA01990000' length: 19
      Received frame 480 [7] A100FA01990000
      Manager ->Read on HeatingModule SOFTWARE_NUMMER
      Rx serial port: 't4807A0005F00000000' length: 19
      Received frame 480 [7] A0005F00000000
      Manager ->Write on HeatingModule SPEICHERBEDARF 0 (0x0000)
      Rx serial port: 't50079200FA01990043' length: 19
      Received frame 500 [7] 9200FA01990043
      HeatingModule ->Respond on Manager SOFTWARE_NUMMER 67 (0x0043)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F3' length: 19
      Received frame 180 [7] A079FA01D700F3
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.3
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't4807A100FA01D60000' length: 19
      Received frame 480 [7] A100FA01D60000
      Manager ->Read on HeatingModule WPVORLAUFIST
      Rx serial port: 't4807A1001600000000' length: 19
      Received frame 480 [7] A1001600000000
      Manager ->Read on HeatingModule RUECKLAUFISTTEMP
      Rx serial port: 't4807A100FA07A80000' length: 19
      Received frame 480 [7] A100FA07A80000
      Manager ->Read on HeatingModule VERDICHTER
      Rx serial port: 't50079200FA01D600FD' length: 19
      Received frame 500 [7] 9200FA01D600FD
      HeatingModule ->Respond on Manager WPVORLAUFIST 25.3
      Rx serial port: 't4807A000FA02CA00EC' length: 19
      Received frame 480 [7] A000FA02CA00EC
      Manager ->Write on HeatingModule MATERIALNUMMER_HIGH 236 (0x00EC)
      Rx serial port: 't4807A000FA09170000' length: 19
      Received frame 480 [7] A000FA09170000
      Manager ->Write on HeatingModule INTEGRAL_REGELABWEICHUNG_RELATIV 0 (0x0000)
      Rx serial port: 't500792001600CD0000' length: 19
      Received frame 500 [7] 92001600CD0000
      HeatingModule ->Respond on Manager RUECKLAUFISTTEMP 20.5
      Rx serial port: 't50079200FA07A80008' length: 19
      Received frame 500 [7] 9200FA07A80008
      HeatingModule ->Respond on Manager VERDICHTER 0.8
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't100731000C00000000' length: 19
      Received frame 100 [7] 31000C00000000
      Unknown_100h ->Read on Boiler AUSSENTEMP
      Rx serial port: 't180722000CFFFD0000' length: 19
      Received frame 180 [7] 22000CFFFD0000
      Boiler ->Respond on Unknown_100h AUSSENTEMP -0.3
      Rx serial port: 't3017D0000151450000' length: 19
      Received frame 301 [7] D0000151450000
      RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F3' length: 19
      Received frame 180 [7] A079FA01D700F3
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.3
      Rx serial port: 't100731001600000000' length: 19
      Received frame 100 [7] 31001600000000
      Unknown_100h ->Read on Boiler RUECKLAUFISTTEMP
      Rx serial port: 't180722001600EC0000' length: 19
      Received frame 180 [7] 22001600EC0000
      Boiler ->Respond on Unknown_100h RUECKLAUFISTTEMP 23.6
      Rx serial port: 't4807A0797400000000' length: 19
      Received frame 480 [7] A0797400000000
      Manager ->Write on HeatingModule_Broadcast EVU_SPERRE_AKTIV 0 (0x0000)
      Rx serial port: 't480720007400000000' length: 19
      Received frame 480 [7] 20007400000000
      Manager ->Write on Unknown_100h EVU_SPERRE_AKTIV 0 (0x0000)
      Rx serial port: 't10072600FD09000000' length: 19
      Received frame 100 [7] 2600FD09000000
      Unknown_100h ->System on Unknown_100h BUSKONFIGURATION 2304 (0x0900)
      Rx serial port: 't48079600FD09000000' length: 19
      Received frame 480 [7] 9600FD09000000
      Manager ->System on Manager BUSKONFIGURATION 2304 (0x0900)
      Rx serial port: 't18073600FD09000000' length: 19
      Received frame 180 [7] 3600FD09000000
      Boiler ->System on Boiler BUSKONFIGURATION 2304 (0x0900)
      Rx serial port: 't6017C601FD09000000' length: 19
      Received frame 601 [7] C601FD09000000
      Mixer ->System on Mixer BUSKONFIGURATION 2304 (0x0900)
      Rx serial port: 't30176601FD08000000' length: 19
      Received frame 301 [7] 6601FD08000000
      RemoteControl ->System on RemoteControl BUSKONFIGURATION 2048 (0x0800)
      Rx serial port: 't6807D600FD08000000' length: 19
      Received frame 680 [7] D600FD08000000
      ComfortSoft ->System on ComfortSoft BUSKONFIGURATION 2048 (0x0800)
      Rx serial port: 't6027C602FD09000000' length: 19
      Received frame 602 [7] C602FD09000000
      Mixer2 ->System on Mixer2 BUSKONFIGURATION 2304 (0x0900)
      Rx serial port: 't30276602FD08000000' length: 19
      Received frame 302 [7] 6602FD08000000
      RemoteControl2 ->System on RemoteControl2 BUSKONFIGURATION 2048 (0x0800)
      Rx serial port: 't5147A614FD08000000' length: 19
      Received frame 514 [7] A614FD08000000
      514 ->System on 514 BUSKONFIGURATION 2048 (0x0800)
      Rx serial port: 't51E7A61EFD08000000' length: 19
      Received frame 51E [7] A61EFD08000000
      51E ->System on 51E BUSKONFIGURATION 2048 (0x0800)
      Rx serial port: 't5287A628FD08000000' length: 19
      Received frame 528 [7] A628FD08000000
      528 ->System on 528 BUSKONFIGURATION 2048 (0x0800)
      Rx serial port: 't5327A632FD08000000' length: 19
      Received frame 532 [7] A632FD08000000
      532 ->System on 532 BUSKONFIGURATION 2048 (0x0800)
      Rx serial port: 't53C7A63CFD08000000' length: 19
      Received frame 53C [7] A63CFD08000000
      53C ->System on 53C BUSKONFIGURATION 2048 (0x0800)
      Rx serial port: 't5467A646FD08000000' length: 19
      Received frame 546 [7] A646FD08000000
      546 ->System on 546 BUSKONFIGURATION 2048 (0x0800)
      Rx serial port: 't30176601FD01000000' length: 19
      Received frame 301 [7] 6601FD01000000
      RemoteControl ->System on RemoteControl BUSKONFIGURATION 256 (0x0100)
      Rx serial port: 't5007A600FD09000000' length: 19
      Received frame 500 [7] A600FD09000000
      HeatingModule ->System on HeatingModule BUSKONFIGURATION 2304 (0x0900)
      Rx serial port: 't10079600FE01000000' length: 19
      Received frame 100 [7] 9600FE01000000
      Unknown_100h ->System on Manager INITIALISIERUNG 1
      Rx serial port: 't48072700FE00000000' length: 19
      Received frame 480 [7] 2700FE00000000
      Manager ->RespondSystem on Unknown_100h INITIALISIERUNG 0
      Rx serial port: 't60176601FE01000000' length: 19
      Received frame 601 [7] 6601FE01000000
      Mixer ->System on RemoteControl INITIALISIERUNG 1
      Rx serial port: 't18076679FE01000000' length: 19
      Received frame 180 [7] 6679FE01000000
      Boiler ->System on RemoteControl_Broadcast INITIALISIERUNG 1
      Rx serial port: 't4807A679FE01000000' length: 19
      Received frame 480 [7] A679FE01000000
      Manager ->System on HeatingModule_Broadcast INITIALISIERUNG 1
      Rx serial port: 't1807C603FE01000000' length: 19
      Received frame 180 [7] C603FE01000000
      Boiler ->System on Mixer3 INITIALISIERUNG 1
      Rx serial port: 't48072600FE01000000' length: 19
      Received frame 480 [7] 2600FE01000000
      Manager ->System on Unknown_100h INITIALISIERUNG 1
      Rx serial port: 't10079700FE01000000' length: 19
      Received frame 100 [7] 9700FE01000000
      Unknown_100h ->RespondSystem on Manager INITIALISIERUNG 1
      Rx serial port: 't10079000FA4E5D0001' length: 19
      Received frame 100 [7] 9000FA4E5D0001
      Elster CAN frame from Unknown_100h ->Write on Manager with unknown elster index 4E5D, with possible data: '1 - (et_default: 1, et_dec_val: 0.1, et_cent_val: 0.01, et_mil_val: 0.001, et_byte: 1, et_bool: True, et_little_endian: 256, et_betriebsart: ?:et_betriebsart1, et_zeit: (0, 1), et_datum: (0, 1), et_time_domain: (00:00:00, 00:15:00), et_dev_nr: 2, et_err_nr: ERR 1, et_dev_id: 0-1)' [7] 9000FA4E5D0001
      Rx serial port: 't10079000FA4E8B0002' length: 19
      Received frame 100 [7] 9000FA4E8B0002
      Elster CAN frame from Unknown_100h ->Write on Manager with unknown elster index 4E8B, with possible data: '2 - (et_default: 2, et_dec_val: 0.2, et_cent_val: 0.02, et_mil_val: 0.002, et_byte: 2, et_little_endian: 512, et_betriebsart: ?:et_betriebsart2, et_zeit: (0, 2), et_datum: (0, 2), et_time_domain: (00:00:00, 00:30:00), et_dev_nr: 3, et_err_nr: Schuetz klebt, et_dev_id: 0-2)' [7] 9000FA4E8B0002
      Rx serial port: 't30173600FE01000000' length: 19
      Received frame 301 [7] 3600FE01000000
      RemoteControl ->System on Boiler INITIALISIERUNG 1
      Rx serial port: 't18076701FE01000000' length: 19
      Received frame 180 [7] 6701FE01000000
      Boiler ->RespondSystem on RemoteControl INITIALISIERUNG 1
      Rx serial port: 't3017C601FE01000000' length: 19
      Received frame 301 [7] C601FE01000000
      RemoteControl ->System on Mixer INITIALISIERUNG 1
      Rx serial port: 't301731000BC3060000' length: 19
      Received frame 301 [7] 31000BC3060000
      RemoteControl ->Read on Boiler GERAETE_ID
      Rx serial port: 't50079600FE01000000' length: 19
      Received frame 500 [7] 9600FE01000000
      HeatingModule ->System on Manager INITIALISIERUNG 1
      Rx serial port: 't3017300002015E0000' length: 19
      Received frame 301 [7] 300002015E0000
      RemoteControl ->Write on Boiler KESSELSOLLTEMP 35.0
      Rx serial port: 't301730005802000000' length: 19
      Received frame 301 [7] 30005802000000
      RemoteControl ->Write on Boiler MISCHER_ZU 2
      Rx serial port: 't301730006E02000000' length: 19
      Received frame 301 [7] 30006E02000000
      RemoteControl ->Write on Boiler HEIZKREIS_STATUS_PROGSTELL 512 (0x0200)
      Rx serial port: 't60176701FE01000000' length: 19
      Received frame 601 [7] 6701FE01000000
      Mixer ->RespondSystem on RemoteControl INITIALISIERUNG 1
      Rx serial port: 't180762010B80000000' length: 19
      Received frame 180 [7] 62010B80000000
      Boiler ->Respond on RemoteControl GERAETE_ID -128-00
      Rx serial port: 't4807A700FE01000000' length: 19
      Received frame 480 [7] A700FE01000000
      Manager ->RespondSystem on HeatingModule INITIALISIERUNG 1
      Rx serial port: 't48072000FA025B8000' length: 19
      Received frame 480 [7] 2000FA025B8000
      Manager ->Write on Unknown_100h GEBLAESEKUEHLUNG -32768 (0x8000)
      Rx serial port: 't4807A0793001000000' length: 19
      Received frame 480 [7] A0793001000000
      Manager ->Write on HeatingModule_Broadcast ACCESS_EEPROM 1
      Rx serial port: 't4807A0791000000000' length: 19
      Received frame 480 [7] A0791000000000
      Manager ->Write on HeatingModule_Broadcast GERAETEKONFIGURATION 0 (0x0000)
      Rx serial port: 't4807A0795D00000000' length: 19
      Received frame 480 [7] A0795D00000000
      Manager ->Write on HeatingModule_Broadcast ANFAHRENT 0 (0x0000)
      Rx serial port: 't4807A0791600EA0000' length: 19
      Received frame 480 [7] A0791600EA0000
      Manager ->Write on HeatingModule_Broadcast RUECKLAUFISTTEMP 23.4
      Rx serial port: 't4807A0795E00000000' length: 19
      Received frame 480 [7] A0795E00000000
      Manager ->Write on HeatingModule_Broadcast TEILVORRANG_WW 0 (0x0000)
      Rx serial port: 't4807A079FA0A200000' length: 19
      Received frame 480 [7] A079FA0A200000
      Manager ->Write on HeatingModule_Broadcast SAMMEL_RELAISSTATUS 0 (0x0000)
      Rx serial port: 't4807A079FA01120200' length: 19
      Received frame 480 [7] A079FA01120200
      Manager ->Write on HeatingModule_Broadcast PROGRAMMSCHALTER Automatik
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't4807A079FA05D80000' length: 19
      Received frame 480 [7] A079FA05D80000
      Manager ->Write on HeatingModule_Broadcast ZWISCHENEINSPRITZUNG_ISTTEMP 0 (0x0000)
      Rx serial port: 't4807A079FA05DC0000' length: 19
      Received frame 480 [7] A079FA05DC0000
      Manager ->Write on HeatingModule_Broadcast LUEFT_PASSIVKUEHLUNG_UEBER_FORTLUEFTER 0 (0x0000)
      Rx serial port: 't4807A079FA01262900' length: 19
      Received frame 480 [7] A079FA01262900
      Manager ->Write on HeatingModule_Broadcast MINUTE 41
      Rx serial port: 't4807A079FA01250B00' length: 19
      Received frame 480 [7] A079FA01250B00
      Manager ->Write on HeatingModule_Broadcast STUNDE 11
      Rx serial port: 't4807A079FA01221E00' length: 19
      Received frame 480 [7] A079FA01221E00
      Manager ->Write on HeatingModule_Broadcast TAG 30
      Rx serial port: 't4807A079FA01230C00' length: 19
      Received frame 480 [7] A079FA01230C00
      Manager ->Write on HeatingModule_Broadcast MONAT 12
      Rx serial port: 't4807A079FA01241800' length: 19
      Received frame 480 [7] A079FA01241800
      Manager ->Write on HeatingModule_Broadcast JAHR 24
      Rx serial port: 't4807A079FA4E300000' length: 19
      Received frame 480 [7] A079FA4E300000
      Elster CAN frame from Manager ->Write on HeatingModule_Broadcast with unknown elster index 4E30, with possible data: '0 - (et_default: 0, et_dec_val: 0, et_cent_val: 0, et_mil_val: 0, et_byte: 0, et_bool: False, et_little_bool: False, et_little_endian: 0, et_betriebsart: Notbetrieb, et_zeit: (0, 0), et_datum: (0, 0), et_time_domain: (00:00:00, 00:00:00), et_dev_nr: 1, et_err_nr: ERR 0, et_dev_id: 0-0)' [7] A079FA4E300000
      Rx serial port: 't4807A0005F00000000' length: 19
      Received frame 480 [7] A0005F00000000
      Manager ->Write on HeatingModule SPEICHERBEDARF 0 (0x0000)
      Rx serial port: 't4807A000FA03E70000' length: 19
      Received frame 480 [7] A000FA03E70000
      Manager ->Write on HeatingModule QUELLENPUMPEN_STATUS 0 (0x0000)
      Rx serial port: 't4807A000FA05DF0000' length: 19
      Received frame 480 [7] A000FA05DF0000
      Manager ->Write on HeatingModule TEMPORALE_LUEFTUNGSSTUFE_DAUER 0 (0x0000)
      Rx serial port: 't4807A100FA07CB0000' length: 19
      Received frame 480 [7] A100FA07CB0000
      Manager ->Read on HeatingModule PARAMETERSATZ
      Rx serial port: 't4807A100FA03590000' length: 19
      Received frame 480 [7] A100FA03590000
      Manager ->Read on HeatingModule HARDWARE_NUMMER
      Rx serial port: 't50079200FA07CB0011' length: 19
      Received frame 500 [7] 9200FA07CB0011
      HeatingModule ->Respond on Manager PARAMETERSATZ 17 (0x0011)
      Rx serial port: 't4807A000FAFDAE0000' length: 19
      Received frame 480 [7] A000FAFDAE0000
      Elster CAN frame from Manager ->Write on HeatingModule without elster index. [7] A000FAFDAE0000
      Rx serial port: 't4807A100FA01D60000' length: 19
      Received frame 480 [7] A100FA01D60000
      Manager ->Read on HeatingModule WPVORLAUFIST
      Rx serial port: 't4807A1001600000000' length: 19
      Received frame 480 [7] A1001600000000
      Manager ->Read on HeatingModule RUECKLAUFISTTEMP
      Rx serial port: 't1807A000FAFDAA0000' length: 19
      Received frame 180 [7] A000FAFDAA0000
      Elster CAN frame from Boiler ->Write on HeatingModule without elster index. [7] A000FAFDAA0000
      Rx serial port: 't4807A0000B80000000' length: 19
      Received frame 480 [7] A0000B80000000
      Manager ->Write on HeatingModule GERAETE_ID -128-00
      Rx serial port: 't50079200FA03598000' length: 19
      Received frame 500 [7] 9200FA03598000
      HeatingModule ->Respond on Manager HARDWARE_NUMMER -32768 (0x8000)
      Rx serial port: 't4807A000FA0199014B' length: 19
      Received frame 480 [7] A000FA0199014B
      Manager ->Write on HeatingModule SOFTWARE_NUMMER 331 (0x014B)
      Rx serial port: 't4807A000FA019A0002' length: 19
      Received frame 480 [7] A000FA019A0002
      Manager ->Write on HeatingModule SOFTWARE_VERSION 2 (0x0002)
      Rx serial port: 't4807A100FA07FC0000' length: 19
      Received frame 480 [7] A100FA07FC0000
      Manager ->Read on HeatingModule LZ_VERD_1_HEIZBETRIEB
      Rx serial port: 't4807A100FA07FD0000' length: 19
      Received frame 480 [7] A100FA07FD0000
      Manager ->Read on HeatingModule LZ_VERD_2_HEIZBETRIEB
      Rx serial port: 't4807A100FA07FE0000' length: 19
      Received frame 480 [7] A100FA07FE0000
      Manager ->Read on HeatingModule LZ_VERD_1_2_HEIZBETRIEB
      Rx serial port: 't4807A100FA08020000' length: 19
      Received frame 480 [7] A100FA08020000
      Manager ->Read on HeatingModule LZ_VERD_1_WW_BETRIEB
      Rx serial port: 't50079200FA01D600FC' length: 19
      Received frame 500 [7] 9200FA01D600FC
      HeatingModule ->Respond on Manager WPVORLAUFIST 25.2
      Rx serial port: 't4807A100FA08030000' length: 19
      Received frame 480 [7] A100FA08030000
      Manager ->Read on HeatingModule LZ_VERD_2_WW_BETRIEB
      Rx serial port: 't4807A100FA08040000' length: 19
      Received frame 480 [7] A100FA08040000
      Manager ->Read on HeatingModule LZ_VERD_1_2_WW_BETRIEB
      Rx serial port: 't4807A100FA07FF0000' length: 19
      Received frame 480 [7] A100FA07FF0000
      Manager ->Read on HeatingModule LZ_VERD_1_KUEHLBETRIEB
      Rx serial port: 't4807A100FA08000000' length: 19
      Received frame 480 [7] A100FA08000000
      Manager ->Read on HeatingModule LZ_VERD_2_KUEHLBETRIEB
      Rx serial port: 't4807A100FA08010000' length: 19
      Received frame 480 [7] A100FA08010000
      Manager ->Read on HeatingModule LZ_VERD_1_2_KUEHLBETRIEB
      Rx serial port: 't4807A100FA01990000' length: 19
      Received frame 480 [7] A100FA01990000
      Manager ->Read on HeatingModule SOFTWARE_NUMMER
      Rx serial port: 't500792001600CC0000' length: 19
      Received frame 500 [7] 92001600CC0000
      HeatingModule ->Respond on Manager RUECKLAUFISTTEMP 20.4
      Rx serial port: 't4807A100FA019A0000' length: 19
      Received frame 480 [7] A100FA019A0000
      Manager ->Read on HeatingModule SOFTWARE_VERSION
      Rx serial port: 't50079200FA07FC8000' length: 19
      Received frame 500 [7] 9200FA07FC8000
      HeatingModule ->Respond on Manager LZ_VERD_1_HEIZBETRIEB -32.768
      Rx serial port: 't50079200FA07FD20C6' length: 19
      Received frame 500 [7] 9200FA07FD20C6
      HeatingModule ->Respond on Manager LZ_VERD_2_HEIZBETRIEB 8.390
      Rx serial port: 't50079200FA07FE0000' length: 19
      Received frame 500 [7] 9200FA07FE0000
      HeatingModule ->Respond on Manager LZ_VERD_1_2_HEIZBETRIEB 0.000
      Rx serial port: 't50079200FA08028000' length: 19
      Received frame 500 [7] 9200FA08028000
      HeatingModule ->Respond on Manager LZ_VERD_1_WW_BETRIEB -32768 (0x8000)
      Rx serial port: 't50079200FA08030CD1' length: 19
      Received frame 500 [7] 9200FA08030CD1
      HeatingModule ->Respond on Manager LZ_VERD_2_WW_BETRIEB 3281 (0x0CD1)
      Rx serial port: 't50079200FA08040000' length: 19
      Received frame 500 [7] 9200FA08040000
      HeatingModule ->Respond on Manager LZ_VERD_1_2_WW_BETRIEB 0 (0x0000)
      Rx serial port: 't50079200FA07FF8000' length: 19
      Received frame 500 [7] 9200FA07FF8000
      HeatingModule ->Respond on Manager LZ_VERD_1_KUEHLBETRIEB -32.768
      Rx serial port: 't50079200FA0800005C' length: 19
      Received frame 500 [7] 9200FA0800005C
      HeatingModule ->Respond on Manager LZ_VERD_2_KUEHLBETRIEB 0.092
      Rx serial port: 't50079200FA08010000' length: 19
      Received frame 500 [7] 9200FA08010000
      HeatingModule ->Respond on Manager LZ_VERD_1_2_KUEHLBETRIEB 0.000
      Rx serial port: 't50079200FA01990043' length: 19
      Received frame 500 [7] 9200FA01990043
      HeatingModule ->Respond on Manager SOFTWARE_NUMMER 67 (0x0043)
      Rx serial port: 't50079200FA019A0010' length: 19
      Received frame 500 [7] 9200FA019A0010
      HeatingModule ->Respond on Manager SOFTWARE_VERSION 16 (0x0010)
      Rx serial port: 't500790000100000000' length: 19
      Received frame 500 [7] 90000100000000
      HeatingModule ->Write on Manager FEHLERMELDUNG 0 (0x0000)
      Rx serial port: 't50079000FA13880000' length: 19
      Received frame 500 [7] 9000FA13880000
      HeatingModule ->Write on Manager SOLAR_KOLLEKTOR_3_I_ANTEIL 0 (0x0000)
      Rx serial port: 't500790001401090000' length: 19
      Received frame 500 [7] 90001401090000
      HeatingModule ->Write on Manager VERDAMPFERTEMP 9
      Rx serial port: 't500790000B43100000' length: 19
      Received frame 500 [7] 90000B43100000
      HeatingModule ->Write on Manager GERAETE_ID 67-16
      Rx serial port: 't500790005A81000000' length: 19
      Received frame 500 [7] 90005A81000000
      HeatingModule ->Write on Manager SPEICHER_STATUS -127
      Rx serial port: 't500790006000000000' length: 19
      Received frame 500 [7] 90006000000000
      HeatingModule ->Write on Manager SCHALTFKT_IWS 0 (0x0000)
      Rx serial port: 't50079000FA4E310000' length: 19
      Received frame 500 [7] 9000FA4E310000
      Elster CAN frame from HeatingModule ->Write on Manager with unknown elster index 4E31, with possible data: '0 - (et_default: 0, et_dec_val: 0, et_cent_val: 0, et_mil_val: 0, et_byte: 0, et_bool: False, et_little_bool: False, et_little_endian: 0, et_betriebsart: Notbetrieb, et_zeit: (0, 0), et_datum: (0, 0), et_time_domain: (00:00:00, 00:00:00), et_dev_nr: 1, et_err_nr: ERR 0, et_dev_id: 0-0)' [7] 9000FA4E310000
      Rx serial port: 't500790006100000000' length: 19
      Received frame 500 [7] 90006100000000
      HeatingModule ->Write on Manager ABTAUUNGAKTIV 0 (0x0000)
      Rx serial port: 't50079000FA079F0064' length: 19
      Received frame 500 [7] 9000FA079F0064
      HeatingModule ->Write on Manager STUETZSTELLE_ND1 100 (0x0064)
      Rx serial port: 't50079000FA07A006A4' length: 19
      Received frame 500 [7] 9000FA07A006A4
      HeatingModule ->Write on Manager STUETZSTELLE_ND2 1700 (0x06A4)
      Rx serial port: 't50079000FA07A10064' length: 19
      Received frame 500 [7] 9000FA07A10064
      HeatingModule ->Write on Manager STUETZSTELLE_HD1 100 (0x0064)
      Rx serial port: 't50079000FA07A20C1C' length: 19
      Received frame 500 [7] 9000FA07A20C1C
      HeatingModule ->Write on Manager STUETZSTELLE_HD2 3100 (0x0C1C)
      Rx serial port: 't50079000FA07F20000' length: 19
      Received frame 500 [7] 9000FA07F20000
      HeatingModule ->Write on Manager BETRIEBSART_WP 0 (0x0000)
      Rx serial port: 't50079000FA080E0000' length: 19
      Received frame 500 [7] 9000FA080E0000
      HeatingModule ->Write on Manager FATAL_ERROR 0 (0x0000)
      Rx serial port: 't50079000FA06390001' length: 19
      Received frame 500 [7] 9000FA06390001
      HeatingModule ->Write on Manager K_OS_RMX_RESERVE_INFO3 1 (0x0001)
      Rx serial port: 't50079000FA08280000' length: 19
      Received frame 500 [7] 9000FA08280000
      HeatingModule ->Write on Manager FEHLER_PARAMETERSATZ_IWS 0 (0x0000)
      Rx serial port: 't500790001401090000' length: 19
      Received frame 500 [7] 90001401090000
      HeatingModule ->Write on Manager VERDAMPFERTEMP 9
      Rx serial port: 't50079000FA06390001' length: 19
      Received frame 500 [7] 9000FA06390001
      HeatingModule ->Write on Manager K_OS_RMX_RESERVE_INFO3 1 (0x0001)
      Rx serial port: 't3017C0015802000000' length: 19
      Received frame 301 [7] C0015802000000
      RemoteControl ->Write on Mixer MISCHER_ZU 2
      Rx serial port: 't3017C001290A000000' length: 19
      Received frame 301 [7] C001290A000000
      RemoteControl ->Write on Mixer MAX_TEMP_HZK 256.0
      Rx serial port: 't3017C0012A1E000000' length: 19
      Received frame 301 [7] C0012A1E000000
      RemoteControl ->Write on Mixer KP 7680 (0x1E00)
      Rx serial port: 't3017C0017501910000' length: 19
      Received frame 301 [7] C0017501910000
      RemoteControl ->Write on Mixer FEUCHTE 40.1
      Rx serial port: 't3017C001FA010F0500' length: 19
      Received frame 301 [7] C001FA010F0500
      RemoteControl ->Write on Mixer RAUMEINFLUSS 1280 (0x0500)
      Rx serial port: 't3017C0011100CA0000' length: 19
      Received frame 301 [7] C0011100CA0000
      RemoteControl ->Write on Mixer RAUMISTTEMP 20.2
      Rx serial port: 't3017C0011200CA0000' length: 19
      Received frame 301 [7] C0011200CA0000
      RemoteControl ->Write on Mixer VERSTELLTE_RAUMSOLLTEMP 20.2
      Rx serial port: 't3017C1011100CA0000' length: 19
      Received frame 301 [7] C1011100CA0000
      RemoteControl ->Read on Mixer RAUMISTTEMP
      Rx serial port: 't6017620111FE700000' length: 19
      Received frame 601 [7] 620111FE700000
      Mixer ->Respond on RemoteControl RAUMISTTEMP -40.0
      Rx serial port: 't3017C1011200CA0000' length: 19
      Received frame 301 [7] C1011200CA0000
      RemoteControl ->Read on Mixer VERSTELLTE_RAUMSOLLTEMP
      Rx serial port: 't6017620112FE700000' length: 19
      Received frame 601 [7] 620112FE700000
      Mixer ->Respond on RemoteControl VERSTELLTE_RAUMSOLLTEMP -40.0
      Rx serial port: 't3017C1010F80000000' length: 19
      Received frame 301 [7] C1010F80000000
      RemoteControl ->Read on Mixer VORLAUFISTTEMP
      Rx serial port: 't601762010FFE700000' length: 19
      Received frame 601 [7] 62010FFE700000
      Mixer ->Respond on RemoteControl VORLAUFISTTEMP -40.0
      Rx serial port: 't3017C10104015E0000' length: 19
      Received frame 301 [7] C10104015E0000
      RemoteControl ->Read on Mixer VORLAUFSOLLTEMP
      Rx serial port: 't6017620104FE700000' length: 19
      Received frame 601 [7] 620104FE700000
      Mixer ->Respond on RemoteControl VORLAUFSOLLTEMP -40.0
      Rx serial port: 't48072000FA13880000' length: 19
      Received frame 480 [7] 2000FA13880000
      Manager ->Write on Unknown_100h SOLAR_KOLLEKTOR_3_I_ANTEIL 0 (0x0000)
      Rx serial port: 't48072000FA080E0000' length: 19
      Received frame 480 [7] 2000FA080E0000
      Manager ->Write on Unknown_100h FATAL_ERROR 0 (0x0000)
      Rx serial port: 't180760790CFFFD0000' length: 19
      Received frame 180 [7] 60790CFFFD0000
      Boiler ->Write on RemoteControl_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't180760791600E90000' length: 19
      Received frame 180 [7] 60791600E90000
      Boiler ->Write on RemoteControl_Broadcast RUECKLAUFISTTEMP 23.3
      Rx serial port: 't180760792300000000' length: 19
      Received frame 180 [7] 60792300000000
      Boiler ->Write on RemoteControl_Broadcast MAX_HYSTERESE 0
      Rx serial port: 't180760790E01AD0000' length: 19
      Received frame 180 [7] 60790E01AD0000
      Boiler ->Write on RemoteControl_Broadcast SPEICHERISTTEMP 42.9
      Rx serial port: 't180760790DFE700000' length: 19
      Received frame 180 [7] 60790DFE700000
      Boiler ->Write on RemoteControl_Broadcast SAMMLERISTTEMP -40.0
      Rx serial port: 't180760791000310000' length: 19
      Received frame 180 [7] 60791000310000
      Boiler ->Write on RemoteControl_Broadcast GERAETEKONFIGURATION 49 (0x0031)
      Rx serial port: 't180760795A02000000' length: 19
      Received frame 180 [7] 60795A02000000
      Boiler ->Write on RemoteControl_Broadcast SPEICHER_STATUS 2
      Rx serial port: 't180760795100000000' length: 19
      Received frame 180 [7] 60795100000000
      Boiler ->Write on RemoteControl_Broadcast MULTIFUNKTION_ISTTEMP 0.0
      Rx serial port: 't180760790100000000' length: 19
      Received frame 180 [7] 60790100000000
      Boiler ->Write on RemoteControl_Broadcast FEHLERMELDUNG 0 (0x0000)
      Rx serial port: 't180760795E80000000' length: 19
      Received frame 180 [7] 60795E80000000
      Boiler ->Write on RemoteControl_Broadcast TEILVORRANG_WW -32768 (0x8000)
      Rx serial port: 't180760795300000000' length: 19
      Received frame 180 [7] 60795300000000
      Boiler ->Write on RemoteControl_Broadcast HZK_PUMPE 0 (0x0000)
      Rx serial port: 't1807607909290B0000' length: 19
      Received frame 180 [7] 607909290B0000
      Boiler ->Write on RemoteControl_Broadcast UHRZEIT 11:41
      Rx serial port: 't180760790A1E0C1800' length: 19
      Received frame 180 [7] 60790A1E0C1800
      Boiler ->Write on RemoteControl_Broadcast DATUM 30.12.
      Rx serial port: 't18076079FA01D20000' length: 19
      Received frame 180 [7] 6079FA01D20000
      Boiler ->Write on RemoteControl_Broadcast PUMPENSTATUS 0
      Rx serial port: 't18076079FA0A000033' length: 19
      Received frame 180 [7] 6079FA0A000033
      Boiler ->Write on RemoteControl_Broadcast AUSSEN_FROSTTEMP 5.1
      Rx serial port: 't301730000201540000' length: 19
      Received frame 301 [7] 30000201540000
      RemoteControl ->Write on Boiler KESSELSOLLTEMP 34.0
      Rx serial port: 't18076079FA05E00000' length: 19
      Received frame 180 [7] 6079FA05E00000
      Boiler ->Write on RemoteControl_Broadcast SCHALTFKT_QUELLE_IWS 0 (0x0000)
      Rx serial port: 't18076079FA0A200000' length: 19
      Received frame 180 [7] 6079FA0A200000
      Boiler ->Write on RemoteControl_Broadcast SAMMEL_RELAISSTATUS 0 (0x0000)
      Rx serial port: 't18076079FA0199014B' length: 19
      Received frame 180 [7] 6079FA0199014B
      Boiler ->Write on RemoteControl_Broadcast SOFTWARE_NUMMER 331 (0x014B)
      Rx serial port: 't18076079FA05E00000' length: 19
      Received frame 180 [7] 6079FA05E00000
      Boiler ->Write on RemoteControl_Broadcast SCHALTFKT_QUELLE_IWS 0 (0x0000)
      Rx serial port: 't3017C0017501910000' length: 19
      Received frame 301 [7] C0017501910000
      RemoteControl ->Write on Mixer FEUCHTE 40.1
      Rx serial port: 't3017C0011100CA0000' length: 19
      Received frame 301 [7] C0011100CA0000
      RemoteControl ->Write on Mixer RAUMISTTEMP 20.2
      Rx serial port: 't301731000BC3060000' length: 19
      Received frame 301 [7] 31000BC3060000
      RemoteControl ->Read on Boiler GERAETE_ID
      Rx serial port: 't180762010B80000000' length: 19
      Received frame 180 [7] 62010B80000000
      Boiler ->Respond on RemoteControl GERAETE_ID -128-00
      Rx serial port: 't3017C0010401350000' length: 19
      Received frame 301 [7] C0010401350000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 30.9
      Rx serial port: 't3017C0010401320000' length: 19
      Received frame 301 [7] C0010401320000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 30.6
      Rx serial port: 't3017C00104012F0000' length: 19
      Received frame 301 [7] C00104012F0000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 30.3
      Rx serial port: 't3017C0010401280000' length: 19
      Received frame 301 [7] C0010401280000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 29.6
      Rx serial port: 't3017C0010401210000' length: 19
      Received frame 301 [7] C0010401210000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 28.9
      Rx serial port: 't3017C00104011A0000' length: 19
      Received frame 301 [7] C00104011A0000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 28.2
      Rx serial port: 't3017C0010401140000' length: 19
      Received frame 301 [7] C0010401140000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 27.6
      Rx serial port: 't3017C00104010E0000' length: 19
      Received frame 301 [7] C00104010E0000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 27.0
      Rx serial port: 't3017C0010401080000' length: 19
      Received frame 301 [7] C0010401080000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 26.4
      Rx serial port: 't3017C0010401020000' length: 19
      Received frame 301 [7] C0010401020000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 25.8
      Rx serial port: 't3017C0010400FC0000' length: 19
      Received frame 301 [7] C0010400FC0000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 25.2
      Rx serial port: 't301730000200F70000' length: 19
      Received frame 301 [7] 30000200F70000
      RemoteControl ->Write on Boiler KESSELSOLLTEMP 24.7
      Rx serial port: 't3017C0010400F60000' length: 19
      Received frame 301 [7] C0010400F60000
      RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 24.6
      Rx serial port: 't601760010FFE700000' length: 19
      Received frame 601 [7] 60010FFE700000
      Mixer ->Write on RemoteControl VORLAUFISTTEMP -40.0
      Rx serial port: 't6017600104FE700000' length: 19
      Received frame 601 [7] 600104FE700000
      Mixer ->Write on RemoteControl VORLAUFSOLLTEMP -40.0
      Rx serial port: 't601760010100000000' length: 19
      Received frame 601 [7] 60010100000000
      Mixer ->Write on RemoteControl FEHLERMELDUNG 0 (0x0000)
      Rx serial port: 't601760015200000000' length: 19
      Received frame 601 [7] 60015200000000
      Mixer ->Write on RemoteControl BRENNER 0
      Rx serial port: 't601760015A02000000' length: 19
      Received frame 601 [7] 60015A02000000
      Mixer ->Write on RemoteControl SPEICHER_STATUS 2
      Rx serial port: 't601760015600000000' length: 19
      Received frame 601 [7] 60015600000000
      Mixer ->Write on RemoteControl DCF 0
      Rx serial port: 't601760015700000000' length: 19
      Received frame 601 [7] 60015700000000
      Mixer ->Write on RemoteControl MISCHER_AUF 0
      Rx serial port: 't6017600128015E0000' length: 19
      Received frame 601 [7] 600128015E0000
      Mixer ->Write on RemoteControl MAX_TEMP_KESSEL 35.0
      Rx serial port: 't48072000FA01990186' length: 19
      Received frame 480 [7] 2000FA01990186
      Manager ->Write on Unknown_100h SOFTWARE_NUMMER 390 (0x0186)
      Rx serial port: 't180760792300000000' length: 19
      Received frame 180 [7] 60792300000000
      Boiler ->Write on RemoteControl_Broadcast MAX_HYSTERESE 0
      Rx serial port: 't180760790E01AD0000' length: 19
      Received frame 180 [7] 60790E01AD0000
      Boiler ->Write on RemoteControl_Broadcast SPEICHERISTTEMP 42.9
      Rx serial port: 't180760795A02000000' length: 19
      Received frame 180 [7] 60795A02000000
      Boiler ->Write on RemoteControl_Broadcast SPEICHER_STATUS 2
      Rx serial port: 't18076079FA0A000033' length: 19
      Received frame 180 [7] 6079FA0A000033
      Boiler ->Write on RemoteControl_Broadcast AUSSEN_FROSTTEMP 5.1
      Rx serial port: 't4807A000FA02CA00EA' length: 19
      Received frame 480 [7] A000FA02CA00EA
      Manager ->Write on HeatingModule MATERIALNUMMER_HIGH 234 (0x00EA)
      Rx serial port: 't4807A0005F00000000' length: 19
      Received frame 480 [7] A0005F00000000
      Manager ->Write on HeatingModule SPEICHERBEDARF 0 (0x0000)
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807A0793001000000' length: 19
      Received frame 480 [7] A0793001000000
      Manager ->Write on HeatingModule_Broadcast ACCESS_EEPROM 1
      Rx serial port: 't4807A100FA07180000' length: 19
      Received frame 480 [7] A100FA07180000
      Manager ->Read on HeatingModule TEST_OBJEKT_215
      Rx serial port: 't1807A079FA01D700F6' length: 19
      Received frame 180 [7] A079FA01D700F6
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.6
      Rx serial port: 't50079200FA07180064' length: 19
      Received frame 500 [7] 9200FA07180064
      HeatingModule ->Respond on Manager TEST_OBJEKT_215 100 (0x0064)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F6' length: 19
      Received frame 180 [7] A079FA01D700F6
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.6
      Rx serial port: 't4807A0005E00000000' length: 19
      Received frame 480 [7] A0005E00000000
      Manager ->Write on HeatingModule TEILVORRANG_WW 0 (0x0000)
      Rx serial port: 't480720007400010000' length: 19
      Received frame 480 [7] 20007400010000
      Manager ->Write on Unknown_100h EVU_SPERRE_AKTIV 1 (0x0001)
      Rx serial port: 't4807A100FA07FC0000' length: 19
      Received frame 480 [7] A100FA07FC0000
      Manager ->Read on HeatingModule LZ_VERD_1_HEIZBETRIEB
      Rx serial port: 't48072000FA4E790001' length: 19
      Received frame 480 [7] 2000FA4E790001
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E79, with possible data: '1 - (et_default: 1, et_dec_val: 0.1, et_cent_val: 0.01, et_mil_val: 0.001, et_byte: 1, et_bool: True, et_little_endian: 256, et_betriebsart: ?:et_betriebsart1, et_zeit: (0, 1), et_datum: (0, 1), et_time_domain: (00:00:00, 00:15:00), et_dev_nr: 2, et_err_nr: ERR 1, et_dev_id: 0-1)' [7] 2000FA4E790001
      Rx serial port: 't50079200FA07FC8000' length: 19
      Received frame 500 [7] 9200FA07FC8000
      HeatingModule ->Respond on Manager LZ_VERD_1_HEIZBETRIEB -32.768
      Rx serial port: 't48072000FA06D60001' length: 19
      Received frame 480 [7] 2000FA06D60001
      Manager ->Write on Unknown_100h TEST_OBJEKT_149 1 (0x0001)
      Rx serial port: 't48072000FA06D70001' length: 19
      Received frame 480 [7] 2000FA06D70001
      Manager ->Write on Unknown_100h TEST_OBJEKT_150 1 (0x0001)
      Rx serial port: 't48072000FA06D80001' length: 19
      Received frame 480 [7] 2000FA06D80001
      Manager ->Write on Unknown_100h TEST_OBJEKT_151 1 (0x0001)
      Rx serial port: 't4807A100FA07FD0000' length: 19
      Received frame 480 [7] A100FA07FD0000
      Manager ->Read on HeatingModule LZ_VERD_2_HEIZBETRIEB
      Rx serial port: 't48072000FA01120200' length: 19
      Received frame 480 [7] 2000FA01120200
      Manager ->Write on Unknown_100h PROGRAMMSCHALTER Automatik
      Rx serial port: 't50079200FA07FD20C6' length: 19
      Received frame 500 [7] 9200FA07FD20C6
      HeatingModule ->Respond on Manager LZ_VERD_2_HEIZBETRIEB 8.390
      Rx serial port: 't4807A100FA07FE0000' length: 19
      Received frame 480 [7] A100FA07FE0000
      Manager ->Read on HeatingModule LZ_VERD_1_2_HEIZBETRIEB
      Rx serial port: 't48072000FA01210100' length: 19
      Received frame 480 [7] 2000FA01210100
      Manager ->Write on Unknown_100h WOCHENTAG 1
      Rx serial port: 't50079200FA07FE0000' length: 19
      Received frame 500 [7] 9200FA07FE0000
      HeatingModule ->Respond on Manager LZ_VERD_1_2_HEIZBETRIEB 0.000
      Rx serial port: 't4807A100FA08020000' length: 19
      Received frame 480 [7] A100FA08020000
      Manager ->Read on HeatingModule LZ_VERD_1_WW_BETRIEB
      Rx serial port: 't48072000FA4E5C0004' length: 19
      Received frame 480 [7] 2000FA4E5C0004
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E5C, with possible data: '4 - (et_default: 4, et_dec_val: 0.4, et_cent_val: 0.04, et_mil_val: 0.004, et_byte: 4, et_little_endian: 1024, et_betriebsart: ?:et_betriebsart4, et_zeit: (0, 4), et_datum: (0, 4), et_time_domain: (00:00:00, 01:00:00), et_dev_nr: 5, et_err_nr: Hochdruck, et_dev_id: 0-4)' [7] 2000FA4E5C0004
      Rx serial port: 't10076101FA01990000' length: 19
      Received frame 100 [7] 6101FA01990000
      Unknown_100h ->Read on RemoteControl SOFTWARE_NUMMER
      Rx serial port: 't10076101FA019A0000' length: 19
      Received frame 100 [7] 6101FA019A0000
      Unknown_100h ->Read on RemoteControl SOFTWARE_VERSION
      Rx serial port: 't4807A100FA08030000' length: 19
      Received frame 480 [7] A100FA08030000
      Manager ->Read on HeatingModule LZ_VERD_2_WW_BETRIEB
      Rx serial port: 't50079200FA08028000' length: 19
      Received frame 500 [7] 9200FA08028000
      HeatingModule ->Respond on Manager LZ_VERD_1_WW_BETRIEB -32768 (0x8000)
      Rx serial port: 't48072000FA019A0002' length: 19
      Received frame 480 [7] 2000FA019A0002
      Manager ->Write on Unknown_100h SOFTWARE_VERSION 2 (0x0002)
      Rx serial port: 't4807A100FA08040000' length: 19
      Received frame 480 [7] A100FA08040000
      Manager ->Read on HeatingModule LZ_VERD_1_2_WW_BETRIEB
      Rx serial port: 't48072000FA01990186' length: 19
      Received frame 480 [7] 2000FA01990186
      Manager ->Write on Unknown_100h SOFTWARE_NUMMER 390 (0x0186)
      Rx serial port: 't4807A100FA02590000' length: 19
      Received frame 480 [7] A100FA02590000
      Manager ->Read on HeatingModule LAUFZEIT_DHC1
      Rx serial port: 't48072000FA13880000' length: 19
      Received frame 480 [7] 2000FA13880000
      Manager ->Write on Unknown_100h SOLAR_KOLLEKTOR_3_I_ANTEIL 0 (0x0000)
      Rx serial port: 't30172200FA019900C3' length: 19
      Received frame 301 [7] 2200FA019900C3
      RemoteControl ->Respond on Unknown_100h SOFTWARE_NUMMER 195 (0x00C3)
      Rx serial port: 't4807A100FA025A0000' length: 19
      Received frame 480 [7] A100FA025A0000
      Manager ->Read on HeatingModule LAUFZEIT_DHC2
      Rx serial port: 't48072000FA4E5E0000' length: 19
      Received frame 480 [7] 2000FA4E5E0000
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E5E, with possible data: '0 - (et_default: 0, et_dec_val: 0, et_cent_val: 0, et_mil_val: 0, et_byte: 0, et_bool: False, et_little_bool: False, et_little_endian: 0, et_betriebsart: Notbetrieb, et_zeit: (0, 0), et_datum: (0, 0), et_time_domain: (00:00:00, 00:00:00), et_dev_nr: 1, et_err_nr: ERR 0, et_dev_id: 0-0)' [7] 2000FA4E5E0000
      Rx serial port: 't50079200FA08030CD1' length: 19
      Received frame 500 [7] 9200FA08030CD1
      HeatingModule ->Respond on Manager LZ_VERD_2_WW_BETRIEB 3281 (0x0CD1)
      Rx serial port: 't48072000FA4E6A0000' length: 19
      Received frame 480 [7] 2000FA4E6A0000
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E6A, with possible data: '0 - (et_default: 0, et_dec_val: 0, et_cent_val: 0, et_mil_val: 0, et_byte: 0, et_bool: False, et_little_bool: False, et_little_endian: 0, et_betriebsart: Notbetrieb, et_zeit: (0, 0), et_datum: (0, 0), et_time_domain: (00:00:00, 00:00:00), et_dev_nr: 1, et_err_nr: ERR 0, et_dev_id: 0-0)' [7] 2000FA4E6A0000
      Rx serial port: 't50079200FA08040000' length: 19
      Received frame 500 [7] 9200FA08040000
      HeatingModule ->Respond on Manager LZ_VERD_1_2_WW_BETRIEB 0 (0x0000)
      Rx serial port: 't4807A100FA08050000' length: 19
      Received frame 480 [7] A100FA08050000
      Manager ->Read on HeatingModule LZ_DHC12
      Rx serial port: 't48072000FA06D60001' length: 19
      Received frame 480 [7] 2000FA06D60001
      Manager ->Write on Unknown_100h TEST_OBJEKT_149 1 (0x0001)
      Rx serial port: 't30172200FA019A0006' length: 19
      Received frame 301 [7] 2200FA019A0006
      RemoteControl ->Respond on Unknown_100h SOFTWARE_VERSION 6 (0x0006)
      Rx serial port: 't4807A100FA08000000' length: 19
      Received frame 480 [7] A100FA08000000
      Manager ->Read on HeatingModule LZ_VERD_2_KUEHLBETRIEB
      Rx serial port: 't48072000FA4E61084E' length: 19
      Received frame 480 [7] 2000FA4E61084E
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E61, with possible data: '2126 - (et_default: 2126, et_dec_val: 212.6, et_cent_val: 21.26, et_mil_val: 2.126, et_byte: 78, et_little_endian: 19976, et_betriebsart: ?:et_betriebsart2126, et_zeit: (8, 78), et_datum: (8, 78), et_time_domain: (02:00:00, 19:30:00), et_dev_nr: 2127, et_err_nr: ERR 2126, et_dev_id: 8-78)' [7] 2000FA4E61084E
      Rx serial port: 't50079200FA02590036' length: 19
      Received frame 500 [7] 9200FA02590036
      HeatingModule ->Respond on Manager LAUFZEIT_DHC1 54 (0x0036)
      Rx serial port: 't48072000FA4E890001' length: 19
      Received frame 480 [7] 2000FA4E890001
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E89, with possible data: '1 - (et_default: 1, et_dec_val: 0.1, et_cent_val: 0.01, et_mil_val: 0.001, et_byte: 1, et_bool: True, et_little_endian: 256, et_betriebsart: ?:et_betriebsart1, et_zeit: (0, 1), et_datum: (0, 1), et_time_domain: (00:00:00, 00:15:00), et_dev_nr: 2, et_err_nr: ERR 1, et_dev_id: 0-1)' [7] 2000FA4E890001
      Rx serial port: 't48072000FA4E62008E' length: 19
      Received frame 480 [7] 2000FA4E62008E
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E62, with possible data: '142 - (et_default: 142, et_dec_val: 14.2, et_cent_val: 1.42, et_mil_val: 0.142, et_byte: -114, et_little_endian: 36352, et_betriebsart: ?:et_betriebsart142, et_zeit: (0, 142), et_datum: (0, 142), et_time_domain: (00:00:00, 1.11:30:00), et_dev_nr: 143, et_err_nr: ERR 142, et_dev_id: 0-142)' [7] 2000FA4E62008E
      Rx serial port: 't4807A100FA08080000' length: 19
      Received frame 480 [7] A100FA08080000
      Manager ->Read on HeatingModule ABTAUZEIT_VERD1
      Rx serial port: 't48072000FA4E560011' length: 19
      Received frame 480 [7] 2000FA4E560011
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E56, with possible data: '17 - (et_default: 17, et_dec_val: 1.7, et_cent_val: 0.17, et_mil_val: 0.017, et_byte: 17, et_little_endian: 4352, et_betriebsart: ?:et_betriebsart17, et_zeit: (0, 17), et_datum: (0, 17), et_time_domain: (00:00:00, 04:15:00), et_dev_nr: 18, et_err_nr: ERR 17, et_dev_id: 0-17)' [7] 2000FA4E560011
      Rx serial port: 't50079200FA025A0025' length: 19
      Received frame 500 [7] 9200FA025A0025
      HeatingModule ->Respond on Manager LAUFZEIT_DHC2 37 (0x0025)
      Rx serial port: 't4807A100FA08090000' length: 19
      Received frame 480 [7] A100FA08090000
      Manager ->Read on HeatingModule ABTAUZEIT_VERD2
      Rx serial port: 't51472000FA4E7C01F7' length: 19
      Received frame 514 [7] 2000FA4E7C01F7
      Elster CAN frame from 514 ->Write on Unknown_100h with unknown elster index 4E7C, with possible data: '503 - (et_default: 503, et_dec_val: 50.3, et_cent_val: 5.03, et_mil_val: 0.503, et_byte: -9, et_little_endian: 63233, et_betriebsart: ?:et_betriebsart503, et_zeit: (1, 247), et_datum: (1, 247), et_time_domain: (00:15:00, 2.13:45:00), et_dev_nr: 504, et_err_nr: ERR 503, et_dev_id: 1-247)' [7] 2000FA4E7C01F7
      Rx serial port: 't51472000FA4E7D0028' length: 19
      Received frame 514 [7] 2000FA4E7D0028
      Elster CAN frame from 514 ->Write on Unknown_100h with unknown elster index 4E7D, with possible data: '40 - (et_default: 40, et_dec_val: 4, et_cent_val: 0.4, et_mil_val: 0.04, et_byte: 40, et_little_endian: 10240, et_betriebsart: ?:et_betriebsart40, et_zeit: (0, 40), et_datum: (0, 40), et_time_domain: (00:00:00, 10:00:00), et_dev_nr: 41, et_err_nr: ERR 40, et_dev_id: 0-40)' [7] 2000FA4E7D0028
      Rx serial port: 't51472000FA4E880090' length: 19
      Received frame 514 [7] 2000FA4E880090
      Elster CAN frame from 514 ->Write on Unknown_100h with unknown elster index 4E88, with possible data: '144 - (et_default: 144, et_dec_val: 14.4, et_cent_val: 1.44, et_mil_val: 0.144, et_byte: -112, et_little_endian: 36864, et_betriebsart: ?:et_betriebsart144, et_zeit: (0, 144), et_datum: (0, 144), et_time_domain: (00:00:00, 1.12:00:00), et_dev_nr: 145, et_err_nr: ERR 144, et_dev_id: 0-144)' [7] 2000FA4E880090
      Rx serial port: 't50079200FA08050188' length: 19
      Received frame 500 [7] 9200FA08050188
      HeatingModule ->Respond on Manager LZ_DHC12 392 (0x0188)
      Rx serial port: 't48072000FA4E591100' length: 19
      Received frame 480 [7] 2000FA4E591100
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E59, with possible data: '4352 - (et_default: 4352, et_dec_val: 435.2, et_cent_val: 43.52, et_mil_val: 4.352, et_byte: 0, et_little_endian: 17, et_betriebsart: ?:et_betriebsart4352, et_zeit: (17, 0), et_datum: (17, 0), et_time_domain: (04:15:00, 00:00:00), et_dev_nr: 4353, et_err_nr: ERR 4352, et_dev_id: 17-0)' [7] 2000FA4E591100
      Rx serial port: 't48072000FA4E760F30' length: 19
      Received frame 480 [7] 2000FA4E760F30
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E76, with possible data: '3888 - (et_default: 3888, et_dec_val: 388.8, et_cent_val: 38.88, et_mil_val: 3.888, et_byte: 48, et_little_endian: 12303, et_betriebsart: ?:et_betriebsart3888, et_zeit: (15, 48), et_datum: (15, 48), et_time_domain: (03:45:00, 12:00:00), et_dev_nr: 3889, et_err_nr: ERR 3888, et_dev_id: 15-48)' [7] 2000FA4E760F30
      Rx serial port: 't48072000FA4E7700A0' length: 19
      Received frame 480 [7] 2000FA4E7700A0
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E77, with possible data: '160 - (et_default: 160, et_dec_val: 16, et_cent_val: 1.6, et_mil_val: 0.16, et_byte: -96, et_little_endian: 40960, et_betriebsart: ?:et_betriebsart160, et_zeit: (0, 160), et_datum: (0, 160), et_time_domain: (00:00:00, 1.16:00:00), et_dev_nr: 161, et_err_nr: ERR 160, et_dev_id: 0-160)' [7] 2000FA4E7700A0
      Rx serial port: 't50079200FA0800005C' length: 19
      Received frame 500 [7] 9200FA0800005C
      HeatingModule ->Respond on Manager LZ_VERD_2_KUEHLBETRIEB 0.092
      Rx serial port: 't48072000FA4E780010' length: 19
      Received frame 480 [7] 2000FA4E780010
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E78, with possible data: '16 - (et_default: 16, et_dec_val: 1.6, et_cent_val: 0.16, et_mil_val: 0.016, et_byte: 16, et_little_endian: 4096, et_betriebsart: ?:et_betriebsart16, et_zeit: (0, 16), et_datum: (0, 16), et_time_domain: (00:00:00, 04:00:00), et_dev_nr: 17, et_err_nr: Abtauen, et_dev_id: 0-16)' [7] 2000FA4E780010
      Rx serial port: 't51472000FA4E81000F' length: 19
      Received frame 514 [7] 2000FA4E81000F
      Elster CAN frame from 514 ->Write on Unknown_100h with unknown elster index 4E81, with possible data: '15 - (et_default: 15, et_dec_val: 1.5, et_cent_val: 0.15, et_mil_val: 0.015, et_byte: 15, et_little_endian: 3840, et_betriebsart: ?:et_betriebsart15, et_zeit: (0, 15), et_datum: (0, 15), et_time_domain: (00:00:00, 03:45:00), et_dev_nr: 16, et_err_nr: ERR 15, et_dev_id: 0-15)' [7] 2000FA4E81000F
      Rx serial port: 't48072000FA0641003F' length: 19
      Received frame 480 [7] 2000FA0641003F
      Manager ->Write on Unknown_100h TEST_OBJEKT_0 63 (0x003F)
      Rx serial port: 't50079200FA08080000' length: 19
      Received frame 500 [7] 9200FA08080000
      HeatingModule ->Respond on Manager ABTAUZEIT_VERD1 0 (0x0000)
      Rx serial port: 't48072000FA06D70001' length: 19
      Received frame 480 [7] 2000FA06D70001
      Manager ->Write on Unknown_100h TEST_OBJEKT_150 1 (0x0001)
      Rx serial port: 't48072000FA06D80001' length: 19
      Received frame 480 [7] 2000FA06D80001
      Manager ->Write on Unknown_100h TEST_OBJEKT_151 1 (0x0001)
      Rx serial port: 't48072000FA06DF0258' length: 19
      Received frame 480 [7] 2000FA06DF0258
      Manager ->Write on Unknown_100h TEST_OBJEKT_158 600 (0x0258)
      Rx serial port: 't48072000FA06ED0000' length: 19
      Received frame 480 [7] 2000FA06ED0000
      Manager ->Write on Unknown_100h TEST_OBJEKT_172 0 (0x0000)
      Rx serial port: 't48072000FA06EE0001' length: 19
      Received frame 480 [7] 2000FA06EE0001
      Manager ->Write on Unknown_100h TEST_OBJEKT_173 1 (0x0001)
      Rx serial port: 't48072000FA06E00002' length: 19
      Received frame 480 [7] 2000FA06E00002
      Manager ->Write on Unknown_100h TEST_OBJEKT_159 2 (0x0002)
      Rx serial port: 't50079200FA080900A0' length: 19
      Received frame 500 [7] 9200FA080900A0
      HeatingModule ->Respond on Manager ABTAUZEIT_VERD2 160 (0x00A0)
      Rx serial port: 't60272000FA06DD0384' length: 19
      Received frame 602 [7] 2000FA06DD0384
      Mixer2 ->Write on Unknown_100h TEST_OBJEKT_156 900 (0x0384)
      Rx serial port: 't48072000FA06DE02BC' length: 19
      Received frame 480 [7] 2000FA06DE02BC
      Manager ->Write on Unknown_100h TEST_OBJEKT_157 700 (0x02BC)
      Rx serial port: 't18072000FA06E30000' length: 19
      Received frame 180 [7] 2000FA06E30000
      Boiler ->Write on Unknown_100h TEST_OBJEKT_162 0 (0x0000)
      Rx serial port: 't51472000FA4E84003B' length: 19
      Received frame 514 [7] 2000FA4E84003B
      Elster CAN frame from 514 ->Write on Unknown_100h with unknown elster index 4E84, with possible data: '59 - (et_default: 59, et_dec_val: 5.9, et_cent_val: 0.59, et_mil_val: 0.059, et_byte: 59, et_little_endian: 15104, et_betriebsart: ?:et_betriebsart59, et_zeit: (0, 59), et_datum: (0, 59), et_time_domain: (00:00:00, 14:45:00), et_dev_nr: 60, et_err_nr: ERR 59, et_dev_id: 0-59)' [7] 2000FA4E84003B
      Rx serial port: 't51472000FA4E8500AA' length: 19
      Received frame 514 [7] 2000FA4E8500AA
      Elster CAN frame from 514 ->Write on Unknown_100h with unknown elster index 4E85, with possible data: '170 - (et_default: 170, et_dec_val: 17, et_cent_val: 1.7, et_mil_val: 0.17, et_byte: -86, et_little_endian: 43520, et_betriebsart: ?:et_betriebsart170, et_zeit: (0, 170), et_datum: (0, 170), et_time_domain: (00:00:00, 1.18:30:00), et_dev_nr: 171, et_err_nr: ERR 170, et_dev_id: 0-170)' [7] 2000FA4E8500AA
      Rx serial port: 't51472000FA4E8600AA' length: 19
      Received frame 514 [7] 2000FA4E8600AA
      Elster CAN frame from 514 ->Write on Unknown_100h with unknown elster index 4E86, with possible data: '170 - (et_default: 170, et_dec_val: 17, et_cent_val: 1.7, et_mil_val: 0.17, et_byte: -86, et_little_endian: 43520, et_betriebsart: ?:et_betriebsart170, et_zeit: (0, 170), et_datum: (0, 170), et_time_domain: (00:00:00, 1.18:30:00), et_dev_nr: 171, et_err_nr: ERR 170, et_dev_id: 0-170)' [7] 2000FA4E8600AA
      Rx serial port: 't48072000FA06F70001' length: 19
      Received frame 480 [7] 2000FA06F70001
      Manager ->Write on Unknown_100h TEST_OBJEKT_182 1 (0x0001)
      Rx serial port: 't48072000FA06E80000' length: 19
      Received frame 480 [7] 2000FA06E80000
      Manager ->Write on Unknown_100h TEST_OBJEKT_167 0 (0x0000)
      Rx serial port: 't48072000FA06DBFF38' length: 19
      Received frame 480 [7] 2000FA06DBFF38
      Manager ->Write on Unknown_100h TEST_OBJEKT_154 -200 (0xFF38)
      Rx serial port: 't48072000FA06DCFF38' length: 19
      Received frame 480 [7] 2000FA06DCFF38
      Manager ->Write on Unknown_100h TEST_OBJEKT_155 -200 (0xFF38)
      Rx serial port: 't48072000FA06E10001' length: 19
      Received frame 480 [7] 2000FA06E10001
      Manager ->Write on Unknown_100h TEST_OBJEKT_160 1 (0x0001)
      Rx serial port: 't48072000FA06E2001E' length: 19
      Received frame 480 [7] 2000FA06E2001E
      Manager ->Write on Unknown_100h TEST_OBJEKT_161 30 (0x001E)
      Rx serial port: 't48072000FA06E5028A' length: 19
      Received frame 480 [7] 2000FA06E5028A
      Manager ->Write on Unknown_100h TEST_OBJEKT_164 650 (0x028A)
      Rx serial port: 't48072000FA06E602BC' length: 19
      Received frame 480 [7] 2000FA06E602BC
      Manager ->Write on Unknown_100h TEST_OBJEKT_165 700 (0x02BC)
      Rx serial port: 't48072000FA06E702EE' length: 19
      Received frame 480 [7] 2000FA06E702EE
      Manager ->Write on Unknown_100h TEST_OBJEKT_166 750 (0x02EE)
      Rx serial port: 't48072000FA01262900' length: 19
      Received frame 480 [7] 2000FA01262900
      Manager ->Write on Unknown_100h MINUTE 41
      Rx serial port: 't10079000FA06AF0001' length: 19
      Received frame 100 [7] 9000FA06AF0001
      Unknown_100h ->Write on Manager TEST_OBJEKT_110 1 (0x0001)
      Rx serial port: 't48072000FA01250B00' length: 19
      Received frame 480 [7] 2000FA01250B00
      Manager ->Write on Unknown_100h STUNDE 11
      Rx serial port: 't48072000FA01221E00' length: 19
      Received frame 480 [7] 2000FA01221E00
      Manager ->Write on Unknown_100h TAG 30
      Rx serial port: 't48072000FA01230C00' length: 19
      Received frame 480 [7] 2000FA01230C00
      Manager ->Write on Unknown_100h MONAT 12
      Rx serial port: 't48072000FA01241800' length: 19
      Received frame 480 [7] 2000FA01241800
      Manager ->Write on Unknown_100h JAHR 24
      Rx serial port: 't48072000FA4E5D0005' length: 19
      Received frame 480 [7] 2000FA4E5D0005
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E5D, with possible data: '5 - (et_default: 5, et_dec_val: 0.5, et_cent_val: 0.05, et_mil_val: 0.005, et_byte: 5, et_little_endian: 1280, et_betriebsart: ?:et_betriebsart5, et_zeit: (0, 5), et_datum: (0, 5), et_time_domain: (00:00:00, 01:15:00), et_dev_nr: 6, et_err_nr: Verdampferfuehler, et_dev_id: 0-5)' [7] 2000FA4E5D0005
      Rx serial port: 't100731000C00000000' length: 19
      Received frame 100 [7] 31000C00000000
      Unknown_100h ->Read on Boiler AUSSENTEMP
      Rx serial port: 't180722000CFFFD0000' length: 19
      Received frame 180 [7] 22000CFFFD0000
      Boiler ->Respond on Unknown_100h AUSSENTEMP -0.3
      Rx serial port: 't100731001600000000' length: 19
      Received frame 100 [7] 31001600000000
      Unknown_100h ->Read on Boiler RUECKLAUFISTTEMP
      Rx serial port: 't180722001600EA0000' length: 19
      Received frame 180 [7] 22001600EA0000
      Boiler ->Respond on Unknown_100h RUECKLAUFISTTEMP 23.4
      Rx serial port: 't100731000E00000000' length: 19
      Received frame 100 [7] 31000E00000000
      Unknown_100h ->Read on Boiler SPEICHERISTTEMP
      Rx serial port: 't180722000E01AD0000' length: 19
      Received frame 180 [7] 22000E01AD0000
      Boiler ->Respond on Unknown_100h SPEICHERISTTEMP 42.9
      Rx serial port: 't4807A100FA01D60000' length: 19
      Received frame 480 [7] A100FA01D60000
      Manager ->Read on HeatingModule WPVORLAUFIST
      Rx serial port: 't4807A1001600000000' length: 19
      Received frame 480 [7] A1001600000000
      Manager ->Read on HeatingModule RUECKLAUFISTTEMP
      Rx serial port: 't50079200FA01D600FC' length: 19
      Received frame 500 [7] 9200FA01D600FC
      HeatingModule ->Respond on Manager WPVORLAUFIST 25.2
      Rx serial port: 't4807A100FA07A80000' length: 19
      Received frame 480 [7] A100FA07A80000
      Manager ->Read on HeatingModule VERDICHTER
      Rx serial port: 't4807A000FA02CA00EB' length: 19
      Received frame 480 [7] A000FA02CA00EB
      Manager ->Write on HeatingModule MATERIALNUMMER_HIGH 235 (0x00EB)
      Rx serial port: 't4807A000FA09170000' length: 19
      Received frame 480 [7] A000FA09170000
      Manager ->Write on HeatingModule INTEGRAL_REGELABWEICHUNG_RELATIV 0 (0x0000)
      Rx serial port: 't500792001600CB0000' length: 19
      Received frame 500 [7] 92001600CB0000
      HeatingModule ->Respond on Manager RUECKLAUFISTTEMP 20.3
      Rx serial port: 't48072000FA4E790001' length: 19
      Received frame 480 [7] 2000FA4E790001
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E79, with possible data: '1 - (et_default: 1, et_dec_val: 0.1, et_cent_val: 0.01, et_mil_val: 0.001, et_byte: 1, et_bool: True, et_little_endian: 256, et_betriebsart: ?:et_betriebsart1, et_zeit: (0, 1), et_datum: (0, 1), et_time_domain: (00:00:00, 00:15:00), et_dev_nr: 2, et_err_nr: ERR 1, et_dev_id: 0-1)' [7] 2000FA4E790001
      Rx serial port: 't48072000FA4E5C0004' length: 19
      Received frame 480 [7] 2000FA4E5C0004
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E5C, with possible data: '4 - (et_default: 4, et_dec_val: 0.4, et_cent_val: 0.04, et_mil_val: 0.004, et_byte: 4, et_little_endian: 1024, et_betriebsart: ?:et_betriebsart4, et_zeit: (0, 4), et_datum: (0, 4), et_time_domain: (00:00:00, 01:00:00), et_dev_nr: 5, et_err_nr: Hochdruck, et_dev_id: 0-4)' [7] 2000FA4E5C0004
      Rx serial port: 't48072000FA4E7F0101' length: 19
      Received frame 480 [7] 2000FA4E7F0101
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E7F, with possible data: '257 - (et_default: 257, et_dec_val: 25.7, et_cent_val: 2.57, et_mil_val: 0.257, et_byte: 1, et_little_endian: 257, et_betriebsart: ?:et_betriebsart257, et_zeit: (1, 1), et_datum: (1, 1), et_time_domain: (00:15:00, 00:15:00), et_dev_nr: 258, et_err_nr: ERR 257, et_dev_id: 1-1)' [7] 2000FA4E7F0101
      Rx serial port: 't48072000FA4E800001' length: 19
      Received frame 480 [7] 2000FA4E800001
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E80, with possible data: '1 - (et_default: 1, et_dec_val: 0.1, et_cent_val: 0.01, et_mil_val: 0.001, et_byte: 1, et_bool: True, et_little_endian: 256, et_betriebsart: ?:et_betriebsart1, et_zeit: (0, 1), et_datum: (0, 1), et_time_domain: (00:00:00, 00:15:00), et_dev_nr: 2, et_err_nr: ERR 1, et_dev_id: 0-1)' [7] 2000FA4E800001
      Rx serial port: 't48072000FA4E790001' length: 19
      Received frame 480 [7] 2000FA4E790001
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E79, with possible data: '1 - (et_default: 1, et_dec_val: 0.1, et_cent_val: 0.01, et_mil_val: 0.001, et_byte: 1, et_bool: True, et_little_endian: 256, et_betriebsart: ?:et_betriebsart1, et_zeit: (0, 1), et_datum: (0, 1), et_time_domain: (00:00:00, 00:15:00), et_dev_nr: 2, et_err_nr: ERR 1, et_dev_id: 0-1)' [7] 2000FA4E790001
      Rx serial port: 't50079200FA07A80008' length: 19
      Received frame 500 [7] 9200FA07A80008
      HeatingModule ->Respond on Manager VERDICHTER 0.8
      Rx serial port: 't48072000FA4E560011' length: 19
      Received frame 480 [7] 2000FA4E560011
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E56, with possible data: '17 - (et_default: 17, et_dec_val: 1.7, et_cent_val: 0.17, et_mil_val: 0.017, et_byte: 17, et_little_endian: 4352, et_betriebsart: ?:et_betriebsart17, et_zeit: (0, 17), et_datum: (0, 17), et_time_domain: (00:00:00, 04:15:00), et_dev_nr: 18, et_err_nr: ERR 17, et_dev_id: 0-17)' [7] 2000FA4E560011
      Rx serial port: 't48072000FA0641003F' length: 19
      Received frame 480 [7] 2000FA0641003F
      Manager ->Write on Unknown_100h TEST_OBJEKT_0 63 (0x003F)
      Rx serial port: 't48072000FA06F70001' length: 19
      Received frame 480 [7] 2000FA06F70001
      Manager ->Write on Unknown_100h TEST_OBJEKT_182 1 (0x0001)
      Rx serial port: 't48072000FA06E80000' length: 19
      Received frame 480 [7] 2000FA06E80000
      Manager ->Write on Unknown_100h TEST_OBJEKT_167 0 (0x0000)
      Rx serial port: 't48072000FA06DBFF38' length: 19
      Received frame 480 [7] 2000FA06DBFF38
      Manager ->Write on Unknown_100h TEST_OBJEKT_154 -200 (0xFF38)
      Rx serial port: 't48072000FA06DCFF38' length: 19
      Received frame 480 [7] 2000FA06DCFF38
      Manager ->Write on Unknown_100h TEST_OBJEKT_155 -200 (0xFF38)
      Rx serial port: 't48072000FA06E10001' length: 19
      Received frame 480 [7] 2000FA06E10001
      Manager ->Write on Unknown_100h TEST_OBJEKT_160 1 (0x0001)
      Rx serial port: 't48072000FA06E2001E' length: 19
      Received frame 480 [7] 2000FA06E2001E
      Manager ->Write on Unknown_100h TEST_OBJEKT_161 30 (0x001E)
      Rx serial port: 't48072000FA06E5028A' length: 19
      Received frame 480 [7] 2000FA06E5028A
      Manager ->Write on Unknown_100h TEST_OBJEKT_164 650 (0x028A)
      Rx serial port: 't48072000FA06E602BC' length: 19
      Received frame 480 [7] 2000FA06E602BC
      Manager ->Write on Unknown_100h TEST_OBJEKT_165 700 (0x02BC)
      Rx serial port: 't48072000FA06E702EE' length: 19
      Received frame 480 [7] 2000FA06E702EE
      Manager ->Write on Unknown_100h TEST_OBJEKT_166 750 (0x02EE)
      Rx serial port: 't51472000FA4E81000F' length: 19
      Received frame 514 [7] 2000FA4E81000F
      Elster CAN frame from 514 ->Write on Unknown_100h with unknown elster index 4E81, with possible data: '15 - (et_default: 15, et_dec_val: 1.5, et_cent_val: 0.15, et_mil_val: 0.015, et_byte: 15, et_little_endian: 3840, et_betriebsart: ?:et_betriebsart15, et_zeit: (0, 15), et_datum: (0, 15), et_time_domain: (00:00:00, 03:45:00), et_dev_nr: 16, et_err_nr: ERR 15, et_dev_id: 0-15)' [7] 2000FA4E81000F
      Rx serial port: 't51472000FA4E7A0000' length: 19
      Received frame 514 [7] 2000FA4E7A0000
      Elster CAN frame from 514 ->Write on Unknown_100h with unknown elster index 4E7A, with possible data: '0 - (et_default: 0, et_dec_val: 0, et_cent_val: 0, et_mil_val: 0, et_byte: 0, et_bool: False, et_little_bool: False, et_little_endian: 0, et_betriebsart: Notbetrieb, et_zeit: (0, 0), et_datum: (0, 0), et_time_domain: (00:00:00, 00:00:00), et_dev_nr: 1, et_err_nr: ERR 0, et_dev_id: 0-0)' [7] 2000FA4E7A0000
      Rx serial port: 't100731000C00000000' length: 19
      Received frame 100 [7] 31000C00000000
      Unknown_100h ->Read on Boiler AUSSENTEMP
      Rx serial port: 't51472000FA4E870000' length: 19
      Received frame 514 [7] 2000FA4E870000
      Elster CAN frame from 514 ->Write on Unknown_100h with unknown elster index 4E87, with possible data: '0 - (et_default: 0, et_dec_val: 0, et_cent_val: 0, et_mil_val: 0, et_byte: 0, et_bool: False, et_little_bool: False, et_little_endian: 0, et_betriebsart: Notbetrieb, et_zeit: (0, 0), et_datum: (0, 0), et_time_domain: (00:00:00, 00:00:00), et_dev_nr: 1, et_err_nr: ERR 0, et_dev_id: 0-0)' [7] 2000FA4E870000
      Rx serial port: 't48072000FA4E591100' length: 19
      Received frame 480 [7] 2000FA4E591100
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E59, with possible data: '4352 - (et_default: 4352, et_dec_val: 435.2, et_cent_val: 43.52, et_mil_val: 4.352, et_byte: 0, et_little_endian: 17, et_betriebsart: ?:et_betriebsart4352, et_zeit: (17, 0), et_datum: (17, 0), et_time_domain: (04:15:00, 00:00:00), et_dev_nr: 4353, et_err_nr: ERR 4352, et_dev_id: 17-0)' [7] 2000FA4E591100
      Rx serial port: 't100731001600000000' length: 19
      Received frame 100 [7] 31001600000000
      Unknown_100h ->Read on Boiler RUECKLAUFISTTEMP
      Rx serial port: 't180760791600EB0000' length: 19
      Received frame 180 [7] 60791600EB0000
      Boiler ->Write on RemoteControl_Broadcast RUECKLAUFISTTEMP 23.5
      Rx serial port: 't4807A0791600EB0000' length: 19
      Received frame 480 [7] A0791600EB0000
      Manager ->Write on HeatingModule_Broadcast RUECKLAUFISTTEMP 23.5
      Rx serial port: 't100731000E00000000' length: 19
      Received frame 100 [7] 31000E00000000
      Unknown_100h ->Read on Boiler SPEICHERISTTEMP
      Rx serial port: 't180722000CFFFD0000' length: 19
      Received frame 180 [7] 22000CFFFD0000
      Boiler ->Respond on Unknown_100h AUSSENTEMP -0.3
      Rx serial port: 't48072000FA4E760F30' length: 19
      Received frame 480 [7] 2000FA4E760F30
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E76, with possible data: '3888 - (et_default: 3888, et_dec_val: 388.8, et_cent_val: 38.88, et_mil_val: 3.888, et_byte: 48, et_little_endian: 12303, et_betriebsart: ?:et_betriebsart3888, et_zeit: (15, 48), et_datum: (15, 48), et_time_domain: (03:45:00, 12:00:00), et_dev_nr: 3889, et_err_nr: ERR 3888, et_dev_id: 15-48)' [7] 2000FA4E760F30
      Rx serial port: 't180722001600EB0000' length: 19
      Received frame 180 [7] 22001600EB0000
      Boiler ->Respond on Unknown_100h RUECKLAUFISTTEMP 23.5
      Rx serial port: 't180722000E01AD0000' length: 19
      Received frame 180 [7] 22000E01AD0000
      Boiler ->Respond on Unknown_100h SPEICHERISTTEMP 42.9
      Rx serial port: 't48072000FA4E7700A0' length: 19
      Received frame 480 [7] 2000FA4E7700A0
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E77, with possible data: '160 - (et_default: 160, et_dec_val: 16, et_cent_val: 1.6, et_mil_val: 0.16, et_byte: -96, et_little_endian: 40960, et_betriebsart: ?:et_betriebsart160, et_zeit: (0, 160), et_datum: (0, 160), et_time_domain: (00:00:00, 1.16:00:00), et_dev_nr: 161, et_err_nr: ERR 160, et_dev_id: 0-160)' [7] 2000FA4E7700A0
      Rx serial port: 't48072000FA4E780010' length: 19
      Received frame 480 [7] 2000FA4E780010
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E78, with possible data: '16 - (et_default: 16, et_dec_val: 1.6, et_cent_val: 0.16, et_mil_val: 0.016, et_byte: 16, et_little_endian: 4096, et_betriebsart: ?:et_betriebsart16, et_zeit: (0, 16), et_datum: (0, 16), et_time_domain: (00:00:00, 04:00:00), et_dev_nr: 17, et_err_nr: Abtauen, et_dev_id: 0-16)' [7] 2000FA4E780010
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F6' length: 19
      Received frame 180 [7] A079FA01D700F6
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.6
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't51472000FA4E7A0008' length: 19
      Received frame 514 [7] 2000FA4E7A0008
      Elster CAN frame from 514 ->Write on Unknown_100h with unknown elster index 4E7A, with possible data: '8 - (et_default: 8, et_dec_val: 0.8, et_cent_val: 0.08, et_mil_val: 0.008, et_byte: 8, et_little_endian: 2048, et_betriebsart: ?:et_betriebsart8, et_zeit: (0, 8), et_datum: (0, 8), et_time_domain: (00:00:00, 02:00:00), et_dev_nr: 9, et_err_nr: Hexschalter, et_dev_id: 0-8)' [7] 2000FA4E7A0008
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807A0005F00000000' length: 19
      Received frame 480 [7] A0005F00000000
      Manager ->Write on HeatingModule SPEICHERBEDARF 0 (0x0000)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F6' length: 19
      Received frame 180 [7] A079FA01D700F6
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.6
      Rx serial port: 't4807A100FA01990000' length: 19
      Received frame 480 [7] A100FA01990000
      Manager ->Read on HeatingModule SOFTWARE_NUMMER
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't50079200FA01990043' length: 19
      Received frame 500 [7] 9200FA01990043
      HeatingModule ->Respond on Manager SOFTWARE_NUMMER 67 (0x0043)
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807A100FA01D60000' length: 19
      Received frame 480 [7] A100FA01D60000
      Manager ->Read on HeatingModule WPVORLAUFIST
      Rx serial port: 't50079200FA01D600FC' length: 19
      Received frame 500 [7] 9200FA01D600FC
      HeatingModule ->Respond on Manager WPVORLAUFIST 25.2
      Rx serial port: 't4807A1001600000000' length: 19
      Received frame 480 [7] A1001600000000
      Manager ->Read on HeatingModule RUECKLAUFISTTEMP
      Rx serial port: 't500792001600CB0000' length: 19
      Received frame 500 [7] 92001600CB0000
      HeatingModule ->Respond on Manager RUECKLAUFISTTEMP 20.3
      Rx serial port: 't4807A100FA07A80000' length: 19
      Received frame 480 [7] A100FA07A80000
      Manager ->Read on HeatingModule VERDICHTER
      Rx serial port: 't4807A000FA02CA00EA' length: 19
      Received frame 480 [7] A000FA02CA00EA
      Manager ->Write on HeatingModule MATERIALNUMMER_HIGH 234 (0x00EA)
      Rx serial port: 't4807A000FA09170000' length: 19
      Received frame 480 [7] A000FA09170000
      Manager ->Write on HeatingModule INTEGRAL_REGELABWEICHUNG_RELATIV 0 (0x0000)
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't50079200FA07A80008' length: 19
      Received frame 500 [7] 9200FA07A80008
      HeatingModule ->Respond on Manager VERDICHTER 0.8
      Rx serial port: 't100731000C00000000' length: 19
      Received frame 100 [7] 31000C00000000
      Unknown_100h ->Read on Boiler AUSSENTEMP
      Rx serial port: 't180722000CFFFD0000' length: 19
      Received frame 180 [7] 22000CFFFD0000
      Boiler ->Respond on Unknown_100h AUSSENTEMP -0.3
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't100731001600000000' length: 19
      Received frame 100 [7] 31001600000000
      Unknown_100h ->Read on Boiler RUECKLAUFISTTEMP
      Rx serial port: 't180722001600EA0000' length: 19
      Received frame 180 [7] 22001600EA0000
      Boiler ->Respond on Unknown_100h RUECKLAUFISTTEMP 23.4
      Rx serial port: 't100731000E00000000' length: 19
      Received frame 100 [7] 31000E00000000
      Unknown_100h ->Read on Boiler SPEICHERISTTEMP
      Rx serial port: 't180722000E01AD0000' length: 19
      Received frame 180 [7] 22000E01AD0000
      Boiler ->Respond on Unknown_100h SPEICHERISTTEMP 42.9
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F6' length: 19
      Received frame 180 [7] A079FA01D700F6
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.6
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't4807A0005F00000000' length: 19
      Received frame 480 [7] A0005F00000000
      Manager ->Write on HeatingModule SPEICHERBEDARF 0 (0x0000)
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F6' length: 19
      Received frame 180 [7] A079FA01D700F6
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.6
      Rx serial port: 't4807A100FA01990000' length: 19
      Received frame 480 [7] A100FA01990000
      Manager ->Read on HeatingModule SOFTWARE_NUMMER
      Rx serial port: 't4807E100FAFE4C0000' length: 19
      Received frame 480 [7] E100FAFE4C0000
      Elster CAN frame from Manager ->Read on ExternalDevice without elster index. [7] E100FAFE4C0000
      Rx serial port: 't50079200FA01990043' length: 19
      Received frame 500 [7] 9200FA01990043
      HeatingModule ->Respond on Manager SOFTWARE_NUMMER 67 (0x0043)
      Rx serial port: 't4807A100FA01D60000' length: 19
      Received frame 480 [7] A100FA01D60000
      Manager ->Read on HeatingModule WPVORLAUFIST
      Rx serial port: 't4807A1001600000000' length: 19
      Received frame 480 [7] A1001600000000
      Manager ->Read on HeatingModule RUECKLAUFISTTEMP
      Rx serial port: 't50079200FA01D600FB' length: 19
      Received frame 500 [7] 9200FA01D600FB
      HeatingModule ->Respond on Manager WPVORLAUFIST 25.1
      Rx serial port: 't4807A100FA07A80000' length: 19
      Received frame 480 [7] A100FA07A80000
      Manager ->Read on HeatingModule VERDICHTER
      Rx serial port: 't4807A000FA02CA00E9' length: 19
      Received frame 480 [7] A000FA02CA00E9
      Manager ->Write on HeatingModule MATERIALNUMMER_HIGH 233 (0x00E9)
      Rx serial port: 't4807A000FA09170000' length: 19
      Received frame 480 [7] A000FA09170000
      Manager ->Write on HeatingModule INTEGRAL_REGELABWEICHUNG_RELATIV 0 (0x0000)
      Rx serial port: 't48072000FA4E5E0000' length: 19
      Received frame 480 [7] 2000FA4E5E0000
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E5E, with possible data: '0 - (et_default: 0, et_dec_val: 0, et_cent_val: 0, et_mil_val: 0, et_byte: 0, et_bool: False, et_little_bool: False, et_little_endian: 0, et_betriebsart: Notbetrieb, et_zeit: (0, 0), et_datum: (0, 0), et_time_domain: (00:00:00, 00:00:00), et_dev_nr: 1, et_err_nr: ERR 0, et_dev_id: 0-0)' [7] 2000FA4E5E0000
      Rx serial port: 't500792001600CB0000' length: 19
      Received frame 500 [7] 92001600CB0000
      HeatingModule ->Respond on Manager RUECKLAUFISTTEMP 20.3
      Rx serial port: 't48072000FA4E7E0000' length: 19
      Received frame 480 [7] 2000FA4E7E0000
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E7E, with possible data: '0 - (et_default: 0, et_dec_val: 0, et_cent_val: 0, et_mil_val: 0, et_byte: 0, et_bool: False, et_little_bool: False, et_little_endian: 0, et_betriebsart: Notbetrieb, et_zeit: (0, 0), et_datum: (0, 0), et_time_domain: (00:00:00, 00:00:00), et_dev_nr: 1, et_err_nr: ERR 0, et_dev_id: 0-0)' [7] 2000FA4E7E0000
      Rx serial port: 't50079200FA07A80008' length: 19
      Received frame 500 [7] 9200FA07A80008
      HeatingModule ->Respond on Manager VERDICHTER 0.8
      Rx serial port: 't48072000FA4E6A0000' length: 19
      Received frame 480 [7] 2000FA4E6A0000
      Elster CAN frame from Manager ->Write on Unknown_100h with unknown elster index 4E6A, with possible data: '0 - (et_default: 0, et_dec_val: 0, et_cent_val: 0, et_mil_val: 0, et_byte: 0, et_bool: False, et_little_bool: False, et_little_endian: 0, et_betriebsart: Notbetrieb, et_zeit: (0, 0), et_datum: (0, 0), et_time_domain: (00:00:00, 00:00:00), et_dev_nr: 1, et_err_nr: ERR 0, et_dev_id: 0-0)' [7] 2000FA4E6A0000
      Rx serial port: 't48072000FA06920000' length: 19
      Received frame 480 [7] 2000FA06920000
      Manager ->Write on Unknown_100h TEST_OBJEKT_81 0 (0x0000)
      Rx serial port: 't4807A079FA01262A00' length: 19
      Received frame 480 [7] A079FA01262A00
      Manager ->Write on HeatingModule_Broadcast MINUTE 42
      Rx serial port: 't48072000FA01262A00' length: 19
      Received frame 480 [7] 2000FA01262A00
      Manager ->Write on Unknown_100h MINUTE 42
      Rx serial port: 't100731000C00000000' length: 19
      Received frame 100 [7] 31000C00000000
      Unknown_100h ->Read on Boiler AUSSENTEMP
      Rx serial port: 't180722000CFFFD0000' length: 19
      Received frame 180 [7] 22000CFFFD0000
      Boiler ->Respond on Unknown_100h AUSSENTEMP -0.3
      Rx serial port: 't100731001600000000' length: 19
      Received frame 100 [7] 31001600000000
      Unknown_100h ->Read on Boiler RUECKLAUFISTTEMP
      Rx serial port: 't180722001600E90000' length: 19
      Received frame 180 [7] 22001600E90000
      Boiler ->Respond on Unknown_100h RUECKLAUFISTTEMP 23.3
      Rx serial port: 't10079000FA06AF0001' length: 19
      Received frame 100 [7] 9000FA06AF0001
      Unknown_100h ->Write on Manager TEST_OBJEKT_110 1 (0x0001)
      Rx serial port: 't100731000E00000000' length: 19
      Received frame 100 [7] 31000E00000000
      Unknown_100h ->Read on Boiler SPEICHERISTTEMP
      Rx serial port: 't180722000E01AD0000' length: 19
      Received frame 180 [7] 22000E01AD0000
      Boiler ->Respond on Unknown_100h SPEICHERISTTEMP 42.9
      Rx serial port: 't1807A0790CFFFD0000' length: 19
      Received frame 180 [7] A0790CFFFD0000
      Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
      Rx serial port: 't1807A079FA01D700F6' length: 19
      Received frame 180 [7] A079FA01D700F6
      Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.6
c

To send text to the target process's standard input, enter text into the Debug Console's evaluation box while the target process is running. See https://aka.ms/VSCode-CS-LaunchJson-Console for more information.
      Input: C
      Tx serial port: 'C\r' length: 2
      Rx serial port: '' length: 0
      Response to 'C\r': OK
      Input: 10

´´´