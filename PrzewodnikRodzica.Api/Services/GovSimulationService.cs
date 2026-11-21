using PrzewodnikRodzica.Core.Models;

namespace PrzewodnikRodzica.Api.Services;

public record SimulatedEzlaItem(string SeriesNumber, DateOnly From, DateOnly To, int Days, string Code, string DoctorNote);

public record SimulatedZusAccount(
    string PersonaId,
    string FullName,
    string MaskedPesel,
    string InsuranceTitleCode,
    string InsuranceTitleDescription,
    bool HasActiveSicknessInsurance,
    int ContinuousInsuranceDays,
    decimal SocialSecurityDebtPln,
    string DebtCertificateStatus,
    List<SimulatedEzlaItem> EzlaCertificates,
    CalculationRequest PrefilledCalculatorData);

public record DemoTestCaseItem(
    string Id,
    string Title,
    string Category,
    string Summary,
    string ExpectedOutcomeHint,
    CalculationRequest RequestData);

public class GovSimulationService
{
    public List<SimulatedZusAccount> GetMObywatelPersonas() => new()
    {
        new SimulatedZusAccount(
            "anna-uop",
            "Anna Kowalska",
            "940512*****",
            "0110 00",
            "Pracownik zatrudniony na umowę o pracę (mały płatnik: 14 ubezpieczonych)",
            true,
            920,
            0m,
            "Brak zaległości składkowych (zaświadczenie ZUS-EWN: czyste konto)",
            new()
            {
                new("ZZ-442190", new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 2), 33, "B", "Wynagrodzenie chorobowe (pracodawca, art. 92 KP)"),
                new("ZZ-498211", new DateOnly(2026, 8, 3), new DateOnly(2026, 10, 25), 84, "B", "Zasiłek chorobowy w ciąży 100% (ZUS)")
            },
            new CalculationRequest
            {
                EmploymentTitle = EmploymentTitle.Employee,
                EmployerInsuredCount = 14,
                FteFraction = 1.0m,
                AverageMonthlyGross = 7400m,
                IncludePregnancySickness = true,
                PlannedPregnancySickDays = 117,
                HasCodeBOnEzla = true,
                EventDate = new DateOnly(2026, 10, 26),
                SubmitFullApplicationWithin21Days = true,
                MotherMaternityWeeksTaken = 20,
                PrimaryParentParentalWeeks = 32,
                SecondParentInsured = true,
                SecondParentAverageMonthlyGross = 8600m,
                SecondParentParentalWeeks = 9,
                IncludePaternityLeave = true
            }),

