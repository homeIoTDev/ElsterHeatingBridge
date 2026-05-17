---
name: language-rules
description: Sprachregeln für das gesamte Projekt
invokable: true
---

## Sprachregeln

### 1. Dokumentation (`.md` Dateien)
- **Sprache**: Deutsch
- **Zielgruppe**: Deutsche Entwickler und Anwender
- **Ausnahme**: Fachbegriffe bleiben auf Englisch

### 2. Code-Kommentare
- **Sprache**: Deutsch
- **Ziel**: Verständlichkeit für das Team
- **Beispiele**:
  ```python
  # Berechnet die Heizleistung basierend auf der Außentemperatur
  def calculate_heating_power(temp):
  ```

### 3. Log-Nachrichten
- **Sprache**: Englisch
- **Ziel**: Einheitlichkeit mit Logging-Frameworks
- **Beispiele**:
  ```python
  logging.info("Heating circuit %s activated", circuit_id)
  logger.error("Temperature sensor %s not responding", sensor_id)
  ```

### 4. Variablen-/Funktionsnamen
- **Sprache**: Englisch (Programmierkonvention)
- **Begründung**: Konsistenz mit Programmiersprachen-Syntax

### 5. Commit-Nachrichten
- **Sprache**: Deutsch
- **Ausnahme**: Internationale Teams → Englisch

### Struktur-Hinweise
- `#` für Code-Kommentare
- `//` für Code-Kommentare
- `"""` oder `'''` für Docstrings: Deutsch
- Log-Level: `debug`, `info`, `warning`, `error` bleiben englisch