using PrzewodnikRodzica.Core.Models;
using PrzewodnikRodzica.Core.Services;
using Xunit;

namespace PrzewodnikRodzica.Tests;

public class BenefitEngineTests
{
    private readonly BenefitEngine _engine = new();
    private readonly BenefitParameters _params2026 = new();

    [Fact]
    public void Case1_AgnieszkaWa05_CombiningParentalWithHalfTimeWork_AndTopUpTo1000()
    {
        // Dane ze sprawy 05: 0.63 etatu, ZUS (<=20), 9 mies. 2023-01..09, dodatki wyłączone w Uwagach,
        // wariant 100%/70%, łączenie rodzicielskiego (32 tyg.) z pracą 1/2 etatu + łączenie wydłużonej części
        var req = new CalculationRequest
        {
            EmploymentTitle = EmploymentTitle.Employee,
            EmployerInsuredCount = 10,
            FteFraction = 0.63m,
            EventDate = new DateOnly(2023, 11, 28),
            LeaveStartDate = new DateOnly(2023, 11, 22),
            SubmitFullApplicationWithin21Days = false, // 100% / 70%
            MotherMaternityWeeksTaken = 20,
            PrimaryParentParentalWeeks = 32,
            SecondParentInsured = false,
            IncludePaternityLeave = false,
            CombineWorkWithParentalFte = 0.5m,
            ParentalWeeksCombinedWithWork = 32,
            CombineExtendedPartWithWork = true, // art. 182^1f § 7 KP -> 32 + 32 = 64 tyg.
            MinimalWageOverrideForHistoricalCase = 3600m,
            MonthlySalaries = new()
            {
                new() { Year = 2023, Month = 1, DaysWorked = 21, DaysObligatory = 21, FixedGross = 3100m, VariableProportionalGross = 434m, VariableActualGross = 310m, ExcludeVariableDueToEmployerNote = true },
                new() { Year = 2023, Month = 2, DaysWorked = 20, DaysObligatory = 20, FixedGross = 3300m, VariableProportionalGross = 495m, VariableActualGross = 330m, ExcludeVariableDueToEmployerNote = true },
                new() { Year = 2023, Month = 3, DaysWorked = 23, DaysObligatory = 23, FixedGross = 3300m, VariableProportionalGross = 495m, VariableActualGross = 330m, ExcludeVariableDueToEmployerNote = true },
                new() { Year = 2023, Month = 4, DaysWorked = 19, DaysObligatory = 19, FixedGross = 3300m, VariableProportionalGross = 495m, VariableActualGross = 330m, ExcludeVariableDueToEmployerNote = true },
                new() { Year = 2023, Month = 5, DaysWorked = 21, DaysObligatory = 21, FixedGross = 3300m, VariableProportionalGross = 495m, VariableActualGross = 330m, ExcludeVariableDueToEmployerNote = true },
                new() { Year = 2023, Month = 6, DaysWorked = 21, DaysObligatory = 21, FixedGross = 3300m, VariableProportionalGross = 495m, VariableActualGross = 330m, ExcludeVariableDueToEmployerNote = true },
                new() { Year = 2023, Month = 7, DaysWorked = 21, DaysObligatory = 21, FixedGross = 3410m, VariableProportionalGross = 511.50m, VariableActualGross = 341m, ExcludeVariableDueToEmployerNote = true },
                new() { Year = 2023, Month = 8, DaysWorked = 22, DaysObligatory = 22, FixedGross = 3410m, VariableProportionalGross = 511.50m, VariableActualGross = 341m, ExcludeVariableDueToEmployerNote = true },
                new() { Year = 2023, Month = 9, DaysWorked = 21, DaysObligatory = 21, FixedGross = 3410m, VariableProportionalGross = 511.50m, VariableActualGross = 341m, ExcludeVariableDueToEmployerNote = true },
            }
        };

        var res = _engine.Calculate(req, _params2026);

        Assert.True(res.IsEligibleForMaternityBenefit);
        Assert.Contains("ZUS", res.BenefitPayer);
        Assert.Equal(2860.03m, res.PrimaryParentBase.BenefitBase); // (29830 / 9) * 0.8629

        var mat = res.Timeline.Single(b => b.Id == "maternity-primary");
        Assert.Equal(new DateOnly(2024, 4, 9), mat.EndDate); // dokładnie 20 tyg. (z uwzględnieniem 29.02.2024)
        Assert.Equal(100m, mat.RatePercent);

        var parWork = res.Timeline.Single(b => b.Id == "parental-primary-work");
        Assert.Equal(448, parWork.Days); // 64 tygodnie (32 + 32 wydłużenia)
        Assert.True(parWork.ParentalFloorTopUpApplied); // netto < 1000 zł -> podwyższenie do świadczenia rodzicielskiego!
        Assert.Equal(1000.00m, parWork.MonthlyNetEstimate);
    }

