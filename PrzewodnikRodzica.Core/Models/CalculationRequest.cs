namespace PrzewodnikRodzica.Core.Models;

public enum EmploymentTitle
{
    Employee = 0,              // Umowa o pracę (obowiązkowe chorobowe)
    MandateContract = 1,       // Umowa zlecenie (dobrowolne chorobowe)
    SelfEmployed = 2,          // JDG / pozarolnicza działalność (dobrowolne chorobowe)
    ContractForSpecificWork = 3, // Umowa o dzieło (brak ubezpieczenia chorobowego)
    UninsuredOrUlgaNaStart = 4 // Bezrobotny / student <26 na zleceniu / ulga na start
}

public enum ParenthoodScenario
{
    BiologicalBirth = 0,       // Urodzenie dziecka
    Adoption = 1,              // Przysposobienie (adopcja)
    FosterCareNonProfessional = 2, // Rodzina zastępcza niezawodowa
    StillbirthOrChildDeath = 3 // Urodzenie martwego dziecka lub zgon dziecka
}

public enum SpecialTerminationReason
{
    None = 0,
    EmployerBankruptcyOrLiquidation = 1,
    EmployerDeath = 2,
    UnlawfulTerminationCourtConfirmed = 3,
    FixedTermExtendedToBirth = 4,
    StandardTerminationBeforeBirth = 5
}

public enum ClaimScope
{
    FullMaternityAndParental = 0, // domyślnie — pełna wsteczna kompatybilność z testami xUnit i UI
    PaternityLeaveOnly = 1,       // wyłącznie urlop ojcowski (art. 182^3 KP, 100%, maks. 14 dni)
    FatherParentalOnly = 2        // wyłącznie część urlopu rodzicielskiego ojca (np. ZUR, 70%)
}

public class MonthlySalaryRecord
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int DaysWorked { get; set; }
    public int DaysObligatory { get; set; }
    public decimal FixedGross { get; set; }
    public decimal VariableProportionalGross { get; set; }
    public decimal VariableActualGross { get; set; }
    public decimal ContributionRatePercent { get; set; } = 13.71m;
    public bool ExcludeVariableDueToEmployerNote { get; set; }
    public string? AbsenceReason { get; set; }
}

public class CalculationRequest
{
    // 1. Sytuacja zawodowa i ubezpieczeniowa głównego rodzica (domyślnie matka / wnioskodawca)
    public EmploymentTitle EmploymentTitle { get; set; } = EmploymentTitle.Employee;
    public bool HasVoluntarySicknessInsurance { get; set; } = true;
    public int ContinuousInsuranceDays { get; set; } = 365;
    public bool ExemptFromWaitingPeriod { get; set; } // np. absolwent <90 dni, 10 lat stażu, wypadek w drodze
    public int EmployerInsuredCount { get; set; } = 15; // <=20 -> ZUS, >20 -> Płatnik
    public decimal FteFraction { get; set; } = 1.0m; // np. 1.0 lub 0.63
    public bool IsOver50YearsOld { get; set; }
    public SpecialTerminationReason TerminationReason { get; set; } = SpecialTerminationReason.None;

    // Zadłużenie JDG i flagi ryzyka
    public decimal JdgSocialSecurityDebt { get; set; }
    public bool SuddenBaseIncreaseInPregnancy { get; set; }
    public bool ShortContractJustBeforePregnancy { get; set; }

    // 2. Podstawa wynagrodzenia (albo tabela z Z-3, albo pojedyncza kwota średnia brutto)
    public decimal AverageMonthlyGross { get; set; } = 7000m;
    public List<MonthlySalaryRecord> MonthlySalaries { get; set; } = new();
    public decimal QuarterlyBonusesTotalLast4Quarters { get; set; }
    public decimal AnnualBonusLastYear { get; set; }
    public bool ContinueBaseFromPreviousBenefit { get; set; } // przerwa < 1 mies. (art. 43)
    public decimal? PreviousBenefitBaseNetOfZUS { get; set; }
    public decimal? MinimalWageOverrideForHistoricalCase { get; set; } // np. 4666 dla testów spraw z 2025 r.

    // 3. Ciąża i zasiłek chorobowy
    public bool IncludePregnancySickness { get; set; }
    public int PlannedPregnancySickDays { get; set; }
    public bool HasCodeBOnEzla { get; set; } = true;
    public int PriorNonPregnancySickDaysInYear { get; set; }
    public bool NeedsRehabilitationBenefit { get; set; }

    // 4. Scenariusz rodzicielski i dane dziecka
    public ParenthoodScenario Scenario { get; set; } = ParenthoodScenario.BiologicalBirth;
    public DateOnly EventDate { get; set; } = DateOnly.FromDateTime(DateTime.Today); // poród / przyjęcie na wychowanie
    public DateOnly? LeaveStartDate { get; set; } // opcjonalnie do 6 tyg. przed porodem
    public int ChildrenCount { get; set; } = 1;
    public bool HasZaZyciemCertificate { get; set; }
    public ClaimScope ClaimScope { get; set; } = ClaimScope.FullMaternityAndParental;
    public int? RequestedLeaveDaysOverride { get; set; } // np. 7 dni z 2. części ojcowskiego lub 57 dni z ZUR

    // Dla adopcji / rodziny zastępczej
    public DateOnly? ChildBirthDate { get; set; }
    public bool HasSchoolDeferralDecision { get; set; }

    // Dla urodzenia martwego / zgonu dziecka
    public DateOnly? ChildDeathDate { get; set; }

    // Dla uzupełniającego urlopu macierzyńskiego (art. 180^2 KP)
    public bool CalculateSupplementaryMaternity { get; set; }
    public int? GestationWeekAtBirth { get; set; }
    public int? LowestBirthWeightGrams { get; set; }
    public int HospitalizationDaysWithinLimit { get; set; }
    public bool TermBabyHospitalizedMin2DaysBetweenDay5And28 { get; set; }

    // 5. Wybór wariantu, podział między 2 rodziców i łączenie z pracą
    public bool SubmitFullApplicationWithin21Days { get; set; } = true; // 81,5% vs 100%/70%
    public int MotherMaternityWeeksTaken { get; set; } = 20; // resztę może przejąć ojciec (po min. 14 tyg.)
    public int PrimaryParentParentalWeeks { get; set; } = 32; // z puli wspólnej (bez 9 tyg. drugiego rodzica)
    public bool SecondParentInsured { get; set; } = true;
    public decimal SecondParentAverageMonthlyGross { get; set; } = 8000m;
    public int SecondParentParentalWeeks { get; set; } = 9; // min. 9 tyg. wyłącznych
    public bool IncludePaternityLeave { get; set; } = true;

    // Łączenie urlopu rodzicielskiego z pracą (art. 182^1e - 182^1f KP)
    public decimal CombineWorkWithParentalFte { get; set; } // 0..0.5
    public int ParentalWeeksCombinedWithWork { get; set; }
    public bool CombineExtendedPartWithWork { get; set; } // art. 182^1f § 7 KP

    // 6. Zasiłek opiekuńczy (opcjonalna kalkulacja)
    public bool IncludeCareAllowance { get; set; }
    public int CareAllowanceDays { get; set; }
    public int CareRecipientType { get; set; } // 0 = dziecko <14 lat (60 dni), 1 = niepełnosprawne 14-18 (30 dni), 2 = inny członek / >14 (14 dni)
}