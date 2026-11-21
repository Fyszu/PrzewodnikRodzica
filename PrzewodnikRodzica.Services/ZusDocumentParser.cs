using System.Globalization;
using System.Text.RegularExpressions;
using PrzewodnikRodzica.Core.Models;

namespace PrzewodnikRodzica.Core.Services;

public class ParsedDocumentResult
{
    public List<string> DetectedForms { get; set; } = new();
    public bool HasConfirmedInsurance { get; set; }
    public string InsuranceSummary { get; set; } = string.Empty;
    public List<string> ExtractedFacts { get; set; } = new();
    public CalculationRequest MappedRequest { get; set; } = new();
}

public class ZusDocumentParser
{
    private static readonly CultureInfo PLCulture = new("pl-PL");

    public ParsedDocumentResult ParseExtractedText(string rawText)
    {
        var result = new ParsedDocumentResult();
        var req = new CalculationRequest();
        string normalized = rawText.Replace("\r\n", "\n");

        // 1. Rozpoznanie formularzy w teczce
        if (Regex.IsMatch(normalized, @"\bZ-3\b|Zaświadczenie płatnika składek", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("Z-3 (Zaświadczenie płatnika składek — pracownik)");
        if (Regex.IsMatch(normalized, @"\bZ-3a\b", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("Z-3a (Zaświadczenie płatnika — zleceniobiorca)");
        if (Regex.IsMatch(normalized, @"\bZAM\b|Wniosek\s+o\s+zasiłek\s+macierzyński", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("ZAM (Wniosek o zasiłek macierzyński)");
        if (Regex.IsMatch(normalized, @"\bZAS-26\b|przyjęciu na wychowanie", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("ZAS-26 (Przyjęcie dziecka na wychowanie / przysposobienie)");
        if (Regex.IsMatch(normalized, @"ZUS\s*ZLA|e-?ZLA|Niezdolność do pracy", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("e-ZLA (Zwolnienie lekarskie)");
        if (Regex.IsMatch(normalized, @"\bPOG\b|PISMO OGÓLNE", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("POG (Pismo ogólne do ZUS)");

        // 2. Weryfikacja tytułu ubezpieczenia
        bool hasEmployeeCode = Regex.IsMatch(normalized, @"0110\s*00|1011000|pracownik podlegający|Jest zatrudniony", RegexOptions.IgnoreCase);
        bool hasZ3a = result.DetectedForms.Any(f => f.StartsWith("Z-3a"));

        if (hasEmployeeCode)
        {
            req.EmploymentTitle = EmploymentTitle.Employee;
            result.HasConfirmedInsurance = true;
            result.InsuranceSummary = "Potwierdzono obowiązkowe ubezpieczenie chorobowe z tytułu umowy o pracę (kod 0110 / druk Z-3).";
        }
        else if (hasZ3a)
        {
            req.EmploymentTitle = EmploymentTitle.MandateContract;
            req.HasVoluntarySicknessInsurance = true;
            result.HasConfirmedInsurance = true;
            result.InsuranceSummary = "Wykryto druk Z-3a (umowa zlecenie) — weryfikuj ciągłość 90 dni dobrowolnego ubezpieczenia chorobowego.";
        }
        else
        {
            result.HasConfirmedInsurance = false;
            result.InsuranceSummary = "Nie wykryto jednoznacznego potwierdzenia tytułu ubezpieczenia chorobowego — uzupełnij ręcznie.";
        }

        // 3. Wielkość płatnika (do 20 vs powyżej 20 ubezpieczonych)
        // Document Intelligence oznacza zaznaczone kratki jako :selected: lub X / ☑
        if (Regex.IsMatch(normalized, @"(☑|\[x\]|:selected:|\bX\b)\s*do\s*20\s*ubezpieczonych", RegexOptions.IgnoreCase))
        {
            req.EmployerInsuredCount = 15;
            result.ExtractedFacts.Add("Płatnik zgłasza do 20 ubezpieczonych -> zasiłek wypłaca ZUS.");
        }
        else if (Regex.IsMatch(normalized, @"(☑|\[x\]|:selected:|\bX\b)\s*powyżej\s*20\s*ubezpieczonych", RegexOptions.IgnoreCase))
        {
            req.EmployerInsuredCount = 50;
            result.ExtractedFacts.Add("Płatnik zgłasza powyżej 20 ubezpieczonych -> zasiłek wypłaca pracodawca.");
        }

        // 4. Wymiar etatu
        var fteMatch = Regex.Match(normalized, @"wymiarze czasu pracy\s*[:\|]?\s*(\d+[.,]\d+|1/1|1/2|3/4)", RegexOptions.IgnoreCase);
        if (fteMatch.Success)
        {
            req.FteFraction = ParseFractionOrDecimal(fteMatch.Groups[1].Value, 1.0m);
            result.ExtractedFacts.Add($"Wykryty wymiar etatu: {req.FteFraction:0.##}.");
        }

        // 5. Scenariusz (przysposobienie / rodzina zastępcza / ciąża+chorobowe / łączenie z pracą)
        if (Regex.IsMatch(normalized, @"przysposobieni|ZAS-26", RegexOptions.IgnoreCase))
        {
            req.Scenario = ParenthoodScenario.Adoption;
            result.ExtractedFacts.Add("Wykryto scenariusz: przyjęcie dziecka na wychowanie (przysposobienie).");
        }
        else if (Regex.IsMatch(normalized, @"rodzin[aęy]\s+zastępcz", RegexOptions.IgnoreCase))
        {
            req.Scenario = ParenthoodScenario.FosterCareNonProfessional;
            result.ExtractedFacts.Add("Wykryto scenariusz: rodzina zastępcza niezawodowa.");
        }

        // 6. Łączenie urlopu rodzicielskiego z pracą
        var combineMatch = Regex.Match(normalized, @"łączeni[aeu].{0,80}rodzicielskiego.{0,80}(1/2|0[.,]50?0?|1/4|0[.,]25)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (combineMatch.Success || Regex.IsMatch(normalized, @"wymiar czasu pracy\s*0[.,]500", RegexOptions.IgnoreCase))
        {
            req.CombineWorkWithParentalFte = combineMatch.Success
                ? ParseFractionOrDecimal(combineMatch.Groups[1].Value, 0.5m)
                : 0.5m;
            req.ParentalWeeksCombinedWithWork = 32;
            req.CombineExtendedPartWithWork = Regex.IsMatch(normalized, @"proporcjonalnego wydłużenia.{0,80}(1/2|0[.,]5)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            result.ExtractedFacts.Add($"Wykryto łączenie urlopu rodzicielskiego z pracą ({req.CombineWorkWithParentalFte:0.##} etatu, wydłużona część łączona: {(req.CombineExtendedPartWithWork ? "TAK" : "NIE")}).");
        }

        // 7. Deklaracja pełnego wymiaru (stawka 81,5%) vs 100%/70%
        bool fullDimensionDeclared = Regex.IsMatch(normalized, @"(☑|\[x\]|:selected:|\bX\b)\s*Informuję,\s*że\s*zamierzam\s*skorzystać\s*z\s*pełnego\s*wymiaru", RegexOptions.IgnoreCase)
            || Regex.IsMatch(normalized, @"macierzyńskiego.{0,60}rodzicielskiego.{0,80}(364|52\s*tyg|2026-01-27.{0,15}2027-01-25)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        req.SubmitFullApplicationWithin21Days = fullDimensionDeclared;
        result.ExtractedFacts.Add(fullDimensionDeclared
            ? "Wykryto wniosek o pełny wymiar (stawka uśredniona 81,5%)."
            : "Wniosek złożony na osobne okresy (wariant 100% macierzyński / 70% rodzicielski).");

        // 8. Analiza sekcji „Uwagi” dot. wyłączenia składników (art. 41 ust. 1)
        bool excludeVarByNote = Regex.IsMatch(normalized, @"NIE\s+PODLEGA\s+ZALICZENIU\s+DO\s+PODSTAWY|WYPŁACANY\s+MIMO\s+NIEZDOLNOŚCI", RegexOptions.IgnoreCase);
        if (excludeVarByNote)
        {
            result.ExtractedFacts.Add("Sekcja UWAGI: wykryto dodatki wypłacane mimo L4/urlopu — wyłączono kol. 7 i 8 z podstawy wymiaru.");
        }

        // 9. Ekstrakcja tabeli wynagrodzeń miesięcznych z Z-3 (wiersze: Rok | Miesiąc | Dni | ... | Kwoty | 13.71%)
        ExtractMonthlySalaryRows(normalized, req, excludeVarByNote);
        if (req.MonthlySalaries.Count > 0)
        {
            result.ExtractedFacts.Add($"Wyekstrahowano {req.MonthlySalaries.Count} wierszy wynagrodzeń z tabeli Z-3.");
        }

        // 10. Ciążowe L4 / wynagrodzenie chorobowe (art. 92 KP)
        if (Regex.IsMatch(normalized, @"kod\s*B|zasiłek chorobowy.{0,40}ciąży|art\.\s*92\s*Kodeksu pracy.{0,80}TAK", RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            req.IncludePregnancySickness = true;
            req.HasCodeBOnEzla = true;
            req.PlannedPregnancySickDays = 115; // domyślna estymacja do edycji przez użytkownika
            result.ExtractedFacts.Add("Wykryto okres niezdolności do pracy w ciąży (wynagrodzenie chorobowe / zasiłek chorobowy).");
        }

        result.MappedRequest = req;
        return result;
    }

    private static void ExtractMonthlySalaryRows(string text, CalculationRequest req, bool excludeVarByNote)
    {
        // Dopasowuje zarówno tabele Markdown z Document Intelligence (| 2025 | 1 | 21 | 21 | ... | 8532.44 | ... | 13.71% |),
        // jak i wiersze tekstowe z PdfPig
        var lineRegex = new Regex(
            @"(?<year>202[2-7])\s*[\|\s]+(?<month>1[0-2]|0?[1-9])\s*[\|\s]+(?<d1>\d{1,2})\s*[\|\s]+(?<d2>\d{1,2})(?<rest>[^\n]{5,120})13[.,]7[13]",
            RegexOptions.Multiline);

        var seenMonths = new HashSet<(int, int)>();

        foreach (Match m in lineRegex.Matches(text))
        {
            int year = int.Parse(m.Groups["year"].Value);
            int month = int.Parse(m.Groups["month"].Value);
            if (!seenMonths.Add((year, month))) continue;

            int d1 = int.Parse(m.Groups["d1"].Value);
            int d2 = int.Parse(m.Groups["d2"].Value);
            int daysObligatory = Math.Max(d1, d2);
            int daysWorked = Math.Min(d1, d2);

            string rest = m.Groups["rest"].Value;
            var moneyMatches = Regex.Matches(rest, @"\d{1,2}\s?\d{3}[.,]\d{2}|\d{3}[.,]\d{2}");
            var amounts = moneyMatches
                .Select(mm => ParseDecimalSafe(mm.Value))
                .Where(v => v > 0)
                .ToList();

            if (amounts.Count == 0) continue;

            var row = new MonthlySalaryRecord
            {
                Year = year,
                Month = month,
                DaysWorked = daysWorked,
                DaysObligatory = daysObligatory,
                FixedGross = amounts[0],
                VariableProportionalGross = amounts.Count >= 3 ? amounts[1] : 0m,
                VariableActualGross = amounts.Count == 2 ? amounts[1] : (amounts.Count >= 3 ? amounts[2] : 0m),
                ContributionRatePercent = 13.71m,
                ExcludeVariableDueToEmployerNote = excludeVarByNote,
                AbsenceReason = rest.Contains("ZLA", StringComparison.OrdinalIgnoreCase) ? "ZLA" : null
            };

            req.MonthlySalaries.Add(row);
        }
    }

    private static decimal ParseFractionOrDecimal(string raw, decimal fallback)
    {
        raw = raw.Trim();
        if (raw.Contains('/'))
        {
            var parts = raw.Split('/');
            if (parts.Length == 2 &&
                decimal.TryParse(parts[0], out var num) &&
                decimal.TryParse(parts[1], out var den) && den != 0)
            {
                return Math.Round(num / den, 4);
            }
        }
        return ParseDecimalSafe(raw) is > 0 and var val ? val : fallback;
    }

    private static decimal ParseDecimalSafe(string raw)
    {
        string clean = raw.Replace(" ", "").Replace(",", ".");
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : 0m;
    }
}