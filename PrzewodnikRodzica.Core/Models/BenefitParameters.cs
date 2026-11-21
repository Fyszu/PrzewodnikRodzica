namespace PrzewodnikRodzica.Core.Models;

public class BenefitParameters
{
    public int Year { get; set; } = 2026;


    #region Kwoty bazowe 2026

    public decimal MinimalWageGross { get; set; } = 4806.00m;
    public decimal SocialSecurityDeductionRate { get; set; } = 0.1371m;
    public decimal MaxVoluntarySicknessBaseGross { get; set; } = 23550.00m;
    public decimal ParentalBenefitFloorNet { get; set; } = 1000.00m;
    public decimal EstimatedPitAdvanceRate { get; set; } = 0.12m;
    public decimal JdgDebtThresholdPercentOfMinWage { get; set; } = 0.01m;
    public decimal FuneralAllowanceStillbirth { get; set; } = 7000.00m;

    #endregion


    #region Stawki procentowe zasiłków

    public decimal PregnancySicknessRate { get; set; } = 1.00m;
    public decimal StandardSicknessRate { get; set; } = 0.80m;
    public decimal RehabPregnancyRate { get; set; } = 1.00m;
    public decimal MaternityRate { get; set; } = 1.00m;
    public decimal SupplementaryMaternityRate { get; set; } = 1.00m;
    public decimal PaternityRate { get; set; } = 1.00m;
    public decimal ParentalRate { get; set; } = 0.70m;
    public decimal UnifiedMaternityAndParentalRate { get; set; } = 0.815m;
    public decimal SecondParentExclusiveParentalRate { get; set; } = 0.70m;
    public decimal CareAllowanceRate { get; set; } = 0.80m;

    #endregion


    #region Okresy i limity (dni / tygodnie)

    public int MandatoryWaitingPeriodDays { get; set; } = 30;
    public int VoluntaryWaitingPeriodDays { get; set; } = 90;
    public int EmployerSickPayDaysUnder50 { get; set; } = 33;
    public int EmployerSickPayDays50Plus { get; set; } = 14;
    public int PregnancyBenefitPeriodDays { get; set; } = 270;
    public int StandardBenefitPeriodDays { get; set; } = 182;
    public int LargePayerInsuredThreshold { get; set; } = 20;

    #endregion


    #region Wymiary urlopów (tygodnie)

    public int MaternityWeeksSingle { get; set; } = 20;
    public int MaternityWeeksTwins { get; set; } = 31;
    public int MaternityWeeksTriplets { get; set; } = 33;
    public int MaternityWeeksQuadruplets { get; set; } = 35;
    public int MaternityWeeksQuintupletsPlus { get; set; } = 37;
    public int MotherMandatoryPostBirthWeeks { get; set; } = 14;
    public int AdoptionMinMaternityWeeks { get; set; } = 9;

    public int ParentalWeeksSingle { get; set; } = 41;
    public int ParentalWeeksMultiple { get; set; } = 43;
    public int ParentalWeeksSingleZaZyciem { get; set; } = 65;
    public int ParentalWeeksMultipleZaZyciem { get; set; } = 67;
    public int ParentalWeeksMinAdoption { get; set; } = 38;
    public int ParentalWeeksMinAdoptionZaZyciem { get; set; } = 62;
    public int NonTransferableParentalWeeksPerParent { get; set; } = 9;

    public int MaxExtendedParentalWeeksSingle { get; set; } = 82;
    public int MaxExtendedParentalWeeksMultiple { get; set; } = 86;
    public int MaxExtendedParentalWeeksSingleZaZyciem { get; set; } = 130;
    public int MaxExtendedParentalWeeksMultipleZaZyciem { get; set; } = 134;

    public int PaternityWeeks { get; set; } = 2;
    public int StillbirthMaternityWeeks { get; set; } = 8;
    public int PostChildDeathMinDays { get; set; } = 7;

    #endregion


    #region Wartości wyliczane pomocniczo

    public decimal MinBenefitBaseFullTime =>
        Math.Round(MinimalWageGross * (1m - SocialSecurityDeductionRate), 2, MidpointRounding.AwayFromZero);

    public decimal MaxVoluntaryBenefitBase =>
        Math.Round(MaxVoluntarySicknessBaseGross * (1m - SocialSecurityDeductionRate), 2, MidpointRounding.AwayFromZero);

    public decimal JdgDebtThresholdAmount =>
        Math.Round(MinimalWageGross * JdgDebtThresholdPercentOfMinWage, 2, MidpointRounding.AwayFromZero);

    #endregion
}