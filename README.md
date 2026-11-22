# Przewodnik Rodzica — Asystent Świadczeń Rodzicielskich ZUS

Deterministyczny kalkulator świadczeń ZUS oraz analizator dokumentacji (C# / .NET + React + Azure AI Document Intelligence), który zamienia wielostronicowe teczki i zawiłe przepisy Kodeksu pracy oraz ustawy zasiłkowej w przejrzysty harmonogram urlopów, dokładne wyliczenie kwot i listę wymaganych formalności.

> **Demo online (hosting na Azure App Service):** `[https://przewodnik-rodzica-demo2026.azurewebsites.net]`  
> **Repozytorium:** `[https://github.com/Fyszu/PrzewodnikRodzica]`  

---

## Problem i założenie architektoniczne

Ustalanie uprawnień do zasiłku macierzyńskiego, ojcowskiego, rodzicielskiego i chorobowego w ciąży wymaga jednoczesnego uwzględnienia kilkunastu zmiennych (tytułu ubezpieczenia, wielkości płatnika, korekt Z-3, ciągłości podstawy z art. 43, przepisów o wcześniakach z art. 180² KP czy automatycznego wyrejestrowania przedsiębiorcy z dobrowolnego chorobowego).

Zastosowanie czystego modelu językowego (LLM) do obliczeń finansowych i interpretacji prawa niesie ryzyko halucynacji. Dlatego **Przewodnik Rodzica** rozdziela te warstwy:

1. **AI tylko tam, gdzie jest potrzebne:** `Azure AI Document Intelligence` odpowiada wyłącznie za OCR, rozpoznawanie tabel wynagrodzeń i pól wyboru (`☑`/`☐`) z plików PDF.
2. **100% deterministyczny silnik obliczeniowo-prawny (`BenefitEngine` w C#):** twarde reguły ustawowe, obliczane przez deterministyczne algorytmy.
3. **Human-in-the-Loop:** gdy teczka nie zawiera kwot (np. podstawa JDG w ZUS DRA, pusta tabela Z-3 przy kontynuacji z art. 43) lub skan jest nieczytelny, system nie zmyśla danych — raportuje lukę wprost i jednym kliknięciem przenosi sprawę do edytowalnego kalkulatora.

---

## Technologie i biblioteki

- **Backend & Silnik domenowy:** C# / .NET, ASP.NET Core Web API, xUnit, PdfPig
- **Frontend:** React, TypeScript, Vite, Tailwind CSS, Lucide Icons
- **OCR / Ekstrakcja strukturalna:** Azure AI Document Intelligence
- **Hosting:** Azure App Service
- **Cache:** SQLite, przechowywanie PDFów w SHA-256

---

## Architektura rozwiązania

```text
[Frontend: React + TypeScript + Tailwind]
        │
        ├──► Kalkulator interaktywny ─────────────────────────┐
        │                                                     ▼
        └──► Upload PDF ──► [API .NET]               [BenefitEngine (C#)]
                                │                    • podstawa wymiaru (13,71%)
                      SHA-256 (klucz cache)          • wyłączenia art. 38 ust. 2
                                │                    • harmonogram i stawki %
                   ┌────────────┴────────────┐       • alerty ryzyka i dokumenty
                   ▼                         ▼
       [Cache OCR (SQLite)]       [Azure AI Document Intelligence]
                   └────────────┬────────────┘
                                ▼
                   [ZusDocumentParser (C#)]
                   • wykrywanie formularzy (Z-3, Z-3b, ZAM, ZUR...)
                   • parsowanie tabel HTML/Markdown i korekt Z-3
                   • mapowanie na CalculationRequest