        new SimulatedZusAccount(
            "marta-jdg-debt",
            "Marta Nowak (JDG)",
            "910824*****",
            "0510 00",
            "Osoba prowadząca pozarolniczą działalność gospodarczą (dobrowolne chorobowe)",
            true,
            410,
            184.50m,
            "UWAGA: Wykryto zaległość 184,50 zł (powyżej progu 1% płacy minimalnej = 48,06 zł)",
            new()
            {
                new("ZZ-512004", new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 30), 21, "B", "Wstrzymano wypłatę do spłaty zaległości (art. 2a)")
            },
            new CalculationRequest
            {
                EmploymentTitle = EmploymentTitle.SelfEmployed,
                HasVoluntarySicknessInsurance = true,
                ContinuousInsuranceDays = 410,
                AverageMonthlyGross = 9200m,
                JdgSocialSecurityDebt = 184.50m,
                IncludePregnancySickness = true,
                PlannedPregnancySickDays = 21,
                HasCodeBOnEzla = true,
                EventDate = new DateOnly(2026, 12, 15),
                SubmitFullApplicationWithin21Days = true,
                MotherMaternityWeeksTaken = 20,
                PrimaryParentParentalWeeks = 32,
                SecondParentInsured = true,
                SecondParentAverageMonthlyGross = 7800m,
                SecondParentParentalWeeks = 9,
                IncludePaternityLeave = true
            }),

        new SimulatedZusAccount(
            "katarzyna-adopcja",
            "Katarzyna Wiśniewska (Adopcja)",
            "880303*****",
            "0110 00",
            "Pracownik (umowa o pracę, duży pracodawca: 85 ubezpieczonych)",
            true,
            1460,
            0m,
            "Brak zaległości (płatnik rozlicza składki terminowo)",
            new(),
            new CalculationRequest
            {
                EmploymentTitle = EmploymentTitle.Employee,
                EmployerInsuredCount = 85,
                FteFraction = 1.0m,
                AverageMonthlyGross = 8800m,
                Scenario = ParenthoodScenario.Adoption,
                EventDate = new DateOnly(2026, 11, 25),
                ChildBirthDate = new DateOnly(2024, 5, 10),
                SubmitFullApplicationWithin21Days = true,
                MotherMaternityWeeksTaken = 20,
                PrimaryParentParentalWeeks = 32,
                SecondParentInsured = true,
                SecondParentAverageMonthlyGross = 9500m,
                SecondParentParentalWeeks = 9,
                IncludePaternityLeave = true
            }),

        new SimulatedZusAccount(
            "piotr-tata",
            "Piotr Zieliński (Tata przejmujący urlop)",
            "901118*****",
            "0110 00",
            "Pracownik (umowa o pracę, mały płatnik: 9 ubezpieczonych)",
            true,
            1800,
            0m,
            "Brak zaległości składkowych",
            new(),
            new CalculationRequest
            {
                EmploymentTitle = EmploymentTitle.Employee,
                EmployerInsuredCount = 9,
                FteFraction = 1.0m,
                AverageMonthlyGross = 6800m,
                EventDate = new DateOnly(2026, 11, 1),
                SubmitFullApplicationWithin21Days = false,
                MotherMaternityWeeksTaken = 14, // matka wraca do pracy po 14 tyg., ojciec przejmuje 6 tyg. macierzyńskiego
                PrimaryParentParentalWeeks = 20,
                SecondParentInsured = true,
                SecondParentAverageMonthlyGross = 10200m,
                SecondParentParentalWeeks = 21, // 9 wyłącznych + 12 ze wspólnej puli
                IncludePaternityLeave = true
            })
    };

    public SimulatedZusAccount? GetPersonaById(string personaId) =>
        GetMObywatelPersonas().FirstOrDefault(p => p.PersonaId.Equals(personaId, StringComparison.OrdinalIgnoreCase));

    public List<DemoTestCaseItem> GetDemoTestCases() => new()
    {
        new DemoTestCaseItem(
            "demo-combine-work",
            "Sprawa A: Łączenie urlopu rodzicielskiego z pracą na 1/2 etatu + wyrównanie do 1 000 zł",
            "Łączenie z pracą (Art. 182^1f § 7 KP)",
            "Pracownica na 0,63 etatu (mały płatnik <=20), wariant 100%/70%, wyłączony dodatek stażowy wg Uwag na Z-3, łączenie 32 tyg. rodzicielskiego + części wydłużonej z pracą na 1/2 etatu.",
            "Macierzyński 20 tyg. (100%), rodzicielski wydłużony do 64 tyg. (448 dni), automatyczne podwyższenie zasiłku netto do 1 000 zł (świadczenie rodzicielskie).",
            new CalculationRequest
            {
                EmploymentTitle = EmploymentTitle.Employee,
                EmployerInsuredCount = 10,
                FteFraction = 0.63m,
                EventDate = new DateOnly(2026, 3, 1),
                SubmitFullApplicationWithin21Days = false,
                MotherMaternityWeeksTaken = 20,
                PrimaryParentParentalWeeks = 32,
                SecondParentInsured = false,
                IncludePaternityLeave = false,
                CombineWorkWithParentalFte = 0.5m,
                ParentalWeeksCombinedWithWork = 32,
                CombineExtendedPartWithWork = true,
                MonthlySalaries = Enumerable.Range(1, 9).Select(m => new MonthlySalaryRecord
                {
                    Year = 2025,
                    Month = m,
                    DaysWorked = 21,
                    DaysObligatory = 21,
                    FixedGross = m <= 6 ? 3300m : 3410m,
                    VariableProportionalGross = 495m,
                    VariableActualGross = 330m,
                    ExcludeVariableDueToEmployerNote = true
                }).ToList()
            }),

        new DemoTestCaseItem(
            "demo-adoption",
            "Sprawa B: Przyjęcie dziecka na wychowanie (przysposobienie) + premia w Z-3",
            "Adopcja (Art. 183 KP + ZAS-26)",
            "Pracownik na pełnym etacie (mały płatnik <=20) przyjmuje 2-letnie dziecko na wychowanie. 12 miesięcy wynagrodzenia z podwyżką w połowie roku oraz premią pomniejszaną za okres zasiłku.",
            "Urlop na warunkach macierzyńskiego (20 tyg.) + rodzicielski (32 tyg.) w stawce 81,5% + 9 tyg. nieprzenoszalne dla drugiego rodzica w stawce 70%.",
            new CalculationRequest
            {
                EmploymentTitle = EmploymentTitle.Employee,
                EmployerInsuredCount = 12,
                FteFraction = 1.0m,
                Scenario = ParenthoodScenario.Adoption,
                EventDate = new DateOnly(2026, 1, 27),
                ChildBirthDate = new DateOnly(2024, 4, 20),
                SubmitFullApplicationWithin21Days = true,
                MotherMaternityWeeksTaken = 20,
                PrimaryParentParentalWeeks = 32,
                SecondParentInsured = true,
                SecondParentAverageMonthlyGross = 9000m,
                SecondParentParentalWeeks = 9,
                IncludePaternityLeave = false,
                MonthlySalaries = Enumerable.Range(1, 12).Select(m => new MonthlySalaryRecord
                {
                    Year = 2025,
                    Month = m,
                    DaysWorked = 21,
                    DaysObligatory = 21,
                    FixedGross = m <= 6 ? 8532.44m : 8959.06m,
                    VariableActualGross = m == 11 ? 760.63m : 0m
                }).ToList()
            }),

        new DemoTestCaseItem(
            "demo-pregnancy-to-maternity",
            "Sprawa C: Ciągłość chorobowego w ciąży (kod B) i zasiłku macierzyńskiego",
            "Ciąża -> Macierzyński (Art. 92 KP + ZAM)",
            "Pracownica (mały płatnik <=20), 115 dni niezdolności w ciąży (33 dni pracodawca + 82 dni ZUS), w tabeli Z-3 dwa miesiące z częściowym ZLA (>=50% czasu przepracowane) oraz premia zależna od nieobecności.",
            "100% zasiłek chorobowy w ciąży, brak ponownego przeliczania podstawy przy przejściu na macierzyński, wniosek ZAM w 21 dni = 81,5%.",
            new CalculationRequest
            {
                EmploymentTitle = EmploymentTitle.Employee,
                EmployerInsuredCount = 15,
                FteFraction = 1.0m,
                IncludePregnancySickness = true,
                PlannedPregnancySickDays = 115,
                HasCodeBOnEzla = true,
                EventDate = new DateOnly(2026, 11, 13),
                SubmitFullApplicationWithin21Days = true,
                MotherMaternityWeeksTaken = 20,
                PrimaryParentParentalWeeks = 32,
                SecondParentInsured = false,
                IncludePaternityLeave = false,
                MonthlySalaries = new()
                {
                    new() { Year = 2025, Month = 7, DaysWorked = 23, DaysObligatory = 23, FixedGross = 4666m, VariableActualGross = 200m },
                    new() { Year = 2025, Month = 8, DaysWorked = 18, DaysObligatory = 21, FixedGross = 4666m, VariableActualGross = 250m, AbsenceReason = "ZLA" },
                    new() { Year = 2025, Month = 9, DaysWorked = 11, DaysObligatory = 21, FixedGross = 4666m, VariableActualGross = 250m, AbsenceReason = "ZLA" },
                    new() { Year = 2025, Month = 10, DaysWorked = 23, DaysObligatory = 23, FixedGross = 4666m, VariableActualGross = 250m },
                    new() { Year = 2025, Month = 11, DaysWorked = 19, DaysObligatory = 19, FixedGross = 4666m, VariableActualGross = 250m },
                    new() { Year = 2025, Month = 12, DaysWorked = 20, DaysObligatory = 20, FixedGross = 4666m, VariableActualGross = 250m },
                    new() { Year = 2026, Month = 1, DaysWorked = 21, DaysObligatory = 21, FixedGross = 4806m, VariableActualGross = 250m },
                    new() { Year = 2026, Month = 2, DaysWorked = 20, DaysObligatory = 20, FixedGross = 4806m, VariableActualGross = 250m },
                    new() { Year = 2026, Month = 3, DaysWorked = 21, DaysObligatory = 21, FixedGross = 4806m, VariableActualGross = 250m },
                    new() { Year = 2026, Month = 4, DaysWorked = 21, DaysObligatory = 21, FixedGross = 4806m, VariableActualGross = 250m },
                    new() { Year = 2026, Month = 5, DaysWorked = 20, DaysObligatory = 20, FixedGross = 4806m, VariableActualGross = 250m },
                    new() { Year = 2026, Month = 6, DaysWorked = 20, DaysObligatory = 20, FixedGross = 4806m, VariableActualGross = 250m },
                }
            }),

        new DemoTestCaseItem(
            "demo-premature-za-zyciem",
            "Sprawa D: Wcześniak (27 tydz., 950 g) + szpital 62 dni + zaświadczenie „Za życiem”",
            "Sytuacja szczególna (Art. 180^2 KP)",
            "Dziecko urodzone w 27. tygodniu ciąży (masa 950 g), 62 dni hospitalizacji (zaokrąglenie w górę = 9 tygodni uzupełniającego urlopu) oraz zaświadczenie „Za życiem” (65 tyg. rodzicielskiego).",
            "Aktywacja stonowanego trybu wsparcia, +9 tyg. uzupełniającego (100%), wydłużony urlop rodzicielski 65 tyg. oraz lista fundacji charytatywnych.",
            new CalculationRequest
            {
                EmploymentTitle = EmploymentTitle.Employee,
                EmployerInsuredCount = 45,
                FteFraction = 1.0m,
                AverageMonthlyGross = 8200m,
                EventDate = new DateOnly(2026, 6, 1),
                HasZaZyciemCertificate = true,
                CalculateSupplementaryMaternity = true,
                GestationWeekAtBirth = 27,
                LowestBirthWeightGrams = 950,
                HospitalizationDaysWithinLimit = 62,
                SubmitFullApplicationWithin21Days = true,
                MotherMaternityWeeksTaken = 20,
                PrimaryParentParentalWeeks = 56, // 65 - 9 wyłącznych dla drugiego rodzica
                SecondParentInsured = true,
                SecondParentAverageMonthlyGross = 9000m,
                SecondParentParentalWeeks = 9,
                IncludePaternityLeave = true
            })
    };

    public string AnswerPueQuestionDeterministic(string question)
    {
        string q = question.ToLowerInvariant();

        if (q.Contains("zadłuż") || q.Contains("zaległ") || q.Contains("dług") || q.Contains("jdg"))
            return "Zgodnie z art. 2a ustawy zasiłkowej osoby opłacające składki za siebie (np. JDG) nie otrzymają zasiłku, jeśli w dniu powstania prawa mają zaległość przekraczającą 1% minimalnego wynagrodzenia (48,06 zł w 2026 r.). Spłata całości w ciągu 6 miesięcy odblokowuje wypłatę (ZUS wypłaca do 60 dni od spłaty).";

        if (q.Contains("21 dni") || q.Contains("81,5") || q.Contains("81.5") || q.Contains("stawk"))
            return "Wniosek o pełny wymiar urlopu macierzyńskiego i rodzicielskiego złożony w ciągu 21 dni od porodu/przyjęcia dziecka daje uśrednioną stawkę 81,5% przez cały okres (wyjątek: 9 niezbywalnych tygodni drugiego rodzica zawsze wynosi 70%). Bez tego wniosku obowiązuje wariant 100% za macierzyński i 70% za rodzicielski.";

        if (q.Contains("zur") || q.Contains("zao") || q.Contains("z-3b") || q.Contains("zam") || q.Contains("formularz"))
            return "Od 24 października 2026 r. formularze ZUR, ZAO oraz Z-3b zostały zlikwidowane. Wszystkie wnioski o zasiłek macierzyński, rodzicielski i ojcowski składa się na jednym zintegrowanym druku ZAM (a dla zasiłku chorobowego na JDG — na druku ZAS-53).";

        if (q.Contains("wcześniak") || q.Contains("uzupełniając") || q.Contains("szpital"))
            return "Uzupełniający urlop macierzyński (art. 180^2 KP) przysługuje bezpośrednio po urlopie macierzyńskim (100% podstawy): do 15 tygodni dla dzieci <28 tyg. ciąży lub ≤1000 g oraz do 8 tygodni dla dzieci 28–37 tyg. lub donoszonych hospitalizowanych min. 2 dni między 5. a 28. dniem. Niepełny tydzień zaokrągla się w górę.";

        if (q.Contains("martw") || q.Contains("poronien") || q.Contains("zgon") || q.Contains("pogrzeb"))
            return "W razie urodzenia martwego dziecka lub zgonu przed 8. tygodniem życia przysługuje 8 tygodni zasiłku macierzyńskiego (100% podstawy, min. 7 dni od zgonu) oraz zasiłek pogrzebowy 7 000 zł (druk Z-12, termin 12 mies.). Wystarczy akt urodzenia z adnotacją lub zaświadczenie lekarza ginekologa/położnej.";

        if (q.Contains("akt urodzenia") || q.Contains("usc"))
            return "Jeżeli płatnikiem zasiłku jest ZUS (np. JDG lub mały pracodawca do 20 osób), nie musisz dostarczać papierowego odpisu aktu urodzenia — wystarczy podać dane dziecka we wniosku, a ZUS sam pobierze odpis z Rejestru Stanu Cywilnego (§ 13 ust. 2 rozporządzenia z 27.09.2026 r.).";

        return "Twoje pytanie zostało zarejestrowane w symulacji PUE/eZUS. W indywidualnych sprawach skontaktuj się z Centrum Kontaktu Klientów ZUS (tel. 22 560 16 00, e-mail: mama@zus.pl lub e-wizyta na www.zus.pl/e-wizyta).";
    }
}