    [Fact]
    public void Case2_AgnieszkaWar02_Adoption_12MonthsWithBonus_81Point5Percent()
    {
        // Dane ze sprawy 02: przysposobienie dziecka ur. 20.04.2024, urlop 27.01.2026 - 25.01.2027 (52 tyg.),
        // 6x 8532.44 + 6x 8959.06 + premia 760.63 w 11/2025
        var salaries = new List<MonthlySalaryRecord>();
        for (int m = 1; m <= 6; m++)
            salaries.Add(new() { Year = 2025, Month = m, DaysWorked = 21, DaysObligatory = 21, FixedGross = 8532.44m });
        for (int m = 7; m <= 12; m++)
            salaries.Add(new()
            {
                Year = 2025,
                Month = m,
                DaysWorked = 21,
                DaysObligatory = 21,
                FixedGross = 8959.06m,
                VariableActualGross = m == 11 ? 760.63m : 0m
            });

        var req = new CalculationRequest
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
            SecondParentParentalWeeks = 9,
            IncludePaternityLeave = false,
            MonthlySalaries = salaries
        };

        var res = _engine.Calculate(req, _params2026);

        Assert.True(res.IsEligibleForMaternityBenefit);
        Assert.Equal(7601.40m, res.PrimaryParentBase.BenefitBase); // (105709.63 / 12) * 0.8629

        var mat = res.Timeline.Single(b => b.Id == "maternity-primary");
        var par = res.Timeline.Single(b => b.Id == "parental-primary");
        var secondPar = res.Timeline.Single(b => b.Id == "parental-second");

