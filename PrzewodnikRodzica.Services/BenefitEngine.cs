using PrzewodnikRodzica.Core.Models;

namespace PrzewodnikRodzica.Core.Services;

public class BenefitEngine
{
    private readonly BenefitBaseCalculator _baseCalculator = new();

    public CalculationResult Calculate(CalculationRequest request, BenefitParameters parameters)
    {
        var result = new CalculationResult
        {
            OfficialLinks = CharitySupportCatalog.GetOfficialLinks(),
            IsSensitiveSituationMode = request.Scenario == ParenthoodScenario.StillbirthOrChildDeath
                                       || request.HasZaZyciemCertificate
                                       || request.CalculateSupplementaryMaternity
        };

        // 1. Podstawa wymiaru
        result.PrimaryParentBase = _baseCalculator.Calculate(request, parameters);
        if (request.SecondParentInsured && request.SecondParentAverageMonthlyGross > 0)
        {
            result.SecondParentBase = _baseCalculator.CalculateSimpleBase(request.SecondParentAverageMonthlyGross, parameters);
        }

        // 2. Wskazanie płatnika zasiłku (art. 61 ustawy zasiłkowej)
        DetermineBenefitPayer(request, parameters, result);

        // 3. Uprawnienie do ubezpieczenia chorobowego na dzień zdarzenia
        bool hasActiveSicknessInsurance = IsCoveredBySicknessInsurance(request);
        bool postTerminationException = request.EmploymentTitle == EmploymentTitle.Employee &&
            request.TerminationReason is SpecialTerminationReason.EmployerBankruptcyOrLiquidation
                or SpecialTerminationReason.EmployerDeath
                or SpecialTerminationReason.UnlawfulTerminationCourtConfirmed
                or SpecialTerminationReason.FixedTermExtendedToBirth;

        bool jdgDebtBlocksBenefit = request.EmploymentTitle == EmploymentTitle.SelfEmployed
                                    && request.JdgSocialSecurityDebt > parameters.JdgDebtThresholdAmount;

        // 4. Kalkulacja zasiłku chorobowego w ciąży (jeśli włączona)
        if (request.IncludePregnancySickness)
        {
            result.PregnancySickness = CalculatePregnancySickness(request, parameters, result.PrimaryParentBase, hasActiveSicknessInsurance, jdgDebtBlocksBenefit);
        }

        // 5. Kalkulacja zasiłku opiekuńczego (jeśli włączona)
        if (request.IncludeCareAllowance)
        {
            result.CareAllowance = CalculateCareAllowance(request, parameters, result.PrimaryParentBase, hasActiveSicknessInsurance && !jdgDebtBlocksBenefit);
        }

        // 6. Ocena prawa do zasiłku macierzyńskiego
        if ((!hasActiveSicknessInsurance && !postTerminationException) ||
            request.TerminationReason == SpecialTerminationReason.StandardTerminationBeforeBirth)
        {
            result.IsEligibleForMaternityBenefit = false;
            result.RecommendKosiniakoweFallback = true;
            result.EligibilityHeadline = "Brak prawa do zasiłku macierzyńskiego z ZUS — przysługuje świadczenie rodzicielskie (1 000 zł/mies.)";

            // Jeżeli ojciec jest ubezpieczony, nadal może skorzystać z urlopu rodzicielskiego i ojcowskiego!
            if (request.SecondParentInsured && result.SecondParentBase != null && request.Scenario != ParenthoodScenario.StillbirthOrChildDeath)
            {
                BuildFatherOnlyTimeline(request, parameters, result);
            }
        }
        else if (jdgDebtBlocksBenefit)
        {
            result.IsEligibleForMaternityBenefit = false;
            result.EligibilityHeadline = $"Prawo do zasiłku ZAWIESZONE z powodu zadłużenia składek ({request.JdgSocialSecurityDebt:N2} zł > próg {parameters.JdgDebtThresholdAmount:N2} zł)";
            BuildMainTimeline(request, parameters, result); // Pokazujemy symulację kwot po spłacie zadłużenia
        }
        else
        {
            result.IsEligibleForMaternityBenefit = true;
            result.EligibilityHeadline = request.Scenario == ParenthoodScenario.StillbirthOrChildDeath
                ? "Przysługuje zasiłek macierzyński w trybie szczególnym (100% podstawy) oraz zasiłek pogrzebowy"
                : "Przysługuje prawo do zasiłku (brak okresu wyczekiwania)";

            BuildMainTimeline(request, parameters, result);
        }

        // 7. Podsumowania, Radar Ryzyk, Dokumenty, Fundacje
        result.TotalCombinedLeaveDays = result.Timeline.Sum(t => t.Days);
        result.TotalEstimatedBenefitsGross = Round2(
            result.Timeline.Sum(t => t.TotalBlockGross) +
            (result.PregnancySickness?.TotalEstimatedGross ?? 0m) +
            (result.CareAllowance?.TotalGrossAmount ?? 0m));

        BuildRiskAlerts(request, parameters, result, hasActiveSicknessInsurance, jdgDebtBlocksBenefit);
        BuildRequiredDocuments(request, parameters, result);

        if (result.IsSensitiveSituationMode)
        {
            var allCharities = CharitySupportCatalog.GetAll();

            if (request.Scenario == ParenthoodScenario.StillbirthOrChildDeath)
            {
                result.CharitySupport = allCharities
                    .Where(c => c.Name.Contains("Ernesta", StringComparison.OrdinalIgnoreCase)
                             || c.Name.Contains("Tęczowy", StringComparison.OrdinalIgnoreCase)
                             || c.Name.Contains("Hospicjum", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                bool showPremature = request.CalculateSupplementaryMaternity;
                bool showZaZyciem = request.HasZaZyciemCertificate;

                result.CharitySupport = allCharities
                    .Where(c =>
                        (showPremature && c.Name.Contains("Wcześniak", StringComparison.OrdinalIgnoreCase)) ||
                        (showZaZyciem && (c.Name.Contains("Gajusz", StringComparison.OrdinalIgnoreCase)
                                       || c.Name.Contains("Hospicjum", StringComparison.OrdinalIgnoreCase))))
                    .ToList();
            }
        }

        return result;
    }

    private static bool IsCoveredBySicknessInsurance(CalculationRequest request)
    {
        return request.EmploymentTitle switch
        {
            EmploymentTitle.Employee => true,
            EmploymentTitle.MandateContract => request.HasVoluntarySicknessInsurance,
            EmploymentTitle.SelfEmployed => request.HasVoluntarySicknessInsurance,
            EmploymentTitle.ContractForSpecificWork => false,
            EmploymentTitle.UninsuredOrUlgaNaStart => false,
            _ => false
        };
    }

    private static void DetermineBenefitPayer(CalculationRequest request, BenefitParameters parameters, CalculationResult result)
    {
        bool postTermination = request.TerminationReason is not SpecialTerminationReason.None;

        if (request.EmploymentTitle == EmploymentTitle.SelfEmployed)
        {
            result.BenefitPayer = "Zakład Ubezpieczeń Społecznych (ZUS)";
            result.PayerExplanation = "Osobom prowadzącym pozarolniczą działalność gospodarczą zasiłki zawsze ustala i wypłaca ZUS (art. 61 ust. 1 pkt 2 lit. b).";
        }
        else if (postTermination)
        {
            result.BenefitPayer = "Zakład Ubezpieczeń Społecznych (ZUS)";
            result.PayerExplanation = "Za okres po ustaniu ubezpieczenia chorobowego (w tym przy przedłużeniu umowy do dnia porodu) zasiłek wypłaca ZUS.";
        }
        else if (request.EmploymentTitle is EmploymentTitle.Employee or EmploymentTitle.MandateContract)
        {
            if (request.EmployerInsuredCount > parameters.LargePayerInsuredThreshold)
            {
                result.BenefitPayer = "Płatnik składek (Pracodawca / Zleceniodawca)";
                result.PayerExplanation = $"Płatnik zgłasza powyżej {parameters.LargePayerInsuredThreshold} ubezpieczonych (wg stanu na 30 listopada poprzedniego roku) — sam ustala i wypłaca zasiłek w terminach wypłaty wynagrodzeń.";
            }
            else
            {
                result.BenefitPayer = "Zakład Ubezpieczeń Społecznych (ZUS)";
                result.PayerExplanation = $"Mały płatnik (do {parameters.LargePayerInsuredThreshold} ubezpieczonych) — wnioski składasz u pracodawcy/zleceniodawcy, który przekazuje je wraz z Z-3/Z-3a do ZUS w ciągu 7 dni. ZUS wypłaca zasiłek w terminie do 30 dni.";
            }
        }
        else
        {
            result.BenefitPayer = "Brak płatnika zasiłku z ZUS (Gmina / MOPS dla świadczenia rodzicielskiego)";
            result.PayerExplanation = "Brak tytułu do ubezpieczenia chorobowego — właściwy organ dla świadczenia rodzicielskiego (1 000 zł/mies.) to urząd gminy/miasta.";
        }
    }

    private static PregnancySicknessSummary CalculatePregnancySickness(
        CalculationRequest request,
        BenefitParameters parameters,
        BaseCalculationSummary baseSummary,
        bool hasActiveSicknessInsurance,
        bool jdgDebtBlocksBenefit)
    {
        var summary = new PregnancySicknessSummary
        {
            MaxBenefitPeriodDays = request.HasCodeBOnEzla ? parameters.PregnancyBenefitPeriodDays : parameters.StandardBenefitPeriodDays,
            RatePercent = (request.HasCodeBOnEzla ? parameters.PregnancySicknessRate : parameters.StandardSicknessRate) * 100m
        };

        if (!hasActiveSicknessInsurance)
        {
            summary.IsEligible = false;
            summary.EligibilityReason = "Brak podlegania ubezpieczeniu chorobowemu.";
            return summary;
        }

        int requiredWaitingDays = request.EmploymentTitle == EmploymentTitle.Employee
            ? parameters.MandatoryWaitingPeriodDays
            : parameters.VoluntaryWaitingPeriodDays;

        if (!request.ExemptFromWaitingPeriod && request.ContinuousInsuranceDays < requiredWaitingDays)
        {
            summary.IsEligible = false;
            summary.EligibilityReason = $"Nie upłynął okres wyczekiwania ({request.ContinuousInsuranceDays} z wymaganych {requiredWaitingDays} dni nieprzerwanego ubezpieczenia — art. 4 ustawy zasiłkowej).";
            return summary;
        }

        if (jdgDebtBlocksBenefit)
        {
            summary.IsEligible = false;
            summary.EligibilityReason = $"Zadłużenie składek ({request.JdgSocialSecurityDebt:N2} zł) przekracza próg 1% płacy minimalnej ({parameters.JdgDebtThresholdAmount:N2} zł). Wypłata nastąpi po spłacie całości (maks. 6 mies.).";
            return summary;
        }

        summary.IsEligible = true;
        summary.EligibilityReason = request.HasCodeBOnEzla
            ? "Spełniono warunki — zasiłek chorobowy w ciąży (kod B) wynosi 100% podstawy przez maks. 270 dni. Okresy choroby sprzed ciąży nie wliczają się do limitu po przerwie."
            : "Uwaga: brak kodu literowego „B” na e-ZLA obniża świadczenie do 80% podstawy i skraca limit do 182 dni.";

        int effectiveDays = Math.Min(request.PlannedPregnancySickDays, summary.MaxBenefitPeriodDays);

        if (request.EmploymentTitle == EmploymentTitle.Employee)
        {
            int employerLimit = request.IsOver50YearsOld
                ? parameters.EmployerSickPayDays50Plus
                : parameters.EmployerSickPayDaysUnder50;
            int remainingEmployerDays = Math.Max(0, employerLimit - request.PriorNonPregnancySickDaysInYear);

            summary.EmployerPaidDays = Math.Min(effectiveDays, remainingEmployerDays);
            summary.ZusPaidDays = Math.Max(0, effectiveDays - summary.EmployerPaidDays);
        }
        else
        {
            summary.EmployerPaidDays = 0;
            summary.ZusPaidDays = effectiveDays;
        }

        decimal rate = summary.RatePercent / 100m;
        summary.DailyGrossAmount = Round2(baseSummary.DailyBase100Percent * rate);
        summary.TotalEstimatedGross = Round2(summary.DailyGrossAmount * effectiveDays);

        if (request.TerminationReason is SpecialTerminationReason.EmployerBankruptcyOrLiquidation or SpecialTerminationReason.EmployerDeath)
        {
            summary.EligibleForAllowanceInMaternityAmount = true;
            summary.SpecialNote = "Przysługuje zasiłek w wysokości zasiłku macierzyńskiego (100% podstawy) do dnia porodu, jeżeli pracodawca nie zapewnił innego zatrudnienia (art. 30 ust. 3).";
        }
        else if (request.NeedsRehabilitationBenefit || request.PlannedPregnancySickDays > summary.MaxBenefitPeriodDays)
        {
            summary.SpecialNote = $"Po wyczerpaniu {summary.MaxBenefitPeriodDays} dni okresu zasiłkowego można wystąpić o świadczenie rehabilitacyjne (100% podstawy w ciąży, art. 19 ust. 1) na wniosek ZNp-7.";
        }

        return summary;
    }

    private static CareAllowanceSummary CalculateCareAllowance(
        CalculationRequest request,
        BenefitParameters parameters,
        BaseCalculationSummary baseSummary,
        bool isEligible)
    {
        int maxDays = request.CareRecipientType switch
        {
            0 => 60, // dziecko do 14 lat (lub zdrowe do 8 lat)
            1 => 30, // chore dziecko niepełnosprawne 14-18 lat
            _ => 14  // inny członek rodziny / dziecko > 14 lat
        };

        int days = Math.Min(Math.Max(0, request.CareAllowanceDays), maxDays);
        decimal daily = Round2(baseSummary.DailyBase100Percent * parameters.CareAllowanceRate);

        return new CareAllowanceSummary
        {
            IsEligible = isEligible,
            MaxAnnualDays = maxDays,
            CalculatedDays = isEligible ? days : 0,
            RatePercent = parameters.CareAllowanceRate * 100m,
            DailyGrossAmount = daily,
            TotalGrossAmount = isEligible ? Round2(daily * days) : 0m,
            FormCode = request.CareRecipientType == 2 ? "Z-15B" : "Z-15A"
        };
    }

    private static void BuildMainTimeline(CalculationRequest request, BenefitParameters parameters, CalculationResult result)
    {
        DateOnly cursor = request.LeaveStartDate ?? request.EventDate;

        // Scenariusz 1: Urodzenie martwego dziecka lub zgon dziecka (art. 180^1 KP)
        if (request.Scenario == ParenthoodScenario.StillbirthOrChildDeath)
        {
            int days;
            if (request.ChildDeathDate == null || request.ChildDeathDate.Value < request.EventDate.AddDays(parameters.StillbirthMaternityWeeks * 7))
            {
                int daysFromBirth = parameters.StillbirthMaternityWeeks * 7; // 56 dni
                int daysFromDeathMin = request.ChildDeathDate.HasValue
                    ? (request.ChildDeathDate.Value.DayNumber - request.EventDate.DayNumber) + parameters.PostChildDeathMinDays
                    : daysFromBirth;
                days = Math.Max(daysFromBirth, daysFromDeathMin);
            }
            else
            {
                days = parameters.PostChildDeathMinDays;
                cursor = request.ChildDeathDate.Value;
            }

            result.Timeline.Add(CreateBlock(
                "stillbirth-maternity",
                "Zasiłek macierzyński po stracie dziecka",
                "Matka",
                cursor,
                days,
                parameters.MaternityRate,
                0m,
                result.PrimaryParentBase,
                parameters,
                "Art. 180^1 Kodeksu pracy (100% podstawy)",
                "slate"));

            result.FuneralAllowanceAmount = parameters.FuneralAllowanceStillbirth;
            return;
        }

        // Scenariusz 1b: Wniosek wyłącznie o urlop ojcowski (ojciec jako wnioskodawca)
        if (request.ClaimScope == ClaimScope.PaternityLeaveOnly)
        {
            int maxPaternityDays = parameters.PaternityWeeks * 7; // 14 dni
            int days = request.RequestedLeaveDaysOverride is > 0
                ? Math.Min(request.RequestedLeaveDaysOverride.Value, maxPaternityDays)
                : maxPaternityDays;

            result.Timeline.Add(CreateBlock(
                "paternity-applicant",
                days < maxPaternityDays ? $"Urlop ojcowski (część — {days} dni)" : "Urlop ojcowski",
                "Ojciec / Wnioskodawca",
                cursor,
                days,
                parameters.PaternityRate,
                0m,
                result.PrimaryParentBase,
                parameters,
                "Art. 182^3 KP (100% podstawy, wyłączne prawo ojca — maks. 14 dni w ciągu 12 mies. od porodu)",
                "sky"));
            return;
        }

        // Scenariusz 1c: Wniosek wyłącznie o część urlopu rodzicielskiego ojca (np. druk ZUR)
        if (request.ClaimScope == ClaimScope.FatherParentalOnly)
        {
            int pool = GetParentalPoolWeeks(request, parameters, false);
            int maxFatherDays = (pool - parameters.NonTransferableParentalWeeksPerParent) * 7;
            int days = request.RequestedLeaveDaysOverride is > 0
                ? Math.Clamp(request.RequestedLeaveDaysOverride.Value, 7, maxFatherDays)
                : Math.Max(request.SecondParentParentalWeeks, 9) * 7;

            result.Timeline.Add(CreateBlock(
                "parental-father-applicant",
                "Urlop rodzicielski (część ojca)",
                "Ojciec / Wnioskodawca",
                cursor,
                days,
                parameters.SecondParentExclusiveParentalRate, // 70%
                0m,
                result.PrimaryParentBase,
                parameters,
                "Art. 182^1a § 4 KP / Art. 31 ust. 3f ustawy zasiłkowej (70%)",
                "cyan"));
            return;
        }

        // Scenariusz 2: Poród / Adopcja / Rodzina zastępcza
        int baseMaternityWeeks = GetMaternityWeeksByChildrenCount(request.ChildrenCount, parameters);
        bool isMinAdoptionAgeCapMode = false;

        if (request.Scenario is ParenthoodScenario.Adoption or ParenthoodScenario.FosterCareNonProfessional && request.ChildBirthDate.HasValue)
        {
            int maxAgeYears = request.Scenario == ParenthoodScenario.Adoption
                ? 14
                : (request.HasSchoolDeferralDecision ? 10 : 7);

            DateOnly ageLimitDate = request.ChildBirthDate.Value.AddYears(maxAgeYears);
            int daysUntilAgeCap = ageLimitDate.DayNumber - cursor.DayNumber;

            if (daysUntilAgeCap < baseMaternityWeeks * 7)
            {
                isMinAdoptionAgeCapMode = true;
                baseMaternityWeeks = parameters.AdoptionMinMaternityWeeks; // Art. 183 § 3 KP: gwarancja 9 tygodni
            }
        }

        // Walidacja podziału urlopu macierzyńskiego (matka min. 14 tygodni po porodzie)
        int motherMatWeeks = Math.Clamp(request.MotherMaternityWeeksTaken, parameters.MotherMandatoryPostBirthWeeks, baseMaternityWeeks);
        if (baseMaternityWeeks < parameters.MotherMandatoryPostBirthWeeks)
            motherMatWeeks = baseMaternityWeeks; // przypadek 9 tyg. adopcji

        int fatherTransferredMatWeeks = request.SecondParentInsured ? Math.Max(0, baseMaternityWeeks - motherMatWeeks) : 0;
        if (!request.SecondParentInsured)
            motherMatWeeks = baseMaternityWeeks;

        decimal primaryRate = request.SubmitFullApplicationWithin21Days
            ? parameters.UnifiedMaternityAndParentalRate
            : parameters.MaternityRate;

        decimal primaryParentalRate = request.SubmitFullApplicationWithin21Days
            ? parameters.UnifiedMaternityAndParentalRate
            : parameters.ParentalRate;

        string matLabel = request.Scenario == ParenthoodScenario.BiologicalBirth
            ? "Urlop macierzyński"
            : "Urlop na warunkach urlopu macierzyńskiego";

        // Blok A1: Macierzyński głównego rodzica
        var matBlock = CreateBlock(
            "maternity-primary",
            matLabel,
            "Matka / Wnioskodawca",
            cursor,
            motherMatWeeks * 7,
            primaryRate,
            0m,
            result.PrimaryParentBase,
            parameters,
            request.Scenario == ParenthoodScenario.BiologicalBirth ? "Art. 180 KP / Art. 31 ustawy zasiłkowej" : "Art. 183 KP",
            "emerald");
        result.Timeline.Add(matBlock);
        cursor = matBlock.EndDate.AddDays(1);

        // Blok A2: Przejęta część urlopu macierzyńskiego przez ojca
        if (fatherTransferredMatWeeks > 0 && result.SecondParentBase != null)
        {
            var fatherMatBlock = CreateBlock(
                "maternity-second",
                "Przejęta część urlopu macierzyńskiego",
                "Drugi rodzic (Ojciec)",
                cursor,
                fatherTransferredMatWeeks * 7,
                primaryRate,
                0m,
                result.SecondParentBase,
                parameters,
                "Art. 180 § 4 KP (po 14 tyg. matki)",
                "teal");
            result.Timeline.Add(fatherMatBlock);
            cursor = fatherMatBlock.EndDate.AddDays(1);
        }

        // Blok B: Uzupełniający urlop macierzyński (art. 180^2 KP — zawsze 100%)
        if (request.CalculateSupplementaryMaternity)
        {
            int suppWeeks = CalculateSupplementaryMaternityWeeks(request);
            result.SupplementaryMaternityWeeks = suppWeeks;
            if (suppWeeks > 0)
            {
                var suppBlock = CreateBlock(
                    "supplementary-maternity",
                    "Uzupełniający urlop macierzyński (wcześniak / szpital)",
                    "Matka / Wnioskodawca",
                    cursor,
                    suppWeeks * 7,
                    parameters.SupplementaryMaternityRate,
                    0m,
                    result.PrimaryParentBase,
                    parameters,
                    "Art. 180^2 KP (100% podstawy, jednorazowo)",
                    "amber");
                result.Timeline.Add(suppBlock);
                cursor = suppBlock.EndDate.AddDays(1);
            }
        }

        // Blok C: Urlop rodzicielski — pula łączna i podział między rodziców
        int totalParentalPool = GetParentalPoolWeeks(request, parameters, isMinAdoptionAgeCapMode);
        int maxSingleParentParental = totalParentalPool - parameters.NonTransferableParentalWeeksPerParent;
        int primaryParentalWeeks = Math.Clamp(request.PrimaryParentParentalWeeks, 0, maxSingleParentParental);

        if (primaryParentalWeeks > 0)
        {
            bool canCombineWithWork = request.EmploymentTitle == EmploymentTitle.Employee
                                      && request.CombineWorkWithParentalFte > 0m
                                      && request.CombineWorkWithParentalFte <= 0.5m
                                      && request.ParentalWeeksCombinedWithWork > 0;

            if (!canCombineWithWork)
            {
                var parBlock = CreateBlock(
                    "parental-primary",
                    "Urlop rodzicielski",
                    "Matka / Wnioskodawca",
                    cursor,
                    primaryParentalWeeks * 7,
                    primaryParentalRate,
                    0m,
                    result.PrimaryParentBase,
                    parameters,
                    "Art. 182^1a KP / Art. 31 ustawy zasiłkowej",
                    "blue");
                result.Timeline.Add(parBlock);
                cursor = parBlock.EndDate.AddDays(1);
            }
            else
            {
                // Łączenie urlopu rodzicielskiego z pracą (art. 182^1e - 182^1f KP)
                int combinedWeeks = Math.Min(request.ParentalWeeksCombinedWithWork, primaryParentalWeeks);
                int nonCombinedWeeks = primaryParentalWeeks - combinedWeeks;

                if (nonCombinedWeeks > 0)
                {
                    var pureParBlock = CreateBlock(
                        "parental-primary-pure",
                        "Urlop rodzicielski (bez pracy)",
                        "Matka / Wnioskodawca",
                        cursor,
                        nonCombinedWeeks * 7,
                        primaryParentalRate,
                        0m,
                        result.PrimaryParentBase,
                        parameters,
                        "Art. 182^1a KP",
                        "blue");
                    result.Timeline.Add(pureParBlock);
                    cursor = pureParBlock.EndDate.AddDays(1);
                }

                // Proporcjonalne wydłużenie (art. 182^1f § 2 i § 7 KP)
                decimal fte = request.CombineWorkWithParentalFte;
                decimal baseExtraWeeks = combinedWeeks * fte;
                decimal finalExtraWeeks = request.CombineExtendedPartWithWork
                    ? baseExtraWeeks / (1m - fte)
                    : baseExtraWeeks;

                int maxExtendedLimitWeeks = GetMaxExtendedParentalWeeks(request, parameters);
                int maxAllowedExtraDays = Math.Max(0, (maxExtendedLimitWeeks - totalParentalPool) * 7);
                int extraDays = Math.Min((int)Math.Floor(finalExtraWeeks * 7m), maxAllowedExtraDays); // art. 182^1f § 4: niepełny dzień pomija się
                result.ExtendedParentalExtraDays = extraDays;

                int combinedDays = combinedWeeks * 7 + (request.CombineExtendedPartWithWork ? extraDays : 0);
                var workParBlock = CreateBlock(
                    "parental-primary-work",
                    $"Urlop rodzicielski + praca ({fte:0.##} etatu)",
                    "Matka / Wnioskodawca",
                    cursor,
                    combinedDays,
                    primaryParentalRate,
                    fte,
                    result.PrimaryParentBase,
                    parameters,
                    request.CombineExtendedPartWithWork
                        ? "Art. 182^1f § 7 KP (wydłużenie łączone z pracą)"
                        : "Art. 182^1f § 2 KP (proporcjonalne wydłużenie)",
                    "indigo");
                result.Timeline.Add(workParBlock);
                cursor = workParBlock.EndDate.AddDays(1);

                if (!request.CombineExtendedPartWithWork && extraDays > 0)
                {
                    var extOnlyBlock = CreateBlock(
                        "parental-primary-extended",
                        "Wydłużona część urlopu rodzicielskiego (bez pracy)",
                        "Matka / Wnioskodawca",
                        cursor,
                        extraDays,
                        primaryParentalRate,
                        0m,
                        result.PrimaryParentBase,
                        parameters,
                        "Art. 182^1f § 5 KP",
                        "blue");
                    result.Timeline.Add(extOnlyBlock);
                    cursor = extOnlyBlock.EndDate.AddDays(1);
                }
            }
        }

        // Blok D: Część urlopu rodzicielskiego drugiego rodzica (9 tyg. nieprzenoszalne zawsze 70%)
        if (request.SecondParentInsured && result.SecondParentBase != null && request.SecondParentParentalWeeks > 0)
        {
            int remainingPool = Math.Max(0, totalParentalPool - primaryParentalWeeks);
            int secondWeeks = Math.Min(request.SecondParentParentalWeeks, remainingPool);
            if (secondWeeks > 0)
            {
                var secondParBlock = CreateBlock(
                    "parental-second",
                    "Urlop rodzicielski — część nieprzenoszalna / drugiego rodzica",
                    "Drugi rodzic (Ojciec)",
                    cursor,
                    secondWeeks * 7,
                    parameters.SecondParentExclusiveParentalRate, // Art. 31 ust. 3f: zawsze 70% za 9 tyg.
                    0m,
                    result.SecondParentBase,
                    parameters,
                    "Art. 182^1a § 4 KP / Art. 31 ust. 3f (70%)",
                    "cyan");
                result.Timeline.Add(secondParBlock);
            }
        }

        // Blok E: Urlop ojcowski (wyłączne prawo ojca — 2 tygodnie, 100%)
        if (request.IncludePaternityLeave && request.SecondParentInsured && result.SecondParentBase != null)
        {
            DateOnly paternityStart = request.EventDate;
            var paternityBlock = CreateBlock(
                "paternity",
                "Urlop ojcowski (do wykorzystania w ciągu 12 mies.)",
                "Drugi rodzic (Ojciec)",
                paternityStart,
                parameters.PaternityWeeks * 7,
                parameters.PaternityRate,
                0m,
                result.SecondParentBase,
                parameters,
                "Art. 182^3 KP (100% podstawy, wyłączne prawo ojca)",
                "sky");
            result.Timeline.Add(paternityBlock);
        }
    }

    private static void BuildFatherOnlyTimeline(CalculationRequest request, BenefitParameters parameters, CalculationResult result)
    {
        if (result.SecondParentBase == null) return;

        // Ojciec ma prawo do urlopu rodzicielskiego (pula pomniejszona o 9 tyg. nieprzenoszalnych matki) oraz ojcowskiego
        int pool = GetParentalPoolWeeks(request, parameters, false);
        int fatherMaxParental = pool - parameters.NonTransferableParentalWeeksPerParent;
        int weeks = Math.Clamp(request.SecondParentParentalWeeks, 0, fatherMaxParental);

        if (weeks > 0)
        {
            result.Timeline.Add(CreateBlock(
                "parental-father-uninsured-mother",
                "Urlop rodzicielski dla ubezpieczonego ojca (matka nieubezpieczona)",
                "Drugi rodzic (Ojciec)",
                request.EventDate,
                weeks * 7,
                parameters.ParentalRate,
                0m,
                result.SecondParentBase,
                parameters,
                "Art. 31 ust. 3h ustawy zasiłkowej (70%, finansowany z dotacji państwa)",
                "cyan"));
        }

        if (request.IncludePaternityLeave)
        {
            result.Timeline.Add(CreateBlock(
                "paternity-only",
                "Urlop ojcowski",
                "Drugi rodzic (Ojciec)",
                request.EventDate,
                parameters.PaternityWeeks * 7,
                parameters.PaternityRate,
                0m,
                result.SecondParentBase,
                parameters,
                "Art. 182^3 KP (100% podstawy)",
                "sky"));
        }
    }

    public static int CalculateSupplementaryMaternityWeeks(CalculationRequest request)
    {
        if (request.HospitalizationDaysWithinLimit <= 0)
            return 0;

        int rawWeeks = (int)Math.Ceiling(request.HospitalizationDaysWithinLimit / 7.0);

        // Przypadek 1: < 28 tyg. ciąży LUB masa <= 1000 g -> maks. 15 tygodni
        if ((request.GestationWeekAtBirth.HasValue && request.GestationWeekAtBirth.Value < 28) ||
            (request.LowestBirthWeightGrams.HasValue && request.LowestBirthWeightGrams.Value <= 1000))
        {
            return Math.Min(rawWeeks, 15);
        }

        // Przypadek 2: 28..36 tyg. ciąży i masa > 1000 g -> maks. 8 tygodni
        if (request.GestationWeekAtBirth.HasValue && request.GestationWeekAtBirth.Value < 37)
        {
            return Math.Min(rawWeeks, 8);
        }

        // Przypadek 3: >= 37 tyg. ciąży — warunek: min. 2 kolejne dni w szpitalu między 5. a 28. dniem po porodzie
        if (request.TermBabyHospitalizedMin2DaysBetweenDay5And28)
        {
            return Math.Min(rawWeeks, 8);
        }

        return 0;
    }

    private static TimelineBlock CreateBlock(
        string id,
        string title,
        string parentRole,
        DateOnly startDate,
        int days,
        decimal rate,
        decimal workFte,
        BaseCalculationSummary baseSummary,
        BenefitParameters parameters,
        string legalBasis,
        string colorToken)
    {
        // Art. 31 ust. 4a: przy łączeniu z pracą zasiłek zmniejsza się proporcjonalnie do wymiaru pracy
        decimal effectiveLeaveFraction = 1m - workFte;
        decimal dailyGross = Round2(baseSummary.DailyBase100Percent * rate * effectiveLeaveFraction);
        decimal monthlyGross = Round2(dailyGross * 30m);
        decimal monthlyNet = Round2(monthlyGross * (1m - parameters.EstimatedPitAdvanceRate));

        bool topUpApplied = false;
        decimal monthlyTopUp = 0m;

        // Art. 31 ust. 3a: podwyższenie zasiłku netto do kwoty świadczenia rodzicielskiego (1000 zł)
        if (monthlyNet < parameters.ParentalBenefitFloorNet)
        {
            topUpApplied = true;
            monthlyTopUp = Round2(parameters.ParentalBenefitFloorNet - monthlyNet);
            monthlyNet = parameters.ParentalBenefitFloorNet;
        }

        decimal totalGross = Round2(dailyGross * days);

        return new TimelineBlock
        {
            Id = id,
            Title = title,
            ParentRole = parentRole,
            StartDate = startDate,
            EndDate = startDate.AddDays(Math.Max(0, days - 1)),
            Days = days,
            Weeks = Math.Round(days / 7m, 2),
            RatePercent = Round2(rate * 100m),
            WorkFteDuringLeave = workFte,
            DailyGrossAmount = dailyGross,
            MonthlyGrossEstimate = monthlyGross,
            MonthlyNetEstimate = monthlyNet,
            ParentalFloorTopUpApplied = topUpApplied,
            MonthlyTopUpAmount = monthlyTopUp,
            TotalBlockGross = totalGross,
            LegalBasis = legalBasis,
            ColorToken = colorToken
        };
    }

    private static int GetMaternityWeeksByChildrenCount(int count, BenefitParameters p) => count switch
    {
        <= 1 => p.MaternityWeeksSingle,
        2 => p.MaternityWeeksTwins,
        3 => p.MaternityWeeksTriplets,
        4 => p.MaternityWeeksQuadruplets,
        _ => p.MaternityWeeksQuintupletsPlus
    };

    private static int GetParentalPoolWeeks(CalculationRequest req, BenefitParameters p, bool minAdoptionMode)
    {
        if (minAdoptionMode)
            return req.HasZaZyciemCertificate ? p.ParentalWeeksMinAdoptionZaZyciem : p.ParentalWeeksMinAdoption;

        if (req.HasZaZyciemCertificate)
            return req.ChildrenCount <= 1 ? p.ParentalWeeksSingleZaZyciem : p.ParentalWeeksMultipleZaZyciem;

        return req.ChildrenCount <= 1 ? p.ParentalWeeksSingle : p.ParentalWeeksMultiple;
    }

    private static int GetMaxExtendedParentalWeeks(CalculationRequest req, BenefitParameters p)
    {
        if (req.HasZaZyciemCertificate)
            return req.ChildrenCount <= 1 ? p.MaxExtendedParentalWeeksSingleZaZyciem : p.MaxExtendedParentalWeeksMultipleZaZyciem;

        return req.ChildrenCount <= 1 ? p.MaxExtendedParentalWeeksSingle : p.MaxExtendedParentalWeeksMultiple;
    }

    private static void BuildRiskAlerts(
        CalculationRequest request,
        BenefitParameters parameters,
        CalculationResult result,
        bool hasActiveSicknessInsurance,
        bool jdgDebtBlocksBenefit)
    {
        if (!hasActiveSicknessInsurance)
        {
            result.Risks.Add(new RiskAlert
            {
                Code = "NO_SICKNESS_INSURANCE",
                Severity = RiskSeverity.Critical,
                Title = "Brak ubezpieczenia chorobowego na dzień porodu / zdarzenia",
                Description = "Osoby na uldze na start, umowie o dzieło, studenci <26 r.ż. na zleceniu lub niezgłoszone do dobrowolnego chorobowego nie nabędą prawa do zasiłku z ZUS.",
                LegalBasis = "Art. 1 i Art. 29 ustawy zasiłkowej",
                RecommendedAction = "Rozważ zgłoszenie do dobrowolnego ubezpieczenia chorobowego (ZUS ZUA — nie działa wstecz!) lub skorzystaj ze świadczenia rodzicielskiego 1 000 zł/mies."
            });
        }

        if (jdgDebtBlocksBenefit)
        {
            result.Risks.Add(new RiskAlert
            {
                Code = "JDG_DEBT_BLOCK",
                Severity = RiskSeverity.Critical,
                Title = "Zaległość w składkach ZUS blokuje wypłatę świadczeń",
                Description = $"Twoje zadłużenie ({request.JdgSocialSecurityDebt:N2} zł) przekracza dopuszczalny próg 1% płacy minimalnej ({parameters.JdgDebtThresholdAmount:N2} zł).",
                LegalBasis = "Art. 2a ustawy zasiłkowej",
                RecommendedAction = "Ureguluj całość zadłużenia nie później niż w ciągu 6 miesięcy od dnia powstania prawa do świadczenia — w przeciwnym razie prawo ulegnie przedawnieniu."
            });
        }

        if (request.ShortContractJustBeforePregnancy || request.SuddenBaseIncreaseInPregnancy)
        {
            result.Risks.Add(new RiskAlert
            {
                Code = "ZUS_CONTROL_RISK",
                Severity = RiskSeverity.Warning,
                Title = "Podwyższone ryzyko postępowania wyjaśniającego ZUS",
                Description = "Zawarcie umowy lub otwarcie działalności dla pozoru albo nagłe podwyższenie podstawy składek w okresie ciąży może skutkować kontrolą ZUS i wydaniem decyzji o niepodleganiu ubezpieczeniom lub obniżeniu podstawy.",
                LegalBasis = "Art. 83 ustawy o systemie ubezpieczeń społecznych / art. 58 i 83 Kodeksu cywilnego",
                RecommendedAction = "Zachowuj pełną dokumentację potwierdzającą faktyczne wykonywanie pracy/usług (maile, faktury, listy obecności, efekty pracy)."
            });
        }

        if (request.EmploymentTitle is EmploymentTitle.SelfEmployed or EmploymentTitle.MandateContract)
        {
            result.Risks.Add(new RiskAlert
            {
                Code = "VOLUNTARY_DEREGISTRATION_AFTER_MATERNITY",
                Severity = RiskSeverity.Warning,
                Title = "Automatyczne wyrejestrowanie z dobrowolnego chorobowego + brak ochrony z Kodeksu pracy",
                Description = "Pobieranie zasiłku macierzyńskiego automatycznie wyrejestrowuje Cię z dobrowolnego ubezpieczenia chorobowego. Dodatkowo nie obejmuje Cię kodeksowa ochrona przed rozwiązaniem umowy w ciąży.",
                LegalBasis = "Poradnik ZUS 2026 / Art. 177 KP",
                RecommendedAction = "Po zakończeniu zasiłku macierzyńskiego pamiętaj o ponownym zgłoszeniu do dobrowolnego chorobowego na druku ZUS ZUA."
            });
        }

        if (request.ClaimScope == ClaimScope.FullMaternityAndParental
            && request.SubmitFullApplicationWithin21Days
            && request.Scenario != ParenthoodScenario.StillbirthOrChildDeath)
        {
            result.Risks.Add(new RiskAlert
            {
                Code = "DEADLINE_21_DAYS",
                Severity = RiskSeverity.Info,
                Title = "Pilnuj 21-dniowego terminu na stawkę 81,5%",
                Description = "Aby otrzymać uśrednione 81,5% przez cały okres, wniosek o pełny wymiar trzeba złożyć w ciągu 21 dni od porodu / przyjęcia dziecka. Jeśli w 1. roku życia dziecka nie wykorzystasz ani jednego dnia rodzicielskiego, przysługuje wyrównanie za macierzyński do 100%.",
                LegalBasis = "Art. 30a i Art. 31 ust. 3–4 ustawy zasiłkowej",
                RecommendedAction = $"Złóż wniosek najpóźniej do {request.EventDate.AddDays(21):dd.MM.yyyy}."
            });
        }

        if (request.EmploymentTitle == EmploymentTitle.Employee && request.CombineWorkWithParentalFte > 0.5m)
        {
            result.Risks.Add(new RiskAlert
            {
                Code = "WORK_FTE_EXCEEDS_HALF",
                Severity = RiskSeverity.Critical,
                Title = "Przekroczony maksymalny wymiar pracy na urlopie rodzicielskim (maks. 1/2 etatu)",
                Description = "Łączenie urlopu rodzicielskiego z pracą u własnego pracodawcy jest dopuszczalne wyłącznie do 1/2 pełnego wymiaru czasu pracy.",
                LegalBasis = "Art. 182^1e § 1 Kodeksu pracy",
                RecommendedAction = "Obniż wymiar pracy do maks. 0,5 etatu lub złóż pismo o rezygnacji z urlopu rodzicielskiego (np. druk POG przy wstrzymaniu wypłaty w ZUS)."
            });
        }

        result.Risks.Add(new RiskAlert
        {
            Code = "CLAIM_LIMITATION_6M",
            Severity = RiskSeverity.Info,
            Title = "6-miesięczny termin przedawnienia roszczenia o zasiłek",
            Description = "Roszczenie o wypłatę zasiłku chorobowego, macierzyńskiego i opiekuńczego przedawnia się po 6 miesiącach od ostatniego dnia okresu, za który zasiłek przysługuje (3 lata przy błędzie ZUS/płatnika).",
            LegalBasis = "Art. 67 ustawy zasiłkowej",
            RecommendedAction = "Składaj dokumenty niezwłocznie przez eZUS lub u pracodawcy."
        });
    }

    private static void BuildRequiredDocuments(CalculationRequest request, BenefitParameters parameters, CalculationResult result)
    {
        bool zusIsPayer = result.BenefitPayer.Contains("ZUS");

        if (request.IncludePregnancySickness)
        {
            if (request.EmploymentTitle == EmploymentTitle.Employee)
            {
                result.RequiredDocuments.Add(new RequiredDocumentItem
                {
                    FormCode = "e-ZLA (kod B)",
                    Name = "Zaświadczenie lekarskie o niezdolności do pracy w ciąży",
                    SubmitTo = "Automatycznie trafia do ZUS i pracodawcy na PUE/eZUS",
                    Deadline = "Poinformuj pracodawcę o nieobecności (najpóźniej 2. dnia)",
                    Notes = zusIsPayer
                        ? "Mały pracodawca (<=20) przekazuje do ZUS formularz Z-3 (lub ZAS-12 na dalszy okres) w ciągu 7 dni."
                        : "Duży pracodawca (>20) wypłaca wynagrodzenie chorobowe i zasiłek samodzielnie."
                });
            }
            else if (request.EmploymentTitle == EmploymentTitle.SelfEmployed)
            {
                result.RequiredDocuments.Add(new RequiredDocumentItem
                {
                    FormCode = "ZAS-53",
                    Name = "Wniosek o zasiłek chorobowy (przedsiębiorca)",
                    SubmitTo = "ZUS (najwygodniej przez portal eZUS)",
                    Deadline = "Do 6 miesięcy od końca okresu zwolnienia",
                    Notes = "Uwaga: formularz Z-3b został zlikwidowany od 24 października 2026 r. — złóż wniosek ZAS-53."
                });
            }
            else if (request.EmploymentTitle == EmploymentTitle.MandateContract)
            {
                result.RequiredDocuments.Add(new RequiredDocumentItem
                {
                    FormCode = "Z-3a",
                    Name = "Zaświadczenie płatnika składek (dla zleceniobiorcy)",
                    SubmitTo = zusIsPayer ? "Zleceniodawca przesyła Z-3a do ZUS" : "Duży zleceniodawca wypłaca na bazie e-ZLA",
                    Deadline = "Niezwłocznie (płatnik ma 7 dni na wysyłkę do ZUS)",
                    Notes = "Pamiętaj o powstrzymaniu się od wykonywania pracy zarobkowej na L4."
                });
            }
        }

        if (request.Scenario == ParenthoodScenario.StillbirthOrChildDeath)
        {
            result.RequiredDocuments.Add(new RequiredDocumentItem
            {
                FormCode = "ZAM + Zaświadczenie lekarskie / Akt urodzenia",
                Name = "Wniosek o zasiłek macierzyński po martwym urodzeniu / zgonie",
                SubmitTo = zusIsPayer ? "ZUS (eZUS) lub przez pracodawcę (Z-3)" : "Pracodawca",
                Deadline = "Do 6 miesięcy od końca okresu zasiłku",
                Notes = "Wymagany odpis skrócony aktu urodzenia z adnotacją o martwym urodzeniu ALBO zaświadczenie lekarza ginekologa-położnika / położnej o martwym urodzeniu (bez względu na czas trwania ciąży)."
            });
            result.RequiredDocuments.Add(new RequiredDocumentItem
            {
                FormCode = "Z-12",
                Name = "Wniosek o zasiłek pogrzebowy (7 000 zł)",
                SubmitTo = "ZUS",
                Deadline = "Do 12 miesięcy od dnia pochówku",
                Notes = "Dołącz rachunki za pogrzeb (rodzicom przysługuje pełne 7 000 zł nawet przy niższych kosztach) oraz kartę zgonu / zaświadczenie o martwym urodzeniu."
            });
            return;
        }

        if (request.ClaimScope == ClaimScope.PaternityLeaveOnly)
        {
            result.RequiredDocuments.Add(new RequiredDocumentItem
            {
                FormCode = request.EmploymentTitle == EmploymentTitle.Employee ? "Wniosek KP / Z-3" : "ZAM",
                Name = "Wniosek o urlop ojcowski (do 2 tygodni, 100% podstawy)",
                SubmitTo = request.EmploymentTitle == EmploymentTitle.Employee
                    ? (zusIsPayer ? "Pracodawca -> ZUS (z drukiem Z-3)" : "Pracodawca")
                    : "Bezpośrednio do ZUS (eZUS)",
                Deadline = "Min. 7 dni przed rozpoczęciem, do ukończenia przez dziecko 12. miesiąca życia",
                Notes = "Urlop ojcowski można wykorzystać jednorazowo (14 dni) albo w 2 częściach po 7 dni (art. 182^3 KP). Od 24.10.2026 r. formularz ZAO został zastąpiony zintegrowanym wnioskiem ZAM."
            });
            return;
        }

        if (request.ClaimScope == ClaimScope.FatherParentalOnly)
        {
            result.RequiredDocuments.Add(new RequiredDocumentItem
            {
                FormCode = request.EmploymentTitle == EmploymentTitle.Employee ? "Wniosek KP / Z-3" : "ZAM",
                Name = "Wniosek o część urlopu rodzicielskiego dla ojca (70% podstawy)",
                SubmitTo = request.EmploymentTitle == EmploymentTitle.Employee
                    ? (zusIsPayer ? "Pracodawca -> ZUS (z drukiem Z-3)" : "Pracodawca")
                    : "Bezpośrednio do ZUS (eZUS)",
                Deadline = "Min. 21 dni przed rozpoczęciem (dla pracownika) lub przed startem zasiłku (JDG)",
                Notes = "9 tygodni z puli urlopu rodzicielskiego to wyłączna, nieprzenoszalna część drugiego rodzica (zawsze 70%). Od 24.10.2026 r. druk ZUR został zastąpiony przez ZAM."
            });
            return;
        }

        if (request.EmploymentTitle == EmploymentTitle.Employee)
        {
            result.RequiredDocuments.Add(new RequiredDocumentItem
            {
                FormCode = "Wniosek KP",
                Name = request.SubmitFullApplicationWithin21Days
                    ? "Jeden wniosek do pracodawcy o urlop macierzyński i rodzicielski (81,5%)"
                    : "Wniosek do pracodawcy o urlop macierzyński (100%)",
                SubmitTo = "Pracodawca (papierowo lub elektronicznie)",
                Deadline = request.SubmitFullApplicationWithin21Days
                    ? $"Do 21 dni od porodu/przyjęcia (do {request.EventDate.AddDays(21):dd.MM.yyyy})"
                    : "Na bieżąco (wniosek o urlop rodzicielski min. 21 dni przed startem)",
                Notes = zusIsPayer
                    ? "Pracodawca (<=20) przekazuje do ZUS formularz Z-3. Gdy płatnikiem jest ZUS, nie musisz dostarczać papierowego aktu urodzenia — ZUS pobierze go z USC po danych dziecka (§ 13 ust. 2 rozporządzenia)."
                    : "Duży pracodawca (>20) wymaga okazania skróconego odpisu aktu urodzenia do wglądu."
            });
        }
        else if (request.EmploymentTitle is EmploymentTitle.SelfEmployed or EmploymentTitle.MandateContract)
        {
            result.RequiredDocuments.Add(new RequiredDocumentItem
            {
                FormCode = "ZAM",
                Name = "Zintegrowany wniosek o zasiłek macierzyński (zastąpił ZUR i ZAO od 24.10.2026)",
                SubmitTo = request.EmploymentTitle == EmploymentTitle.SelfEmployed ? "Bezpośrednio do ZUS (eZUS)" : "Zleceniodawca / ZUS (z Z-3a)",
                Deadline = request.SubmitFullApplicationWithin21Days
                    ? $"Do 21 dni po porodzie (do {request.EventDate.AddDays(21):dd.MM.yyyy}) dla stawki 81,5%"
                    : "Przed rozpoczęciem korzystania z danej części zasiłku",
                Notes = "Formularze ZUR, ZAO oraz Z-3b zostały zlikwidowane 24.10.2026 r. — wszystkie wnioski macierzyńskie, rodzicielskie i ojcowskie składa się na jednym druku ZAM."
            });
        }

        if (request.Scenario is ParenthoodScenario.Adoption or ParenthoodScenario.FosterCareNonProfessional)
        {
            result.RequiredDocuments.Add(new RequiredDocumentItem
            {
                FormCode = "ZAS-26 + Orzeczenie sądu",
                Name = "Oświadczenie o przyjęciu dziecka na wychowanie + dokument sądowy",
                SubmitTo = zusIsPayer ? "ZUS (przez pracodawcę lub bezpośrednio)" : "Pracodawca",
                Deadline = "Wniosek o urlop na warunkach macierzyńskiego w ciągu 7 dni od przyjęcia dziecka (art. 183 § 6 KP)",
                Notes = request.Scenario == ParenthoodScenario.Adoption
                    ? "Dołącz zaświadczenie sądu opiekuńczego o dacie wystąpienia o przysposobienie z datą urodzenia dziecka."
                    : "Dołącz orzeczenie sądu o umieszczeniu w rodzinie zastępczej lub umowę ze starostą (+ decyzję o odroczeniu obowiązku szkolnego, jeśli dotyczy)."
            });
        }

        if (request.CalculateSupplementaryMaternity && result.SupplementaryMaternityWeeks > 0)
        {
            result.RequiredDocuments.Add(new RequiredDocumentItem
            {
                FormCode = "Wniosek + Zaświadczenie szpitalne",
                Name = "Wniosek o uzupełniający urlop macierzyński + zaświadczenie ze szpitala",
                SubmitTo = zusIsPayer ? "Pracodawca -> ZUS (lub ZAM)" : "Pracodawca",
                Deadline = "Co najmniej 21 dni przed końcem podstawowego urlopu macierzyńskiego",
                Notes = "Zaświadczenie ze szpitala musi zawierać tydzień ciąży / masę urodzeniową oraz okresy hospitalizacji. Dołącz oświadczenie drugiego rodzica o niekorzystaniu z tego urlopu."
            });
        }

        if (request.EmploymentTitle == EmploymentTitle.Employee && request.CombineWorkWithParentalFte is > 0 and <= 0.5m)
        {
            result.RequiredDocuments.Add(new RequiredDocumentItem
            {
                FormCode = "Wniosek § 16 KP",
                Name = "Wniosek o łączenie urlopu rodzicielskiego z pracą (do 1/2 etatu)",
                SubmitTo = "Pracodawca (i Z-3 do ZUS przy małym płatniku)",
                Deadline = "Co najmniej 21 dni przed rozpoczęciem wykonywania pracy",
                Notes = "Wskaż wymiar etatu oraz czy zamierzasz łączyć z pracą również część urlopu powstałą w wyniku wydłużenia."
            });
        }

        if (request.IncludePaternityLeave && request.SecondParentInsured)
        {
            result.RequiredDocuments.Add(new RequiredDocumentItem
            {
                FormCode = "Wniosek ojcowski / ZAM",
                Name = "Wniosek o 2-tygodniowy urlop ojcowski",
                SubmitTo = "Pracodawca ojca (lub ZUS na druku ZAM dla JDG)",
                Deadline = $"Min. 7 dni przed rozpoczęciem, najpóźniej przed {request.EventDate.AddYears(1):dd.MM.yyyy}",
                Notes = "Można podzielić maksymalnie na 2 części po 7 dni."
            });
        }
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}