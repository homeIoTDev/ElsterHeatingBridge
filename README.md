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
<img src="doc/testsetup.png" width="600">

## Lizenz
-------

Dieses Programm ist freie Software; Sie können es unter den Bedingungen der GNU General Public License, wie von der Free Software Foundation veröffentlicht, weitergeben und/oder modifizieren; entweder gemäß Version 2 der Lizenz oder (nach Ihrer Wahl) jeder späteren Version.

Die GNU General Public License ist unter <http://www.gnu.org/copyleft/gpl.html> erhältlich. Eine Kopie befindet sich in der Datei GPL.txt, wichtige Hinweise zur Lizenz vom Autor sind in der Datei LICENSE.txt enthalten, die mit diesen Skripten verteilt wird.

Dieses Skript wird in der Hoffnung verteilt, dass es nützlich sein wird, aber OHNE JEDE GEWÄHRLEISTUNG; ohne die implizite Gewährleistung der MARKTGÄNGIGKEIT oder EIGNUNG FÜR EINEN BESTIMMTEN ZWECK. Siehe die GNU General Public License für weitere Details.

## Quellen
----------

Dieses Skript basiert auf der Arbeit von:

* Jürg <http://juerg5524.ch/>
* Immi (THZ-Modul)
* Radiator

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
