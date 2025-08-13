## Untersuchungen 22.12.24 - Setzen der Heizkurve auf 0.2 von der FEK
Eine Anpassung an der FEK (RemoteControl) für HK1 zeigt keinerlei Kommunikation auf dem Bus. Dies legt die Vermutung nahe, dass die FEK die vollständige Steuerung des HK1 übernimmt. Konkret wurde die Heizkurve auf 0.2 festgelegt. Da entweder der WPM die Steuerung des HKs übernimmt und nach der Installation der FEK alle Einstellungen bezüglich der Heizkurve aus diesem verschwunden sind, verwaltet und steuert nun wahrscheinlich die FEK alle Parameter und sendet nur die Ergebnisse an die anderen Module. Es ist fraglich, ob es überhaupt möglich ist, die Werte der FEK bezüglich der Heizkurve auszulesen oder diese extern zu schreiben. Letzteres wäre nur möglich, wenn mehrere FEKs erlaubt sind. 

Schlussfolgerung: Zunächst muss das Abfragen und Beschreiben des Busses umgesetzt und ein Scan auf die FEK durchgeführt werden.
[20241222_Heizkurve.log](audits/20241222_Heizkurve.log)


## Untersuchungen 30.12.24 - Neustart der Steuerung (stromlos)
Eigentlich sollte die Heizung auf der FEK in den Ferienbetrieb wechseln, tut sie aber nicht. Test, ob sie nach Neustart tut. Dabei wurde der Neustart aufgezeichnet.

Ergebnis: Die Heizung wechselt nicht in den Ferienbetrieb. Das Koffersymbol fehlt in der Anzeige der FEK. Es ist nicht ausgeschlossen, dass es sich um einen Bug der Tecalor TTL 10 AC handelt. Auch in den Ferientagen hat sich der Ferienbetrieb nicht eingeschaltet. Es könnte auch sein, dass die FEK die Einstellungen am WPM ignoriert, da sie die Steuerung der Heizung übernimmt.
[20241230_NeustartWPM.log](audits/20241230_NeustartWPM.log)

## Untersuchung 05.01.25 - Scan der gültigen CAN-IDs

In meiner Anlage gibt es ein Modul mit der CanId = 0x100. Dieses Modul konnte ich bisher in keiner
Implementierung im Internet finden. Laut dem Display meiner Wärmepumpe handelt es sich um ein FES.
Auf der Homepage von Stiebel Eltron wird ein FES Komfort als Zubehör angeboten, was darauf hindeutet,
dass es sich um mein neues Bedienteil mit Touchwheel handelt.

Warum musste ich das FEK für die Kühlung installieren, und warum wurde das FES im Keller montiert
und nicht in der Wohnung, obwohl das FES auch die Feuchtemessung durchführen kann?

So werden die Busteilnehmer laut WPM3-Display dargestellt:
| # |Busteilnehmer   |Software   |
|---|---|---|
|01.   |WPM3   |390-02   |
|02.   |FES   |392-03   |
|03.   |FEK   |195-06   |
|04.   |WP1   |243-10   |   


```
      scan on CAN-id: 700
      list of valid can id's:
      
        000 (C306 = 195-06)
        100 (8000 = 128-00)
        180 (8000 = 128-00)
        301 (C306 = 195-06)
        302 (8000 = 128-00)
        480 (8000 = 128-00)
        500 (4310 = 67-16)
        601 (8000 = 128-00)
        602 (8000 = 128-00)
        680 (8000 = 128-00)
```

## Untersuchung 17.01.25 - PROGRAMMSCHALTER/Betriebsart zwischen FEK und FES
Bei der Übergabe von der Betriebsart zwischen FEK und FES gibt es ein Problem: Wenn an der FEK eine Betriebsart ausgewählt wird, 
wird dies nicht an die FES oder an andere Busteilnehmer weitergegeben. Dazu eine Beobachtung:
- An der FEK wird eine Betriebsart ausgewählt, aber kein Telegram wird an die FES oder an einen andere Busteilnehmer gesendet.
- An der FES wird eine Betriebsart ausgewählt, dann wird das Telegram von der FES an die anderen Busteilnehmer gesendet, aber nicht an das FEK.
Es ist also möglich, dass am FEK und FES unterschiedliche Betriebsarteinstellungen vorliegen, was definitiv nicht so sein sollte.