        Assert.Equal(new DateOnly(2026, 6, 15), mat.EndDate);
        Assert.Equal(new DateOnly(2027, 1, 25), par.EndDate);
        Assert.Equal(81.5m, mat.RatePercent);
        Assert.Equal(81.5m, par.RatePercent);
        Assert.Equal(70m, secondPar.RatePercent); // 9 tyg. drugiego rodzica zawsze 70%
    }

    [Fact]
    public void Case3_AlicjaTa2_PregnancySicknessAndMaternity_WithZlaMonths()
    {
        // Dane ze sprawy 2: chorobowe w ciąży (33 dni pracodawca + 82 dni ZUS = 115 dni), następnie macierzyński + rodzicielski
        // Tabela Z-3 (2024-07..2025-06): 6x 4300 + 6x 4666 + premia 192 (07/24) + 11x 250; miesiące 08/24 (18/21) i 09/24 (11/21) >= 50%
        var salaries = new List<MonthlySalaryRecord>
        {
            new() { Year = 2024, Month = 7, DaysWorked = 23, DaysObligatory = 23, FixedGross = 4300m, VariableActualGross = 192m },
            new() { Year = 2024, Month = 8, DaysWorked = 18, DaysObligatory = 21, FixedGross = 4300m, VariableActualGross = 250m, AbsenceReason = "ZLA" },
            new() { Year = 2024, Month = 9, DaysWorked = 11, DaysObligatory = 21, FixedGross = 4300m, VariableActualGross = 250m, AbsenceReason = "ZLA" },
            new() { Year = 2024, Month = 10, DaysWorked = 23, DaysObligatory = 23, FixedGross = 4300m, VariableActualGross = 250m },
            new() { Year = 2024, Month = 11, DaysWorked = 19, DaysObligatory = 19, FixedGross = 4300m, VariableActualGross = 250m },
            new() { Year = 2024, Month = 12, DaysWorked = 20, DaysObligatory = 20, FixedGross = 4300m, VariableActualGross = 250m },
            new() { Year = 2025, Month = 1, DaysWorked = 21, DaysObligatory = 21, FixedGross = 4666m, VariableActualGross = 250m },
            new() { Year = 2025, Month = 2, DaysWorked = 20, DaysObligatory = 20, FixedGross = 4666m, VariableActualGross = 250m },
            new() { Year = 2025, Month = 3, DaysWorked = 21, DaysObligatory = 21, FixedGross = 4666m, VariableActualGross = 250m },
            new() { Year = 2025, Month = 4, DaysWorked = 21, DaysObligatory = 21, FixedGross = 4666m, VariableActualGross = 250m },
            new() { Year = 2025, Month = 5, DaysWorked = 20, DaysObligatory = 20, FixedGross = 4666m, VariableActualGross = 250m },
            new() { Year = 2025, Month = 6, DaysWorked = 20, DaysObligatory = 20, FixedGross = 4666m, VariableActualGross = 250m },
        };

        var req = new CalculationRequest
        {
            EmploymentTitle = EmploymentTitle.Employee,
            EmployerInsuredCount = 15,
            FteFraction = 1.0m,
            IncludePregnancySickness = true,
            PlannedPregnancySickDays = 115,
            HasCodeBOnEzla = true,
            EventDate = new DateOnly(2025, 11, 13),
            SubmitFullApplicationWithin21Days = true,
            MotherMaternityWeeksTaken = 20,
            PrimaryParentParentalWeeks = 32,
            SecondParentInsured = false,
            IncludePaternityLeave = false,
            MinimalWageOverrideForHistoricalCase = 4666m,
            MonthlySalaries = salaries
        };

        var res = _engine.Calculate(req, _params2026);

        Assert.Equal(4079.94m, res.PrimaryParentBase.BenefitBase); // (56738 / 12) * 0.8629
        Assert.Equal(0, res.PrimaryParentBase.MonthsExcludedUnderHalfTime); // 11/21 > 50%

        Assert.NotNull(res.PregnancySickness);
        Assert.Equal(33, res.PregnancySickness!.EmployerPaidDays);
        Assert.Equal(82, res.PregnancySickness.ZusPaidDays);
        Assert.Equal(100m, res.PregnancySickness.RatePercent);

        var par = res.Timeline.Single(b => b.Id == "parental-primary");
        Assert.Equal(new DateOnly(2026, 11, 11), par.EndDate);
    }

    [Fact]
    public void PrematureBaby_Under28Weeks_GetsSupplementaryMaternityRoundedUpToMax15Weeks()
    {
        var req = new CalculationRequest
        {
            EmploymentTitle = EmploymentTitle.Employee,
            AverageMonthlyGross = 8000m,
            EventDate = new DateOnly(2026, 5, 1),
            CalculateSupplementaryMaternity = true,
            GestationWeekAtBirth = 26,
            LowestBirthWeightGrams = 920,
            HospitalizationDaysWithinLimit = 58 // 58 / 7 = 8.28 -> zaokrąglenie w górę = 9 tygodni
        };

        var res = _engine.Calculate(req, _params2026);

        Assert.Equal(9, res.SupplementaryMaternityWeeks);
        Assert.True(res.IsSensitiveSituationMode);
        Assert.NotEmpty(res.CharitySupport);
        Assert.Contains(res.Timeline, b => b.Id == "supplementary-maternity" && b.RatePercent == 100m);
    }

    [Fact]
    public void SelfEmployed_WithDebtAboveThreshold_AndBaseAbove250PercentCap()
    {
        var req = new CalculationRequest
        {
            EmploymentTitle = EmploymentTitle.SelfEmployed,
            HasVoluntarySicknessInsurance = true,
            ContinuousInsuranceDays = 60, // < 90 dni dla chorobowego
            AverageMonthlyGross = 30000m, // > limitu 23 550 zł
            JdgSocialSecurityDebt = 120.50m, // > 48.06 zł (1% płacy minimalnej 2026)
            IncludePregnancySickness = true,
            PlannedPregnancySickDays = 30
        };

        var res = _engine.Calculate(req, _params2026);

        Assert.False(res.IsEligibleForMaternityBenefit);
        Assert.True(res.PrimaryParentBase.VoluntaryMaxCapApplied);
        Assert.Equal(20321.30m, res.PrimaryParentBase.BenefitBase);
        Assert.Contains(res.Risks, r => r.Code == "JDG_DEBT_BLOCK" && r.Severity == RiskSeverity.Critical);
        Assert.False(res.PregnancySickness!.IsEligible);
    }

    [Fact]
    public void StillbirthScenario_ActivatesSensitiveMode_8Weeks100Percent_And7000FuneralAllowance()
    {
        var req = new CalculationRequest
        {
            EmploymentTitle = EmploymentTitle.Employee,
            AverageMonthlyGross = 6000m,
            Scenario = ParenthoodScenario.StillbirthOrChildDeath,
            EventDate = new DateOnly(2026, 6, 10)
        };

        var res = _engine.Calculate(req, _params2026);

        Assert.True(res.IsSensitiveSituationMode);
        Assert.Equal(7000.00m, res.FuneralAllowanceAmount);
        Assert.NotEmpty(res.CharitySupport);
        var block = Assert.Single(res.Timeline);
        Assert.Equal(56, block.Days);
        Assert.Equal(100m, block.RatePercent);
    }

    [Fact]
    public void DeterministicParser_ExtractsZ3TableAndUwagiExclusionCorrectly()
    {
        string sampleMarkdownFromDocIntelligence = """
            ZUS Z-3 Zaświadczenie płatnika składek
            Jest zatrudniony Od 2023-01-01 w wymiarze czasu pracy 0,63
            Czy zgłosiłeś do ubezpieczenia chorobowego :selected: do 20 ubezpieczonych
            | 2023 | 1 | 21 | 21 | | 3100.00 | 434.00 | 310.00 | 13.71% |
            | 2023 | 2 | 20 | 20 | | 3300.00 | 495.00 | 330.00 | 13.71% |
            Uwagi: DODATEK STAŻOWY I FUNKCYJNY JEST WYPŁACANY MIMO NIEZDOLNOŚCI DO PRACY I URLOPU MACIERZYŃSKIEGO, A TYM SAMYM NIE PODLEGA ZALICZENIU DO PODSTAWY WYMIARU ZASIŁKU.
            """;

        var parser = new ZusDocumentParser();
        var parsed = parser.ParseExtractedText(sampleMarkdownFromDocIntelligence);

        Assert.True(parsed.HasConfirmedInsurance);
        Assert.Equal(EmploymentTitle.Employee, parsed.MappedRequest.EmploymentTitle);
        Assert.Equal(0.63m, parsed.MappedRequest.FteFraction);
        Assert.Equal(2, parsed.MappedRequest.MonthlySalaries.Count);
        Assert.All(parsed.MappedRequest.MonthlySalaries, r => Assert.True(r.ExcludeVariableDueToEmployerNote));
    }
}