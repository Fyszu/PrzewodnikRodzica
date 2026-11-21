namespace PrzewodnikRodzica.Core.Models;

public enum RiskSeverity
{
    Info = 0,
    Warning = 1,
    Critical = 2
}

public class BaseCalculationSummary
{
    public decimal AverageGross { get; set; }
    public decimal BenefitBase { get; set; } // Po odliczeniu 13,71%
    public decimal DailyBase100Percent { get; set; } // 1/30 podstawy
    public int MonthsIncluded { get; set; }
    public int MonthsExcludedUnderHalfTime { get; set; }
    public bool MinimalFloorApplied { get; set; }
    public bool VoluntaryMaxCapApplied { get; set; }
    public List<string> CalculationNotes { get; set; } = new();
}

public class PregnancySicknessSummary
{
    public bool IsEligible { get; set; }
    public string EligibilityReason { get; set; } = string.Empty;
    public int EmployerPaidDays { get; set; }
    public int ZusPaidDays { get; set; }
    public int MaxBenefitPeriodDays { get; set; }
    public decimal RatePercent { get; set; }
    public decimal DailyGrossAmount { get; set; }
    public decimal TotalEstimatedGross { get; set; }
    public bool EligibleForAllowanceInMaternityAmount { get; set; }
    public string? SpecialNote { get; set; }
}

public class TimelineBlock
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ParentRole { get; set; } = "Matka / Wnioskodawca"; // lub "Drugi rodzic (Ojciec)"
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Days { get; set; }
    public decimal Weeks { get; set; }
    public decimal RatePercent { get; set; } // np. 100, 81.5, 70
    public decimal WorkFteDuringLeave { get; set; }
    public decimal DailyGrossAmount { get; set; }
    public decimal MonthlyGrossEstimate { get; set; } // 30 dni
    public decimal MonthlyNetEstimate { get; set; }
    public bool ParentalFloorTopUpApplied { get; set; }
    public decimal MonthlyTopUpAmount { get; set; }
    public decimal TotalBlockGross { get; set; }
    public string LegalBasis { get; set; } = string.Empty;
    public string ColorToken { get; set; } = "emerald"; // emerald, teal, blue, amber, slate
}

public class RiskAlert
{
    public string Code { get; set; } = string.Empty;
    public RiskSeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LegalBasis { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
}

public class RequiredDocumentItem
{
    public string FormCode { get; set; } = string.Empty; // np. ZAM, Z-3, ZAS-26, Wniosek KP
    public string Name { get; set; } = string.Empty;
    public string SubmitTo { get; set; } = string.Empty; // Pracodawca / ZUS (eZUS)
    public string Deadline { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class CharityOrganization
{
    public string Name { get; set; } = string.Empty;
    public string FocusArea { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string? Helpline { get; set; }
}

public class ExternalLinkItem
{
    public string Title { get; set; } = string.Empty;
    public string Institution { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class CareAllowanceSummary
{
    public bool IsEligible { get; set; }
    public int MaxAnnualDays { get; set; }
    public int CalculatedDays { get; set; }
    public decimal RatePercent { get; set; }
    public decimal DailyGrossAmount { get; set; }
    public decimal TotalGrossAmount { get; set; }
    public string FormCode { get; set; } = "Z-15A";
}

public class CalculationResult
{
    public bool IsEligibleForMaternityBenefit { get; set; }
    public bool IsSensitiveSituationMode { get; set; }
    public string EligibilityHeadline { get; set; } = string.Empty;
    public string BenefitPayer { get; set; } = string.Empty; // "Zakład Ubezpieczeń Społecznych (ZUS)" lub "Płatnik składek (Pracodawca / Zleceniodawca)"
    public string PayerExplanation { get; set; } = string.Empty;

    public BaseCalculationSummary PrimaryParentBase { get; set; } = new();
    public BaseCalculationSummary? SecondParentBase { get; set; }
    public PregnancySicknessSummary? PregnancySickness { get; set; }
    public CareAllowanceSummary? CareAllowance { get; set; }

    public int SupplementaryMaternityWeeks { get; set; }
    public int ExtendedParentalExtraDays { get; set; }
    public int TotalCombinedLeaveDays { get; set; }
    public decimal TotalEstimatedBenefitsGross { get; set; }
    public decimal? FuneralAllowanceAmount { get; set; }
    public bool RecommendKosiniakoweFallback { get; set; }

    public List<TimelineBlock> Timeline { get; set; } = new();
    public List<RiskAlert> Risks { get; set; } = new();
    public List<RequiredDocumentItem> RequiredDocuments { get; set; } = new();
    public List<CharityOrganization> CharitySupport { get; set; } = new();
    public List<ExternalLinkItem> OfficialLinks { get; set; } = new();
}