Es ist jedoch festzustellen, dass die Heizung tatsächlich das Programm von der FEK verwendet.

## 01.02.2025 Sammlung über 10h aller passiven Telegramme

```
       38861x  RemoteControl ->Write on ComfortSoft FEHLERMELDUNG 20805 (0x5145)
        6924x  Boiler ->Write on HeatingModule_Broadcast AUSSENTEMP -0.3
        6741x  Boiler ->Write on HeatingModule_Broadcast HILFSKESSELSOLL 24.1
        3741x  HeatingModule ->Respond on Manager SOFTWARE_NUMMER 67 (0x0043)
        3709x  Boiler ->Respond on FES_COMFORT AUSSENTEMP -0.3
        3702x  Boiler ->Respond on FES_COMFORT RUECKLAUFISTTEMP 23.7
        3691x  Boiler ->Respond on FES_COMFORT SPEICHERISTTEMP 28.1
        3651x  Manager ->Write on HeatingModule SPEICHERBEDARF 512 (0x0200)
        3502x  HeatingModule ->Respond on Manager WPVORLAUFIST 40.4
        3502x  HeatingModule ->Respond on Manager RUECKLAUFISTTEMP 32.4
        3320x  Manager ->Write on HeatingModule MATERIALNUMMER_HIGH 237 (0x00ED)
        3319x  Manager ->Write on HeatingModule INTEGRAL_REGELABWEICHUNG_RELATIV 0 (0x0000)
        3319x  HeatingModule ->Respond on Manager VERDICHTER 55.3
        3200x  FES_COMFORT ->Respond on ExternalDevice PROGRAMMSCHALTER Absenkbetrieb
        2488x  RemoteControl ->Respond on ExternalDevice PROGRAMMSCHALTER Tagbetrieb
        1474x  Boiler ->Write on RemoteControl_Broadcast MAX_HYSTERESE 0
        997x  RemoteControl ->Write on Mixer FEUCHTE 40.8
        921x  RemoteControl ->Write on Mixer RAUMISTTEMP 20.4
        827x  Manager ->Write on HeatingModule_Broadcast MINUTE 7
        644x  Manager ->Write on FES_COMFORT MINUTE 7
        644x  FES_COMFORT ->Write on Manager TEST_OBJEKT_110 1 (0x0001)
        644x  Boiler ->Respond on RemoteControl GERAETE_ID 128-00
        585x  Manager ->Write on FES_COMFORT TEST_OBJEKT_81 1 (0x0001)
        549x  HeatingModule ->Write on Manager VERDAMPFERTEMP -124
        461x  Mixer ->Write on RemoteControl VORLAUFISTTEMP -40.0
        461x  Mixer ->Write on RemoteControl VORLAUFSOLLTEMP -40.0
        349x  Boiler ->Write on RemoteControl_Broadcast RUECKLAUFISTTEMP 23.6
        341x  Boiler ->Write on RemoteControl_Broadcast PUMPENSTATUS 20482
        339x  Boiler ->Write on RemoteControl_Broadcast SPEICHERISTTEMP 27.8
        288x  Boiler ->Write on RemoteControl_Broadcast SAMMEL_RELAISSTATUS 1 (0x0001)
        279x  Boiler ->Write on RemoteControl_Broadcast TEILVORRANG_WW -32768 (0x8000)
        279x  Boiler ->Write on RemoteControl_Broadcast HZK_PUMPE 256 (0x0100)
        278x  RemoteControl ->Write on Mixer VORLAUFSOLLTEMP 24.1
        277x  RemoteControl ->Write on Boiler KESSELSOLLTEMP 24.1
        276x  RemoteControl ->RespondSystem on Boiler INITIALISIERUNG 1
        276x  RemoteControl ->RespondSystem on Mixer INITIALISIERUNG 1
        276x  Mixer ->System on RemoteControl INITIALISIERUNG 1
        276x  RemoteControl ->Write on Mixer MISCHER_ZU 2
        276x  RemoteControl ->Write on Mixer MAX_TEMP_HZK 256.0
        276x  RemoteControl ->Write on Mixer KP 7680 (0x1E00)
        276x  RemoteControl ->Write on Mixer RAUMEINFLUSS 1280 (0x0500)
        276x  RemoteControl ->Write on Mixer VERSTELLTE_RAUMSOLLTEMP 20.2
        276x  Mixer ->Respond on RemoteControl RAUMISTTEMP -40.0
        276x  Mixer ->Respond on RemoteControl VERSTELLTE_RAUMSOLLTEMP -40.0
        276x  Boiler ->Write on RemoteControl_Broadcast AUSSENTEMP -0.3
        276x  Boiler ->Write on RemoteControl_Broadcast SAMMLERISTTEMP -40.0
        276x  Boiler ->Write on RemoteControl_Broadcast GERAETEKONFIGURATION 49 (0x0031)
        276x  Boiler ->Write on RemoteControl_Broadcast SPEICHER_STATUS 2
        276x  Boiler ->Write on RemoteControl_Broadcast MULTIFUNKTION_ISTTEMP 0.0
        276x  Boiler ->Write on RemoteControl_Broadcast FEHLERMELDUNG 0 (0x0000)
        276x  Boiler ->Write on RemoteControl_Broadcast UHRZEIT 11:03
        276x  Boiler ->Write on RemoteControl_Broadcast DATUM 01.02.
        276x  Boiler ->Write on RemoteControl_Broadcast AUSSEN_FROSTTEMP 5.1
        276x  Boiler ->Write on RemoteControl_Broadcast SCHALTFKT_QUELLE_IWS 0 (0x0000)
        276x  Boiler ->Write on RemoteControl_Broadcast SOFTWARE_NUMMER 331 (0x014B)
        275x  RemoteControl ->Write on Boiler MISCHER_ZU 2
        275x  RemoteControl ->Write on Boiler HEIZKREIS_STATUS_PROGSTELL 512 (0x0200)
        256x  Manager ->Write on HeatingModule_Broadcast RUECKLAUFISTTEMP 23.6
        233x  Mixer ->Write on RemoteControl BRENNER 1
        207x  HeatingModule ->Write on Manager BETRIEBSART_WP 4 (0x0004)
        201x  HeatingModule ->Write on Manager ABTAUUNGAKTIV 512 (0x0200)
        194x  Manager ->Write on HeatingModule_Broadcast STUNDE 11
        194x  HeatingModule ->Respond on Manager LZ_VERD_1_HEIZBETRIEB -32.768
        194x  HeatingModule ->Respond on Manager LZ_VERD_2_HEIZBETRIEB 8.588
        194x  HeatingModule ->Respond on Manager LZ_VERD_1_2_HEIZBETRIEB 0.000
        194x  HeatingModule ->Respond on Manager LZ_VERD_1_WW_BETRIEB -32768 (0x8000)
        194x  HeatingModule ->Respond on Manager LZ_VERD_2_WW_BETRIEB 3331 (0x0D03)
        194x  HeatingModule ->Respond on Manager LZ_VERD_1_2_WW_BETRIEB 0 (0x0000)
        194x  HeatingModule ->Respond on Manager LZ_VERD_1_KUEHLBETRIEB -32.768
        194x  HeatingModule ->Respond on Manager LZ_VERD_2_KUEHLBETRIEB 0.092
        194x  HeatingModule ->Respond on Manager LZ_VERD_1_2_KUEHLBETRIEB 0.000
        189x  Manager ->Write on HeatingModule TEILVORRANG_WW 256 (0x0100)
        189x  HeatingModule ->Write on Manager SCHALTFKT_IWS 0 (0x0000)
        185x  Mixer ->Write on RemoteControl FEHLERMELDUNG 0 (0x0000)
        185x  Mixer ->Write on RemoteControl SPEICHER_STATUS 2
        185x  Mixer ->Write on RemoteControl DCF 0
        185x  Mixer ->Write on RemoteControl MISCHER_AUF 0
        185x  Mixer ->Write on RemoteControl MAX_TEMP_KESSEL 35.0
        184x  Boiler ->RespondSystem on RemoteControl INITIALISIERUNG 1
        184x  Manager ->RespondSystem on FES_COMFORT INITIALISIERUNG 1
        184x  FES_COMFORT ->System on Manager INITIALISIERUNG 1
        184x  Manager ->Write on FES_COMFORT SOLAR_KOLLEKTOR_3_I_ANTEIL 0 (0x0000)
        184x  Manager ->Write on FES_COMFORT FATAL_ERROR 0 (0x0000)
        184x  HeatingModule ->Respond on Manager TEST_OBJEKT_215 100 (0x0064)
        183x  HeatingModule ->RespondSystem on Manager INITIALISIERUNG 1
        183x  Manager ->Write on HeatingModule_Broadcast ACCESS_EEPROM 1
        183x  Manager ->Write on HeatingModule_Broadcast GERAETEKONFIGURATION 0 (0x0000)
        183x  Manager ->Write on HeatingModule_Broadcast ANFAHRENT 0 (0x0000)
        183x  Manager ->Write on HeatingModule_Broadcast SAMMEL_RELAISSTATUS 0 (0x0000)
        183x  Manager ->Write on HeatingModule_Broadcast PROGRAMMSCHALTER Absenkbetrieb
        183x  Manager ->Write on HeatingModule_Broadcast ZWISCHENEINSPRITZUNG_ISTTEMP 0 (0x0000)
        183x  Manager ->Write on HeatingModule_Broadcast LUEFT_PASSIVKUEHLUNG_UEBER_FORTLUEFTER 0 (0x0000)
        183x  Manager ->Write on HeatingModule_Broadcast TAG 1
        183x  Manager ->Write on HeatingModule_Broadcast MONAT 2
        183x  Manager ->Write on HeatingModule_Broadcast JAHR 25
        183x  Manager ->Write on HeatingModule QUELLENPUMPEN_STATUS 0 (0x0000)
        183x  Manager ->Write on HeatingModule TEMPORALE_LUEFTUNGSSTUFE_DAUER 0 (0x0000)
        183x  Manager ->Write on HeatingModule GERAETE_ID 128-00
        183x  Manager ->Write on HeatingModule SOFTWARE_NUMMER 331 (0x014B)
        183x  Manager ->Write on HeatingModule SOFTWARE_VERSION 2 (0x0002)
        183x  HeatingModule ->Respond on Manager HARDWARE_NUMMER -32768 (0x8000)
        183x  HeatingModule ->Write on Manager FEHLERMELDUNG 0 (0x0000)
        183x  HeatingModule ->Write on Manager SOLAR_KOLLEKTOR_3_I_ANTEIL 0 (0x0000)
        183x  HeatingModule ->Write on Manager GERAETE_ID 67-16
        183x  HeatingModule ->Write on Manager SPEICHER_STATUS -127
        183x  HeatingModule ->Write on Manager STUETZSTELLE_ND1 100 (0x0064)
        183x  HeatingModule ->Write on Manager STUETZSTELLE_ND2 1700 (0x06A4)
        183x  HeatingModule ->Write on Manager STUETZSTELLE_HD1 100 (0x0064)
        183x  HeatingModule ->Write on Manager STUETZSTELLE_HD2 3100 (0x0C1C)
        183x  HeatingModule ->Write on Manager FATAL_ERROR 0 (0x0000)
        183x  HeatingModule ->Write on Manager K_OS_RMX_RESERVE_INFO3 1 (0x0001)
        183x  HeatingModule ->Write on Manager FEHLER_PARAMETERSATZ_IWS 0 (0x0000)
        183x  HeatingModule ->Respond on Manager SOFTWARE_VERSION 16 (0x0010)
        160x  RemoteControl ->RespondSystem on RemoteControl BUSKONFIGURATION 256 (0x0100)
        92x  Boiler ->System on RemoteControl_Broadcast INITIALISIERUNG 1
        92x  Manager ->System on HeatingModule_Broadcast INITIALISIERUNG 1
        92x  Boiler ->System on Mixer3 INITIALISIERUNG 1
        91x  Manager ->RespondSystem on HeatingModule INITIALISIERUNG 1
        64x  Boiler ->Respond on FES_COMFORT SOFTWARE_VERSION 2 (0x0002)
        11x  Manager ->Write on FES_COMFORT STUNDE 11
        3x  Manager ->Write on HeatingModule LAUFZEIT_VERD_BEI_SPEICHERBEDARF 1 (0x0001)
        2x  HeatingModule ->Write on Manager ANFORDERUNG_LEISTUNGSZWANG 0 (0x0000)
```
[20250201_PassivElsterTelegrams.log](audits/20250201_PassivElsterTelegrams.log)


