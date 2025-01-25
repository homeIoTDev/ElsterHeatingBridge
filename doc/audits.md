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
