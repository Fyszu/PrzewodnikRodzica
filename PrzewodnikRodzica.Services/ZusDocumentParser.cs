using System.Globalization;
using System.Net;
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
    private const string DateTokenPattern =
        @"\d{1,2}[\s.\-/]+\d{1,2}[\s.\-/]+\d{2,4}|\d{4}[\s.\-/]+\d{1,2}[\s.\-/]+\d{1,2}|(?<!\d)\d{8}(?!\d)";
    private static readonly DateOnly SupplementaryLawEffectiveDate = new(2025, 3, 19);

    public ParsedDocumentResult ParseExtractedText(string rawText)
    {
        var result = new ParsedDocumentResult();
        var req = new CalculationRequest
        {
            // Teczka ZUS dotyczy konkretnego wnioskodawcy — nie doliczamy automatycznie urlopów 2. rodzica (+77 dni)
            SecondParentInsured = false,
            SecondParentParentalWeeks = 0,
            IncludePaternityLeave = false
        };

        string normalized = rawText.Replace("\r\n", "\n");
        string plainText = StripHtmlToPlainText(normalized);

        // Usuwamy standardowe pouczenie ze stopki zaświadczeń szpitalnych (zawiera słowa „rodzina zastępcza” i „przysposobienia”)
        string textWithoutHospitalBoilerplate = Regex.Replace(
            plainText,
            @"Uprawnionymi\s+do\s+uzyskania\s+przedmiotowego\s+zaświadczenia.{0,450}?(?:przysposobienia\s+dziecka|art\.\s*183\s*§\s*31.{0,260})",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 1. Rozpoznanie formularzy w teczce
        bool isZurForm = Regex.IsMatch(plainText, @"\bZUR\b")
            || Regex.IsMatch(plainText, @"Wniosek\s+o\s+zasiłek\s+macierzyński\s+za\s+okres\s+urlopu\s+rodzicielskiego", RegexOptions.IgnoreCase);
        
        if (isZurForm)
            result.DetectedForms.Add("ZUR (Wniosek o zasiłek za okres urlopu rodzicielskiego — od 24.10.2026 zastąpiony przez ZAM)");
        if (Regex.IsMatch(plainText, @"\bZ-3b\b", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("Z-3b (Zaświadczenie płatnika składek — działalność gospodarcza)");
        if (Regex.IsMatch(plainText, @"\bZ-3\b(?![ab])", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("Z-3 (Zaświadczenie płatnika składek — pracownik)");
        if (Regex.IsMatch(plainText, @"\bZ-3a\b", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("Z-3a (Zaświadczenie płatnika — zleceniobiorca)");
        if (Regex.IsMatch(plainText, @"\bZAS-12\b", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("ZAS-12 (Zaświadczenie płatnika składek — dalszy okres zasiłku)");
        if (Regex.IsMatch(plainText, @"\bZAM\b")
            || (!isZurForm && Regex.IsMatch(plainText, @"Wniosek\s+o\s+zasiłek\s+macierzyński", RegexOptions.IgnoreCase)))
            result.DetectedForms.Add("ZAM (Wniosek o zasiłek macierzyński)");
        if (Regex.IsMatch(textWithoutHospitalBoilerplate, @"\bZAS-26\b", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("ZAS-26 (Przyjęcie dziecka na wychowanie / przysposobienie)");
        if (Regex.IsMatch(plainText, @"ZUS\s*ZLA|e-?ZLA", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("e-ZLA (Zwolnienie lekarskie)");
        if (Regex.IsMatch(plainText, @"\bPOG\b|PISMO OGÓLNE", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("POG (Pismo ogólne do ZUS)");
        if (Regex.IsMatch(plainText, @"ZA[ŚS]WIADCZENIE\s+O\s+POBYCI[EU]\s+DZIECKA\s+W\s+SZPITALU", RegexOptions.IgnoreCase))
            result.DetectedForms.Add("Zaświadczenie o pobycie dziecka w szpitalu (art. 180² KP)");

        bool hasZamForm = result.DetectedForms.Any(f => f.StartsWith("ZAM"));

        // 2. Weryfikacja tytułu ubezpieczenia
        bool hasEmployeeCode = Regex.IsMatch(plainText, @"0110\s*00|1011000|\b0110\b|pracownik podlegający|Jest zatrudniony", RegexOptions.IgnoreCase);
        bool hasZ3a = result.DetectedForms.Any(f => f.StartsWith("Z-3a"));
        bool hasJdg = result.DetectedForms.Any(f => f.StartsWith("Z-3b"))
            || Regex.IsMatch(plainText, @"\b0510\b|\b0570\b|\b0590\b|pozarolnicz[aąeę]j?\s+działalnoś[ćc]", RegexOptions.IgnoreCase);

        if (hasEmployeeCode)
        {
            req.EmploymentTitle = EmploymentTitle.Employee;
            result.HasConfirmedInsurance = true;
            result.InsuranceSummary = "Potwierdzono obowiązkowe ubezpieczenie chorobowe z tytułu umowy o pracę (kod 0110 / druk Z-3).";
        }
        else if (hasJdg)
        {
            req.EmploymentTitle = EmploymentTitle.SelfEmployed;
            req.HasVoluntarySicknessInsurance = true;
            result.HasConfirmedInsurance = true;
            result.InsuranceSummary = "Wykryto pozarolniczą działalność gospodarczą — JDG (kod 0510/0570/0590 lub druk Z-3b). Zasiłek wypłaca ZUS (pamiętaj o ponownym zgłoszeniu ZUS ZUA po zakończeniu macierzyńskiego).";
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
        // Najpierw sprawdzamy „powyżej 20”, aby ☑ przed „powyżej 20” nie podpięło się pod sąsiednie „do 20”
        bool largePayerChecked =
            Regex.IsMatch(plainText, @"(☑|\[x\]|:selected:|\bX\b)(?:(?!☐|:unselected:).){0,18}powyżej\s*20\s*ubezpieczonych", RegexOptions.IgnoreCase);

        bool smallPayerChecked = !largePayerChecked && (
            Regex.IsMatch(plainText, @"(☑|\[x\]|:selected:|\bX\b)(?:(?!☐|:unselected:).){0,18}do\s*20\s*ubezpieczonych", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(plainText, @"do\s*20\s*ubezpieczonych[\s|]{0,15}(☑|\[x\]|:selected:)(?!\s*X?\s*powyżej)", RegexOptions.IgnoreCase));

        if (largePayerChecked)
        {
            req.EmployerInsuredCount = 50;
            result.ExtractedFacts.Add("Płatnik zgłasza powyżej 20 ubezpieczonych (duży płatnik — zasiłek w trakcie zatrudnienia wypłaca pracodawca).");
        }
        else if (smallPayerChecked)
        {
            req.EmployerInsuredCount = 15;
            result.ExtractedFacts.Add("Płatnik zgłasza do 20 ubezpieczonych -> zasiłek wypłaca ZUS.");
        }

        // 4. Wymiar etatu
        var fteMatch = Regex.Match(plainText, @"wymiarze czasu pracy\s*[:\|]?\s*(\d+[.,]\d+|1/1|1/2|3/4)", RegexOptions.IgnoreCase);
        if (fteMatch.Success)
        {
            req.FteFraction = ParseFractionOrDecimal(fteMatch.Groups[1].Value, 1.0m);
            result.ExtractedFacts.Add($"Wykryty wymiar etatu: {req.FteFraction:0.##}.");
        }

        // 5. Scenariusz (rodzina zastępcza niezawodowa / przysposobienie / urodzenie)
        bool hasBiologicalBirthProof = Regex.IsMatch(
            plainText,
            @"urodziłam|poród\s+(?:pojedynczy|mnogi|w\s+\d+|dn\.|odbył\s+się)|ciąża\s+(?:bliźniacza|mnoga)|Oddział.{0,25}Położnictwa|(?:☑|\[x\]|:selected:|\bX\b)(?:(?!☐|:unselected:).){0,15}1\.?\s*urodzeniem\s+dziecka",
            RegexOptions.IgnoreCase);

        bool hasFosterOfficialDocument = Regex.IsMatch(
            textWithoutHospitalBoilerplate,
            @"pieczy\s+zastępczej|niezawodow\w*\s+rodzin\w*\s+zastępcz",
            RegexOptions.IgnoreCase);

        bool hasFosterCheckedOnForm = Regex.IsMatch(
            textWithoutHospitalBoilerplate,
            @"(☑|\[x\]|:selected:|\bX\b)(?:(?!☐|:unselected:).){0,25}przyj[ęe]ci[ae]m?\s+na\s+wychowanie.{0,90}rodziny\s+zastępczej",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        bool hasFosterProof =
            hasFosterOfficialDocument ||
            hasFosterCheckedOnForm ||
            (!hasBiologicalBirthProof && !hasZamForm && !isZurForm && Regex.IsMatch(textWithoutHospitalBoilerplate, @"(?<!z\s+wyjątkiem\s+)rodzin[aęy]\s+zastępcz", RegexOptions.IgnoreCase));

        bool hasAdoptionCheckedOnForm = !hasFosterProof && Regex.IsMatch(
            textWithoutHospitalBoilerplate,
            @"(☑|\[x\]|:selected:|\bX\b)(?:(?!☐|:unselected:).){0,25}(?:urlopu\s+na\s+warunkach\s+urlopu\s+macierzyńskiego|przyj[ęe]ci[ae]m?\s+na\s+wychowanie.{0,90}przysposobieni)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        bool hasAdoptionProof = !hasFosterProof && (
            Regex.IsMatch(textWithoutHospitalBoilerplate, @"\bZAS-26\b", RegexOptions.IgnoreCase) ||
            hasAdoptionCheckedOnForm ||
            (!hasBiologicalBirthProof && !hasZamForm && !isZurForm && Regex.IsMatch(textWithoutHospitalBoilerplate, @"przysposobieni", RegexOptions.IgnoreCase)));

        if (hasFosterProof)
        {
            req.Scenario = ParenthoodScenario.FosterCareNonProfessional;
            result.DetectedForms.Add("Postanowienie sądu / Zaświadczenie o pieczy zastępczej");
            result.ExtractedFacts.Add("Wykryto scenariusz: rodzina zastępcza niezawodowa (urlop na warunkach urlopu macierzyńskiego — art. 183 KP).");
        }
        else if (hasAdoptionProof)
        {
            req.Scenario = ParenthoodScenario.Adoption;
            result.ExtractedFacts.Add("Wykryto scenariusz: przyjęcie dziecka na wychowanie (przysposobienie).");
        }

        // 5a. Liczba dzieci (poród mnogi — bliźnięta, trojaczki itd.)
        req.ChildrenCount = DetectChildrenCount(plainText);
        req.MotherMaternityWeeksTaken = req.ChildrenCount switch
        {
            <= 1 => 20,
            2 => 31,
            3 => 33,
            4 => 35,
            _ => 37
        };
        if (req.ChildrenCount > 1)
        {
            result.ExtractedFacts.Add($"Wykryto poród mnogi: {req.ChildrenCount} dzieci (urlop macierzyński: {req.MotherMaternityWeeksTaken} tyg.).");
        }

        // 5b. Zaświadczenie „Za życiem” (wyklucza niezaznaczoną opcję na druku ZAM)
        bool hasZaZyciem =
            Regex.IsMatch(plainText, @"ciężkie.{0,25}nieodwracalne\s+upośledzenie", RegexOptions.IgnoreCase | RegexOptions.Singleline) ||
            Regex.IsMatch(plainText, @"dodatkowe\s+24\s+tygodnie", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(plainText, @"ZA[ŚS]WIADCZENIE\s+LEKARSKIE.{0,180}za\s+[żz]yciem", RegexOptions.IgnoreCase | RegexOptions.Singleline) ||
            Regex.IsMatch(plainText, @"(☑|\[x\]|:selected:)(?:(?!☐|:unselected:).){0,20}Oświadczam(?:(?!☐|:unselected:).){0,180}za\s+[żz]yciem", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (hasZaZyciem)
        {
            req.HasZaZyciemCertificate = true;
            req.PrimaryParentParentalWeeks = req.ChildrenCount <= 1 ? 56 : 58;
            int totalPool = req.ChildrenCount <= 1 ? 65 : 67;
            result.DetectedForms.Add("Zaświadczenie lekarskie „Za życiem” (art. 4 ust. 3 ustawy)");
            result.ExtractedFacts.Add($"Wykryto zaświadczenie „Za życiem” -> wydłużony urlop rodzicielski (+24 tyg., do {req.PrimaryParentParentalWeeks} tyg. dla głównego rodzica / {totalPool} tyg. łącznie).");
        }
        else if (req.ChildrenCount > 1)
        {
            req.PrimaryParentParentalWeeks = 34; // 43 - 9 tyg.
        }

        // 6. Łączenie urlopu rodzicielskiego z pracą
        var combineMatch = Regex.Match(plainText, @"łączeni[aeu].{0,100}rodzicielskiego.{0,100}(1/2|0[.,]50?0?|1/4|0[.,]25)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (combineMatch.Success || Regex.IsMatch(plainText, @"wymiar czasu pracy\s*0[.,]500", RegexOptions.IgnoreCase))
        {
            req.CombineWorkWithParentalFte = combineMatch.Success
                ? ParseFractionOrDecimal(combineMatch.Groups[1].Value, 0.5m)
                : 0.5m;
            req.ParentalWeeksCombinedWithWork = req.PrimaryParentParentalWeeks;
            req.CombineExtendedPartWithWork = Regex.IsMatch(plainText, @"proporcjonalnego wydłużenia.{0,100}(1/2|0[.,]5)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            result.ExtractedFacts.Add($"Wykryto łączenie urlopu rodzicielskiego z pracą ({req.CombineWorkWithParentalFte:0.##} etatu, wydłużona część łączona: {(req.CombineExtendedPartWithWork ? "TAK" : "NIE")}).");
        }

        // 7. Deklaracja pełnego wymiaru (stawka 81,5%) vs 100%/70%
        bool fullDimensionDeclared = DetectFullDimension815(plainText);
        req.SubmitFullApplicationWithin21Days = fullDimensionDeclared;
        result.ExtractedFacts.Add(fullDimensionDeclared
            ? "Wykryto wniosek o pełny wymiar (stawka uśredniona 81,5%)."
            : "Wniosek złożony na osobne okresy (wariant 100% macierzyński / 70% rodzicielski).");

        // 8. Analiza sekcji „Uwagi” dot. wyłączenia składników (art. 41 ust. 1)
        bool excludeVarByNote = Regex.IsMatch(plainText, @"NIE\s+PODLEGA\s+ZALICZENIU\s+DO\s+PODSTAWY|WYPŁACANY\s+MIMO\s+NIEZDOLNOŚCI", RegexOptions.IgnoreCase);
        if (excludeVarByNote)
        {
            result.ExtractedFacts.Add("Sekcja UWAGI: wykryto dodatki wypłacane mimo L4/urlopu — wyłączono kol. 7 i 8 z podstawy wymiaru.");
        }

        // 9. Ekstrakcja tabeli wynagrodzeń miesięcznych z Z-3 (ostatnia korekta nadpisuje wcześniejsze Z-3, maks. 12 mies.)
        ExtractMonthlySalaryRows(normalized, req, excludeVarByNote);
        if (req.MonthlySalaries.Count > 0)
        {
            req.AverageMonthlyGross = Math.Round(
                req.MonthlySalaries.Average(r =>
                    r.FixedGross + (r.ExcludeVariableDueToEmployerNote ? 0m : r.VariableProportionalGross + r.VariableActualGross)),
                2);
            result.ExtractedFacts.Add($"Wyekstrahowano {req.MonthlySalaries.Count} wierszy wynagrodzeń z tabeli Z-3.");
        }
        else if (req.EmploymentTitle == EmploymentTitle.SelfEmployed)
        {
            result.ExtractedFacts.Add("Dla JDG (druk Z-3b) podstawa składek znajduje się w deklaracjach ZUS DRA (nie w PDF) -> w kalkulatorze wpisz własną podstawę wymiaru składek.");
        }
        else if (result.DetectedForms.Any(f => f.StartsWith("Z-3 (")))
        {
            result.ExtractedFacts.Add("Tabela wynagrodzeń na załączonych drukach Z-3 jest pusta (kontynuacja podstawy z wcześniejszego świadczenia — art. 43) -> zastosowano domyślne 7 000,00 zł brutto do ręcznego uzupełnienia w kalkulatorze.");
        }

        if (Regex.IsMatch(plainText, @"podwyższeni[ae]\s+zasiłku\s+macierzyńskiego\s+do\s+(?:wysokości|kwoty)\s+świadczenia\s+rodzicielskiego", RegexOptions.IgnoreCase))
        {
            result.ExtractedFacts.Add("Wykryto pismo ZUS dot. podwyższenia zasiłku macierzyńskiego do kwoty świadczenia rodzicielskiego (1 000,00 zł netto/mies. — art. 31 ust. 3a).");
        }

        // 10. Ekstrakcja dat sprawy (data porodu / początek urlopu), ustania zatrudnienia oraz historycznej płacy minimalnej
        DateOnly? detectedBirthDate = ExtractCaseDatesAndHistoricalMinWage(plainText, req, result);

        ApplyFatherOnlyScopeIfDetected(plainText, isZurForm, hasZamForm, req, result);

        // 10a. Wykrycie umowy przedłużonej do dnia porodu (ustanie zatrudnienia w dniu porodu -> płatnikiem zostaje ZUS)
        DetectEmploymentTerminationOnBirth(plainText, detectedBirthDate, req, result);

        // 10b. Wcześniak / hospitalizacja — uzupełniający urlop macierzyński (art. 180^2 KP, obowiązuje od 19.03.2025)
        ExtractSupplementaryMaternity(plainText, detectedBirthDate, req, result);

        // 11. Ciążowe L4 / wynagrodzenie chorobowe (art. 92 KP + zasiłek chorobowy na Z-3/e-ZLA)
        string textWithoutZ3Instructions = Regex.Replace(
            plainText,
            @"Sekcja:\s*Informacje\s+o\s+wypłaconych\s+świadczeniach.{0,1200}",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        bool hasPregnancySickEvidence =
            Regex.IsMatch(textWithoutZ3Instructions, @"\bkod\s*B\b|zasiłek\s+chorobowy.{0,40}ciąży", RegexOptions.IgnoreCase | RegexOptions.Singleline) ||
            Regex.IsMatch(textWithoutZ3Instructions, @"art\.\s*92\s*Kodeksu\s+pracy.{0,120}(?:☑|\[x\]|:selected:)\s*X?\s*\bTAK\b", RegexOptions.IgnoreCase | RegexOptions.Singleline) ||
            Regex.IsMatch(textWithoutZ3Instructions, @"art\.\s*92\s*Kodeksu\s+pracy.{0,60}\bX\s+TAK\b", RegexOptions.IgnoreCase | RegexOptions.Singleline) ||
            (result.DetectedForms.Any(f => f.StartsWith("e-ZLA")) && Regex.IsMatch(textWithoutZ3Instructions, @"cią[żz]|026\b|O26\b", RegexOptions.IgnoreCase));

        if (hasPregnancySickEvidence)
        {
            req.IncludePregnancySickness = true;
            req.HasCodeBOnEzla = true;

            int extractedSickDays = ExtractPregnancySickDays(plainText, detectedBirthDate);
            if (extractedSickDays > 0)
            {
                req.PlannedPregnancySickDays = extractedSickDays;
                bool isContinuationOnly = Regex.IsMatch(plainText, @"(☑|\[x\]|:selected:|\bX\b)\s*wniosek\s+o\s+kontynuację", RegexOptions.IgnoreCase);
                result.ExtractedFacts.Add(isContinuationOnly
                    ? $"Wykryto wniosek o kontynuację chorobowego w ciąży: z załączonego wycinka zliczono {extractedSickDays} dni (pełną liczbę dni L4 z całej ciąży uzupełnij w kalkulatorze)."
                    : $"Wykryto okres niezdolności do pracy w ciąży: zliczono {extractedSickDays} dni z załączonych e-ZLA / Z-3 (przed porodem).");
            }
            else
            {
                req.PlannedPregnancySickDays = 115;
                result.ExtractedFacts.Add("Wykryto okres niezdolności do pracy w ciąży (wynagrodzenie chorobowe / zasiłek chorobowy — przyjęto estymację 115 dni, edytowalną w kalkulatorze).");
            }
        }

        result.MappedRequest = req;
        return result;
    }

    private static void DetectEmploymentTerminationOnBirth(
        string plainText,
        DateOnly? referenceBirthDate,
        CalculationRequest req,
        ParsedDocumentResult result)
    {
        if (req.EmploymentTitle != EmploymentTitle.Employee || !referenceBirthDate.HasValue)
            return;

        var empMatch = Regex.Match(
            plainText,
            @"Jest\s+zatrudniony.{0,40}?Od\s+(?<from>" + DateTokenPattern + @")\s+do\s+(?<to>" + DateTokenPattern + @")",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (empMatch.Success && TryParseDate(empMatch.Groups["to"].Value, out var empEnd))
        {
            int diff = Math.Abs(empEnd.DayNumber - referenceBirthDate.Value.DayNumber);
            if (diff <= 2)
            {
                req.TerminationReason = SpecialTerminationReason.FixedTermExtendedToBirth;
                result.ExtractedFacts.Add($"Wykryto zakończenie zatrudnienia w dniu porodu ({empEnd:yyyy-MM-dd}, umowa terminowa przedłużona do porodu — art. 177 § 3 KP) -> zasiłek macierzyński po ustaniu zatrudnienia wypłaca ZUS.");
            }
        }
    }

    private static int DetectChildrenCount(string plainText)
    {
        if (Regex.IsMatch(plainText, @"pięcioracz|pięcioro\s+dzieci|37[- ]?tygodniow", RegexOptions.IgnoreCase))
            return 5;
        if (Regex.IsMatch(plainText, @"czworacz|czworo\s+dzieci|35[- ]?tygodniow", RegexOptions.IgnoreCase))
            return 4;
        if (Regex.IsMatch(plainText, @"trojacz|troje\s+dzieci|33[- ]?tygodniow", RegexOptions.IgnoreCase))
            return 3;
        if (Regex.IsMatch(plainText, @"bliźni|dwoje\s+dzieci|dwójk[aęi]\s+dzieci|31[- ]?tygodniow|obie\s+moje\s+córk|obaj\s+moi\s+synow", RegexOptions.IgnoreCase))
            return 2;
        return 1;
    }

    private static void ExtractSupplementaryMaternity(
        string plainText,
        DateOnly? birthDate,
        CalculationRequest req,
        ParsedDocumentResult result)
    {
        bool hasHospitalCertificate =
            Regex.IsMatch(plainText, @"ZA[ŚS]WIADCZENIE\s+O\s+POBYCI[EU]\s+(?:DZIECKA\s+)?W\s+SZPITALU|o\s+pobycie\s+w\s+szpitalu\s+dziecka\s+przedwcześnie", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(plainText, @"(☑|\[x\]|:selected:|\bX\b)(?:(?!☐|:unselected:).){0,25}uzupełniającego\s+urlopu\s+macierzyńskiego", RegexOptions.IgnoreCase);

        if (!hasHospitalCertificate)
            return;

        // Przepisy przejściowe z 19.03.2025 r. (jak w sprawie Jolanta Pi_2)
        if (birthDate.HasValue && birthDate.Value < SupplementaryLawEffectiveDate)
        {
            DateOnly start = req.LeaveStartDate ?? birthDate.Value;
            DateOnly basicMaternityEnd = start.AddDays(req.MotherMaternityWeeksTaken * 7 - 1);
            if (basicMaternityEnd < SupplementaryLawEffectiveDate)
            {
                result.ExtractedFacts.Add($"Wykryto zaświadczenie szpitalne (art. 180² KP), ale podstawowy urlop macierzyński zakończył się {basicMaternityEnd:yyyy-MM-dd} (przed 19.03.2025 r.) — brak prawa do urlopu uzupełniającego w przepisach przejściowych.");
                return;
            }
        }

        // 1. Tydzień ciąży (22..43)
        var weeksFound = new List<int>();
        string[] weekPatterns =
        [
            @"(?:ukończony\s+tydzień\s+ciąży|tydzień\s+ciąży)[^0-9]{0,25}(?<w>\d{2})\b",
            @"(?:poród\s+w|wcześniactwo|urodzone\s+w|przedwcześnie)\s*\(?\s*(?<w>\d{2})(?:\s*\+\s*\d)?\s*(?:tyg|hbd)",
            @"\b(?<w>\d{2})\s*\+\s*\d\s*Hbd\b"
        ];
        foreach (string pat in weekPatterns)
        {
            foreach (Match m in Regex.Matches(plainText, pat, RegexOptions.IgnoreCase))
            {
                if (int.TryParse(m.Groups["w"].Value, out int w) && w is >= 22 and <= 43)
                    weeksFound.Add(w);
            }
        }
        int? gestationWeek = weeksFound.Count > 0 ? weeksFound.Min() : null;

        // 2. Najniższa masa urodzeniowa (odporna na literówki OCR: ciala / clata / clala) + checkbox <= 1000 g na ZAM
        var weightsFound = new List<int>();
        foreach (Match m in Regex.Matches(plainText, @"masa\s+c[il][aą][łlt][aą][^0-9]{0,20}(?<g>\d{3,4})(?!\d)", RegexOptions.IgnoreCase))
        {
            if (int.TryParse(m.Groups["g"].Value, out int g) && g is >= 350 and <= 6000)
                weightsFound.Add(g);
        }
        int? lowestWeight = weightsFound.Count > 0 ? weightsFound.Min() : null;

        bool extremeWeightCheckboxOnZam = Regex.IsMatch(
            plainText,
            @"(☑|\[x\]|:selected:|\bX\b)\s*masa\s+urodzeniowa\s+dziecka\s+n[il]e\s+wi[ęe]ksza\s+ni[żz]\s+1000",
            RegexOptions.IgnoreCase);
        if (!lowestWeight.HasValue && extremeWeightCheckboxOnZam)
            lowestWeight = 1000;

        bool isExtremePremature = (gestationWeek.HasValue && gestationWeek.Value < 28) ||
                                  (lowestWeight.HasValue && lowestWeight.Value <= 1000);
        int windowDays = isExtremePremature ? 105 : 56;

        // 3. Zliczenie dni hospitalizacji z zaświadczeń szpitalnych
        var hospitalDays = new HashSet<DateOnly>();
        bool termMin2DaysBetween5And28 = false;

        void AddHospitalSpan(DateOnly from, DateOnly to)
        {
            if (to < from) return;
            if (birthDate.HasValue)
            {
                DateOnly winEnd = birthDate.Value.AddDays(windowDays - 1);
                if (from < birthDate.Value) from = birthDate.Value;
                if (from > winEnd) return;
                if (to > winEnd) to = winEnd;

                DateOnly d5 = birthDate.Value.AddDays(4);
                DateOnly d28 = birthDate.Value.AddDays(27);
                DateOnly overlapFrom = from > d5 ? from : d5;
                DateOnly overlapTo = to < d28 ? to : d28;
                if (overlapTo.DayNumber - overlapFrom.DayNumber + 1 >= 2)
                    termMin2DaysBetween5And28 = true;
            }

            for (var d = from; d <= to; d = d.AddDays(1))
                hospitalDays.Add(d);
        }

        var stayMatches = Regex.Matches(
            plainText,
            @"przebywało\s+(?:w\s+)?szpitalu[^0-9]{0,25}?(?<from>" + DateTokenPattern + @")\s*(?:r\.?)?\s*(?:do|-)[^0-9]{0,10}(?<to>" + DateTokenPattern + @")",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match m in stayMatches)
        {
            if (TryParseDate(m.Groups["from"].Value, out var from) &&
                TryParseDate(m.Groups["to"].Value, out var to))
            {
                AddHospitalSpan(from, to);
            }
        }

        // 3b. Fallback: data wypisu dziecka ze szpitala na ZAM (pkt 6: „wypisaniem ze szpitala ... dziecka 10102025”)
        if (hospitalDays.Count == 0 && birthDate.HasValue)
        {
            var dischargeMatch = Regex.Match(
                plainText,
                @"wypisaniem\s+ze\s+szpitala.{0,80}?dziecka[^0-9]{0,25}(?<d>" + DateTokenPattern + @")",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (dischargeMatch.Success &&
                TryParseDate(dischargeMatch.Groups["d"].Value, out var dischargeDate) &&
                dischargeDate > birthDate.Value &&
                dischargeDate <= birthDate.Value.AddDays(200))
            {
                AddHospitalSpan(birthDate.Value, dischargeDate);
            }
        }

        // 3c. Fallback: okres urlopu uzupełniającego wpisany wprost na ZAM (np. 30.10.25-11.02.26)
        if (hospitalDays.Count == 0)
        {
            var zamSuppSpan = Regex.Match(
                plainText,
                @"uzupełniającego\s+urlopu\s+macierzyńskiego[^0-9]{0,30}(?<from>" + DateTokenPattern + @")\s*(?:-|do)\s*(?<to>" + DateTokenPattern + @")",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            if (zamSuppSpan.Success &&
                TryParseDate(zamSuppSpan.Groups["from"].Value, out var sFrom) &&
                TryParseDate(zamSuppSpan.Groups["to"].Value, out var sTo))
            {
                int spanDays = sTo.DayNumber - sFrom.DayNumber + 1;
                if (spanDays is >= 7 and <= 112)
                {
                    int clamped = Math.Min(spanDays, windowDays);
                    req.HospitalizationDaysWithinLimit = clamped;
                }
            }
        }

        int totalHospDays = hospitalDays.Count > 0
            ? Math.Min(hospitalDays.Count, windowDays)
            : req.HospitalizationDaysWithinLimit;

        if (totalHospDays == 0)
        {
            var reqWeeksMatch = Regex.Match(plainText, @"(?<w>\d{1,2})[- ]?tygodniowego\s+uzupełniającego\s+urlopu", RegexOptions.IgnoreCase);
            if (reqWeeksMatch.Success && int.TryParse(reqWeeksMatch.Groups["w"].Value, out int rw) && rw is >= 1 and <= 15)
                totalHospDays = rw * 7;
        }

        if (totalHospDays > 0 && (gestationWeek.HasValue || lowestWeight.HasValue))
        {
            req.CalculateSupplementaryMaternity = true;
            req.GestationWeekAtBirth = gestationWeek;
            req.LowestBirthWeightGrams = lowestWeight;
            req.HospitalizationDaysWithinLimit = totalHospDays;
            req.TermBabyHospitalizedMin2DaysBetweenDay5And28 = termMin2DaysBetween5And28;

            int suppWeeks = BenefitEngine.CalculateSupplementaryMaternityWeeks(req);
            result.ExtractedFacts.Add(
                $"Wykryto wcześniactwo / hospitalizację (tydzień ciąży: {gestationWeek?.ToString() ?? "b.d."}, masa: {lowestWeight?.ToString() ?? "b.d."} g, pobyt w limicie: {totalHospDays} dni) -> uzupełniający urlop macierzyński: {suppWeeks} tyg. (100%).");
        }
        else
        {
            result.ExtractedFacts.Add(
                "Wykryto zaświadczenie o pobycie dziecka w szpitalu (art. 180² KP), ale jakość skanu nie pozwoliła na jednoznaczny odczyt dat pobytu lub masy urodzeniowej — uzupełnij sekcję wcześniactwa ręcznie w kalkulatorze.");
        }
    }

    private static bool DetectFullDimension815(string plainText)
    {
        // 1. Zaznaczony checkbox deklaracji pełnego wymiaru na ZAM (odporny na literówki OCR w środku zdania)
        if (Regex.IsMatch(plainText, @"(☑|\[x\]|:selected:|\bX\b)(?:(?!☐|:unselected:).){0,25}Informuj.{0,55}wymiaru", RegexOptions.IgnoreCase | RegexOptions.Singleline))
            return true;

        // 2. Łączny wniosek słowny („urlop macierzyński i rodzicielski”) lub jawna stawka 81,5%
        if (Regex.IsMatch(plainText, @"(?<!na\s+warunkach\s+)urlop[u]?\s+macierzyński(?:ego)?\s+i\s+(?:urlopu\s+)?rodzicielski(?:ego)?", RegexOptions.IgnoreCase))
            return true;

        if (Regex.IsMatch(plainText, @"w\s+wysokości\s+81[.,]5\s*%", RegexOptions.IgnoreCase))
            return true;

        // 3. Na tej samej stronie wniosku ZAM wpisano jednocześnie daty urlopu macierzyńskiego i rodzicielskiego (blisko siebie)
        if (Regex.IsMatch(
                plainText,
                @"urlopu\s+macierzyńskiego.{0,65}\d{2}[.\-/]\d{2}[.\-/]202\d.{0,240}urlopu\s+rodzicielskiego.{0,65}\d{2}[.\-/]\d{2}[.\-/]202\d",
                RegexOptions.IgnoreCase | RegexOptions.Singleline))
            return true;

        // 4. Ciągły okres zasiłku macierzyńskiego ~52–105 tyg. zgłoszony jednym przedziałem na Z-3
        var spanMatches = Regex.Matches(
            plainText,
            @"zasiłek\s+macierzyński.{0,40}?od\s+(?<from>" + DateTokenPattern + @")\s*(?:r\.?)?\s*(?:do|-)\s*(?<to>" + DateTokenPattern + @")",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match m in spanMatches)
        {
            if (TryParseDate(m.Groups["from"].Value, out var from) &&
                TryParseDate(m.Groups["to"].Value, out var to))
            {
                int days = to.DayNumber - from.DayNumber + 1;
                if (days is >= 355 and <= 750)
                    return true;
            }
        }

        return false;
    }

    private static int ExtractPregnancySickDays(string plainText, DateOnly? birthDate)
    {
        var sickDates = new HashSet<DateOnly>();

        void AddRange(DateOnly from, DateOnly to)
        {
            if (to < from) return;
            int rawSpan = to.DayNumber - from.DayNumber + 1;
            // Limit 270 dni (maksymalny okres zasiłkowy w ciąży — na Z-3 pkt 2 bywa podany jeden zbiorczy przedział np. 192 dni)
            if (rawSpan is <= 0 or > 270) return;

            if (birthDate.HasValue)
            {
                if (from < birthDate.Value.AddDays(-300) || from >= birthDate.Value) return;
                if (to >= birthDate.Value) to = birthDate.Value.AddDays(-1);
            }

            for (var d = from; d <= to; d = d.AddDays(1))
                sickDates.Add(d);
        }

        // 1. Wydruki Oryginał ZUS ZLA
        var zlaMatches = Regex.Matches(
            plainText,
            @"Niezdolność\s+do\s+pracy.{0,60}?Od[^0-9]{0,20}(?<from>" + DateTokenPattern + @").{0,60}?Do[^0-9]{0,20}(?<to>" + DateTokenPattern + @")",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match m in zlaMatches)
        {
            if (TryParseDate(m.Groups["from"].Value, out var from) &&
                TryParseDate(m.Groups["to"].Value, out var to))
            {
                AddRange(from, to);
            }
        }

        // 2. Wnioski Z-3 / ZAS-12 (zasiłek chorobowy za okres od ... do ...)
        var zchMatches = Regex.Matches(
            plainText,
            @"zasiłek\s+chorobowy.{0,45}?od[^0-9]{0,10}(?<from>" + DateTokenPattern + @")\s*(?:r\.?)?\s*(?:do|-)\s*(?<to>" + DateTokenPattern + @")",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match m in zchMatches)
        {
            if (TryParseDate(m.Groups["from"].Value, out var from) &&
                TryParseDate(m.Groups["to"].Value, out var to))
            {
                AddRange(from, to);
            }
        }

        var pairRegex = new Regex(
            @"(?<from>" + DateTokenPattern + @")\s*(?:r\.?)?\s*(?:-|do)\s*(?<to>" + DateTokenPattern + @")",
            RegexOptions.IgnoreCase);

        // 3. Z-3 sekcja wypłaconych świadczeń: art. 92 KP (wynagrodzenie chorobowe) + pkt 2 (zasiłek chorobowy wypłacony przez płatnika)
        var paidSickBlocks = Regex.Matches(
            plainText,
            @"(?:art\.\s*92\s*Kodeksu\s+pracy|Czy\s+wypłaciłeś\s+zasiłek\s+chorobowy\s*/\s*świadczenie\s+rehabilitacyjne)(?<block>.{0,260})",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match block in paidSickBlocks)
        {
            foreach (Match p in pairRegex.Matches(block.Groups["block"].Value))
            {
                if (TryParseDate(p.Groups["from"].Value, out var from) &&
                    TryParseDate(p.Groups["to"].Value, out var to))
                {
                    AddRange(from, to);
                }
            }
        }

        return Math.Min(sickDates.Count, 270);
    }

    private static DateOnly? ExtractCaseDatesAndHistoricalMinWage(string plainText, CalculationRequest req, ParsedDocumentResult result)
    {
        DateOnly? birthDate = ExtractDateAfterLabel(
            plainText,
            @"(?:data\s+urodzenia\s+dziecka|przewidywana\s+data\s+porodu|data\s+porodu|poród\s+dn\.?|dziecko\s+urodziło\s+się|poród\s+pojedynczy\s+dnia|urodzeniem\s+dziecka(?:/dzieci)?|data\s+urodzenia)");

        DateOnly? leaveStart = ExtractDateAfterLabel(
            plainText,
            @"(?:urlop[u]?\s+macierzyński(?:ego)?(?:\s+od(?:\s+dnia)?)?|zasił(?:ek|ku)\s+macierzyński(?:ego)?.{0,75}?od(?:\s+dnia)?|okres\s+urlopu\s+macierzyńskiego\s+od(?:\s+dnia)?)");

        if (birthDate.HasValue)
        {
            req.EventDate = birthDate.Value;
            result.ExtractedFacts.Add($"Wykryta data porodu / zdarzenia: {birthDate.Value:yyyy-MM-dd}.");
        }

        if (leaveStart.HasValue)
        {
            if (!birthDate.HasValue ||
                (leaveStart.Value >= birthDate.Value.AddDays(-42) && leaveStart.Value <= birthDate.Value.AddDays(14)))
            {
                req.LeaveStartDate = leaveStart.Value;
                result.ExtractedFacts.Add($"Wykryta data rozpoczęcia urlopu macierzyńskiego: {leaveStart.Value:yyyy-MM-dd}.");

                if (!birthDate.HasValue)
                {
                    req.EventDate = leaveStart.Value;
                }
            }
        }

        DateOnly? referenceDate = req.LeaveStartDate ?? birthDate;
        if (!referenceDate.HasValue && req.MonthlySalaries.Count > 0)
        {
            var last = req.MonthlySalaries[^1];
            referenceDate = new DateOnly(last.Year, last.Month, 1).AddMonths(1);
        }

        if (referenceDate.HasValue && referenceDate.Value.Year < 2026)
        {
            decimal? historicalMin = referenceDate.Value.Year switch
            {
                2023 => referenceDate.Value.Month >= 7 ? 3600m : 3490m,
                2024 => referenceDate.Value.Month >= 7 ? 4300m : 4242m,
                2025 => 4666m,
                _ => null
            };

            if (historicalMin.HasValue)
            {
                req.MinimalWageOverrideForHistoricalCase = historicalMin.Value;
                result.ExtractedFacts.Add($"Sprawa archiwalna ({referenceDate.Value:yyyy-MM}): zastosowano historyczną płacę minimalną {historicalMin.Value:N2} zł.");
            }
        }

        return birthDate ?? req.LeaveStartDate;
    }

    private static DateOnly? ExtractDateAfterLabel(string text, string labelPattern)
    {
        var matches = Regex.Matches(
            text,
            labelPattern + @"[^0-9]{0,60}(?<d>\d{2}[\s.\-/]+\d{2}[\s.\-/]+\d{4}|\d{4}[\s.\-/]+\d{2}[\s.\-/]+\d{2}|(?<!\d)\d{8}(?!\d))",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        var candidates = new List<DateOnly>();
        foreach (Match m in matches)
        {
            if (TryParseDate(m.Groups["d"].Value, out var date) && date.Year is >= 2021 and <= 2028)
                candidates.Add(date);
        }

        if (candidates.Count == 0)
            return null;

        // Preferuj daty z lat 2023–2028 (odporność na literówkę OCR 2025 -> 2021 z ręcznego pisma)
        return candidates.FirstOrDefault(d => d.Year >= 2023) is { Year: >= 2023 } recent
            ? recent
            : candidates[0];
    }

    private static bool TryParseDate(string raw, out DateOnly result)
    {
        string trimmed = raw.Trim();
        if (Regex.IsMatch(trimmed, @"^\d{8}$"))
        {
            return DateOnly.TryParseExact(trimmed, "ddMMyyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out result)
                || DateOnly.TryParseExact(trimmed, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
        }

        string compact = Regex.Replace(trimmed, @"[\s.\-/]+", "-");
        string[] formats = ["dd-MM-yyyy", "d-M-yyyy", "dd-MM-yy", "d-M-yy", "yyyy-MM-dd", "yyyy-M-d"];
        return DateOnly.TryParseExact(compact, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    private static void ExtractMonthlySalaryRows(string text, CalculationRequest req, bool excludeVarByNote)
    {
        // Słownik pozwala nadpisać wcześniejszy druk Z-3 późniejszą korektą w tej samej teczce
        var monthMap = new Dictionary<(int Year, int Month), MonthlySalaryRecord>();

        var tableMatches = Regex.Matches(text, @"<table\b[^>]*>(.*?)</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        foreach (Match tableMatch in tableMatches)
        {
            int? lastYearInTable = null;
            var rowMatches = Regex.Matches(tableMatch.Groups[1].Value, @"<tr\b[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            foreach (Match rowMatch in rowMatches)
            {
                var cellMatches = Regex.Matches(rowMatch.Groups[1].Value, @"<t[dh]\b[^>]*>(.*?)</t[dh]>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                if (cellMatches.Count < 4) continue;

                var cells = cellMatches
                    .Select(c => CleanCellText(c.Groups[1].Value))
                    .ToList();

                TryParseSalaryRowCells(cells, ref lastYearInTable, excludeVarByNote, monthMap);
            }
        }

        int? lastYearMd = null;
        foreach (string line in text.Split('\n'))
        {
            if (!line.Contains('|')) continue;
            string trimmed = line.Trim();
            if (Regex.IsMatch(trimmed, @"^[\|\s:\-]+$")) continue;

            var cells = trimmed.Trim('|').Split('|').Select(CleanCellText).ToList();
            if (cells.Count < 4) continue;

            TryParseSalaryRowCells(cells, ref lastYearMd, excludeVarByNote, monthMap);
        }

        if (monthMap.Count == 0)
        {
            var lineRegex = new Regex(
                @"(?<year>202[0-7])\s+?(?<month>1[0-2]|0?[1-9])\s+(?<d1>\d{1,2})\s+(?<d2>\d{1,2})(?<rest>[^\n]{5,140})",
                RegexOptions.Multiline);

            foreach (Match m in lineRegex.Matches(StripHtmlToPlainText(text)))
            {
                int year = int.Parse(m.Groups["year"].Value);
                int month = int.Parse(m.Groups["month"].Value);
                int d1 = int.Parse(m.Groups["d1"].Value);
                int d2 = int.Parse(m.Groups["d2"].Value);
                if (d1 > 31 || d2 > 31 || Math.Max(d1, d2) == 0) continue;

                string rest = m.Groups["rest"].Value;
                var amounts = ExtractAmountsFromFragment(rest);
                if (amounts.Count == 0) continue;

                UpsertSalaryRow(monthMap, year, month, d1, d2, amounts, rest, excludeVarByNote);
            }
        }

        req.MonthlySalaries = monthMap.Values
            .OrderBy(r => r.Year)
            .ThenBy(r => r.Month)
            .TakeLast(12)
            .ToList();
    }

    private static void TryParseSalaryRowCells(
        List<string> cells,
        ref int? lastYear,
        bool excludeVarByNote,
        Dictionary<(int Year, int Month), MonthlySalaryRecord> monthMap)
    {
        int year = 0;
        int month = 0;
        int idx = 0;

        if (cells.Count >= 2 && TryParseYear(cells[0], out int y0) && TryParseMonth(cells[1], out int m1))
        {
            year = y0;
            month = m1;
            lastYear = year;
            idx = 2;
        }
        else if (TryParseCombinedYearMonth(cells[0], out int yc, out int mc))
        {
            year = yc;
            month = mc;
            lastYear = year;
            idx = 1;
        }
        else if (lastYear.HasValue && string.IsNullOrEmpty(cells[0]) && cells.Count >= 2 && TryParseMonth(cells[1], out int mEmpty))
        {
            year = lastYear.Value;
            month = mEmpty;
            idx = 2;
        }
        else if (lastYear.HasValue && TryParseMonth(cells[0], out int mRowspan) &&
                 cells.Count >= 3 && TryParseDayCount(cells[1], out _) && TryParseDayCount(cells[2], out _))
        {
            year = lastYear.Value;
            month = mRowspan;
            idx = 1;
        }
        else
        {
            return;
        }

        var dayValues = new List<int>();
        while (idx < cells.Count && dayValues.Count < 2)
        {
            string cell = cells[idx];
            if (string.IsNullOrEmpty(cell))
            {
                idx++;
                continue;
            }

            if (TryParseDayCount(cell, out int singleDay))
            {
                dayValues.Add(singleDay);
                idx++;
            }
            else
            {
                var multiDays = Regex.Matches(cell, @"\b(\d{1,2})\b")
                    .Select(x => int.Parse(x.Groups[1].Value))
                    .Where(d => d is >= 0 and <= 31)
                    .ToList();

                if (multiDays.Count == 2 && !cell.Contains(',') && !cell.Contains('.'))
                {
                    dayValues.AddRange(multiDays);
                    idx++;
                }
                else
                {
                    break;
                }
            }
        }

        if (dayValues.Count < 2) return;
        int d1 = dayValues[0];
        int d2 = dayValues[1];
        if (Math.Max(d1, d2) is <= 0 or > 31) return;

        var restCells = cells.Skip(idx).ToList();
        string restJoined = string.Join(" | ", restCells);
        var amounts = new List<decimal>();

        foreach (string cell in restCells)
        {
            if (string.IsNullOrWhiteSpace(cell)) continue;
            if (Regex.IsMatch(cell, @"^\s*(13[.,]7[13]|11[.,]26|9[.,]76)\s*%?\s*$") || cell.Contains('%'))
                continue;
            if (Regex.IsMatch(cell, @"\d{2}[.\-/]\d{2}[.\-/]\d{4}"))
                continue;

            amounts.AddRange(ExtractAmountsFromFragment(cell));
        }

        while (amounts.Count > 1 && (amounts[^1] == 13.71m || amounts[^1] == 13.73m))
        {
            amounts.RemoveAt(amounts.Count - 1);
        }

        if (amounts.Count == 0 || amounts.All(a => a <= 0)) return;

        UpsertSalaryRow(monthMap, year, month, d1, d2, amounts, restJoined, excludeVarByNote);
    }

    private static void UpsertSalaryRow(
        Dictionary<(int Year, int Month), MonthlySalaryRecord> monthMap,
        int year,
        int month,
        int d1,
        int d2,
        List<decimal> amounts,
        string restText,
        bool excludeVarByNote)
    {
        monthMap[(year, month)] = new MonthlySalaryRecord
        {
            Year = year,
            Month = month,
            DaysObligatory = Math.Max(d1, d2),
            DaysWorked = Math.Min(d1, d2),
            FixedGross = amounts[0],
            VariableProportionalGross = amounts.Count >= 3 ? amounts[1] : 0m,
            VariableActualGross = amounts.Count == 2 ? amounts[1] : (amounts.Count >= 3 ? amounts[2] : 0m),
            ContributionRatePercent = 13.71m,
            ExcludeVariableDueToEmployerNote = excludeVarByNote,
            AbsenceReason = restText.Contains("ZLA", StringComparison.OrdinalIgnoreCase) || restText.Contains("CHOR", StringComparison.OrdinalIgnoreCase) ? "ZLA" : null
        };
    }

    private static List<decimal> ExtractAmountsFromFragment(string fragment)
    {
        var list = new List<decimal>();
        string cleaned = Regex.Replace(fragment, @"13[.,]7[13]\s*%?", " ");

        var decMatches = Regex.Matches(cleaned, @"\b\d{1,2}[\s\u00A0]\d{3}[.,]\d{2}\b|\b\d{1,5}[.,]\d{2}\b");
        foreach (Match m in decMatches)
        {
            decimal v = ParseDecimalSafe(m.Value);
            if (v > 0 && v != 13.71m && v != 13.73m)
                list.Add(v);
        }

        if (list.Count == 0)
        {
            var intMatches = Regex.Matches(cleaned, @"\b\d{1,2}[\s\u00A0]\d{3}\b|\b\d{3,5}\b");
            foreach (Match m in intMatches)
            {
                decimal v = ParseDecimalSafe(m.Value);
                if (v >= 100m && v is not (>= 2020m and <= 2028m))
                    list.Add(v);
            }
        }

        return list;
    }

    private static bool TryParseYear(string cell, out int year)
    {
        year = 0;
        var m = Regex.Match(cell, @"^\s*(202[0-7])\s*$");
        return m.Success && int.TryParse(m.Groups[1].Value, out year);
    }

    private static bool TryParseMonth(string cell, out int month)
    {
        month = 0;
        var m = Regex.Match(cell, @"^\s*(1[0-2]|0?[1-9])\s*$");
        return m.Success && int.TryParse(m.Groups[1].Value, out month);
    }

    private static bool TryParseCombinedYearMonth(string cell, out int year, out int month)
    {
        year = 0;
        month = 0;

        var m1 = Regex.Match(cell, @"^\s*(202[0-7])[\s.\-/]+(1[0-2]|0?[1-9])\s*$");
        if (m1.Success)
        {
            year = int.Parse(m1.Groups[1].Value);
            month = int.Parse(m1.Groups[2].Value);
            return true;
        }

        var m2 = Regex.Match(cell, @"^\s*(1[0-2]|0?[1-9])[\s.\-/]+(202[0-7])\s*$");
        if (m2.Success)
        {
            month = int.Parse(m2.Groups[1].Value);
            year = int.Parse(m2.Groups[2].Value);
            return true;
        }

        return false;
    }

    private static bool TryParseDayCount(string cell, out int days)
    {
        days = 0;
        var m = Regex.Match(cell, @"^\s*(\d{1,2})\s*$");
        return m.Success && int.TryParse(m.Groups[1].Value, out days) && days is >= 0 and <= 31;
    }

    private static string StripHtmlToPlainText(string html)
    {
        string unwrapped = Regex.Replace(html, @"<!--(.*?)-->", " $1 ", RegexOptions.Singleline);
        string noTags = Regex.Replace(unwrapped, @"<[^>]+>", " ");
        string decoded = WebUtility.HtmlDecode(noTags).Replace('\u00A0', ' ');
        return Regex.Replace(decoded, @"[ \t]+", " ");
    }

    private static string CleanCellText(string raw)
    {
        string noTags = Regex.Replace(raw, @"<[^>]+>", " ");
        string decoded = WebUtility.HtmlDecode(noTags).Replace('\u00A0', ' ');
        return Regex.Replace(decoded, @"\s+", " ").Trim();
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
        string clean = raw.Replace(" ", "").Replace("\u00A0", "").Replace(",", ".");
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ? val : 0m;
    }

    private static void ApplyFatherOnlyScopeIfDetected(
        string plainText,
        bool isZurForm,
        bool hasZamForm,
        CalculationRequest req,
        ParsedDocumentResult result)
    {
        bool maternityChecked =
            Regex.IsMatch(plainText, @"(☑|\[x\]|:selected:|\bX\b)(?:(?!☐|:unselected:).){0,25}urlopu\s+macierzyńskiego", RegexOptions.IgnoreCase);
        bool parentalChecked =
            Regex.IsMatch(plainText, @"(☑|\[x\]|:selected:|\bX\b)(?:(?!☐|:unselected:).){0,25}urlopu\s+rodzicielskiego", RegexOptions.IgnoreCase);

        bool isPaternityOnly = !maternityChecked && !parentalChecked && (
            Regex.IsMatch(plainText, @"(☑|\[x\]|:selected:|\bX\b)(?:(?!☐|:unselected:).){0,25}urlopu\s+ojcowskiego", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(plainText, @"Wniosek\s+o\s+udzielenie\s+urlopu\s+ojcowskiego", RegexOptions.IgnoreCase));

        bool isFatherParentalOnly = !isPaternityOnly && isZurForm && !hasZamForm && !maternityChecked;

        if (!isPaternityOnly && !isFatherParentalOnly)
            return;

        if (isPaternityOnly)
        {
            req.ClaimScope = ClaimScope.PaternityLeaveOnly;
            req.SubmitFullApplicationWithin21Days = false;
            result.DetectedForms.Add("Wniosek o urlop ojcowski (art. 182³ KP — maks. 2 tyg., 100%)");

            var span = FindLeaveSpan(plainText, @"(?:zasiłek\s+macierzyński|urlopu\s+ojcowskiego)", minDays: 7, maxDays: 14);
            if (span.HasValue)
            {
                req.LeaveStartDate = span.Value.From;
                req.RequestedLeaveDaysOverride = span.Value.Days;
            }

            result.ExtractedFacts.Add(
                $"Wykryto wniosek wyłącznie o urlop ojcowski (ojciec — 100% podstawy, {(req.RequestedLeaveDaysOverride ?? 14)} dni) -> automatycznie przełączono harmonogram na tryb ojca.");
        }
        else
        {
            req.ClaimScope = ClaimScope.FatherParentalOnly;
            req.SubmitFullApplicationWithin21Days = false;

            var span = FindLeaveSpan(plainText, @"(?:urlopu\s+rodzicielskiego|zasiłek\s+macierzyński)", minDays: 7, maxDays: 224);
            if (span.HasValue)
            {
                req.LeaveStartDate = span.Value.From;
                req.RequestedLeaveDaysOverride = span.Value.Days;
            }

            result.ExtractedFacts.Add(
                $"Wykryto wniosek ZUR wyłącznie o urlop rodzicielski ojca (stawka 70%{(req.RequestedLeaveDaysOverride.HasValue ? $", {req.RequestedLeaveDaysOverride} dni" : "")}) -> automatycznie przełączono harmonogram na tryb ojca.");
        }
    }

    private static (DateOnly From, int Days)? FindLeaveSpan(string plainText, string labelPattern, int minDays, int maxDays)
    {
        var matches = Regex.Matches(
            plainText,
            labelPattern + @".{0,50}?(?:od\s+|za\s+okres:?\s*)?(?<from>" + DateTokenPattern + @")\s*(?:r\.?)?\s*(?:-|do)\s*(?<to>" + DateTokenPattern + @")",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match m in matches)
        {
            if (TryParseDate(m.Groups["from"].Value, out var from) &&
                TryParseDate(m.Groups["to"].Value, out var to) &&
                from.Year >= 2023)
            {
                int days = to.DayNumber - from.DayNumber + 1;
                if (days >= minDays && days <= maxDays)
                    return (from, days);
            }
        }
        return null;
    }
}