## 01.02.2025 Einstellung der Uhzzeit und Datum 

An der RemoteControl(FEK) gibt es keine Möglichkeit, die Uhrzeit einzustellen, nur am FES. Von dort werden an den Manager die einzelnen Werte geschrieben, wenn man sie am Display eisntellt. Der Boiler überträgt dann an die Remotcontrols per Broadcast und der Manager zuletzt auch an die FES. Somit kann man die Uhrzeit wohl auch von woanders setzen:

```
FES_COMFORT ->Write on Manager TAG 2
Boiler ->Write on RemoteControl_Broadcast DATUM 02.02.
Boiler ->Write on RemoteControl_Broadcast UHRZEIT 11:24
Manager ->Write on HeatingModule_Broadcast TAG 2
Manager ->Write on FES_COMFORT TAG 2
```

Der Test mit `--can_scan=Manager.MINUTE.1b00` klappt nicht.
Test mit `--can_scan=FES_COMFORT Manager.MINUTE.1e00` funktioniert!!!! Somit lassen sich Uhrzeit und Datum setzen, indem man sich als FES ausgibt und an den Manager schreibt

## 01.02.2025 Einstellung für PROGRAMMSCHALTER

Start der Anlage macht folgendes:
```
Manager ->Write on HeatingModule_Broadcast PROGRAMMSCHALTER Absenkbetrieb
Manager ->Write on FES_COMFORT PROGRAMMSCHALTER Absenkbetrieb
FES_COMFORT ->Read on Boiler PROGRAMMSCHALTER
Boiler ->Respond on FES_COMFORT PROGRAMMSCHALTER Absenkbetrieb
Händische bedienung am FES:
FES_COMFORT ->Write on Boiler PROGRAMMSCHALTER Tagbetrieb
Manager ->Write on FES_COMFORT PROGRAMMSCHALTER Tagbetrieb
Manager ->Write on HeatingModule_Broadcast PROGRAMMSCHALTER Tagbetrieb
FES_COMFORT ->Read on Boiler PROGRAMMSCHALTER
Boiler ->Respond on FES_COMFORT PROGRAMMSCHALTER Tagbetrieb

Händiche Bedienung am FEK:
 =>NICHTS

```

## 16.02.2025 Warmwasserparameter können nur von der FES gelesen werden
Der Test mit `--can_scan=FES_COMFORT Boiler.EINSTELL_SPEICHERSOLLTEMP2` zeigt den Wert, der auch an der FES angezeigt wird bzgl.
der ECO Solltemperatur von Warmwasser. Hingegen hat ein Scan auf Boiler ausgehend vom ExternalDevice keinen einzigen Wert zu Tage gefördert.

Erkenntnis: Welches Modul die Abfrage macht ist wichtig. Es kann nicht für alle Werte ExternalDevice genutzt werden!

[FES_Boiler2.log](audits/FES_Boiler2.log)

## 21.02.2025 Warmwasserparameter können auch vom Modul ComfortSoft gelesen werden!
Ein Test mit `./HeatingMqttService --Logging:LogLevel:Default=Information --can_scan="ComfortSoft Boiler.SPEICHERISTTEMP"` zeigt den Wert korrekt an, der zuvor über FES_COMFORT abgefragt wurde und zu einem Fehler führte.

Erkenntnis: Anstelle von FES sollte ComfortSoft verwendet werden, da das Auslesen über FES zu einem Fehler im FES führte (Anzeige: WP ERR oder ähnlich). Dies kann nur durch Abschalten der Stromversorgung von WPM und WP behoben werden. Dabei ist die Reihenfolge wichtig: Zuerst WPM3 wieder mit Strom versorgen, dann WP, wie es in der Bedienungsanleitung der Wärmepumpe beschrieben ist.

