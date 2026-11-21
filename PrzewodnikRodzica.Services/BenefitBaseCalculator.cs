using PrzewodnikRodzica.Core.Models;

namespace PrzewodnikRodzica.Core.Services;

public class BenefitBaseCalculator
{
    public BaseCalculationSummary Calculate(CalculationRequest request, BenefitParameters parameters)
    {
        var summary = new BaseCalculationSummary();

        // Art. 43 ustawy zasiłkowej: przerwa krótsza niż 1 miesiąc kalendarzowy -> brak przeliczania podstawy
        if (request.ContinueBaseFromPreviousBenefit && request.PreviousBenefitBaseNetOfZUS is > 0)
        {
            var prevBase = Round2(request.PreviousBenefitBaseNetOfZUS.Value);
            summary.BenefitBase = prevBase;
            summary.AverageGross = Round2(prevBase / (1m - parameters.SocialSecurityDeductionRate));
            summary.DailyBase100Percent = Round2(prevBase / 30m);
            summary.CalculationNotes.Add("Zastosowano art. 43 ustawy zasiłkowej — przerwa między świadczeniami była krótsza niż miesiąc kalendarzowy, zachowano poprzednią podstawę wymiaru.");
            return summary;
        }

        decimal avgGross;
        decimal avgNetOfZus;

        if (request.MonthlySalaries.Count > 0)
        {
            var allUnderHalf = request.MonthlySalaries.All(m => m.DaysObligatory > 0 && m.DaysWorked * 2 < m.DaysObligatory);
            var includedTotalsGross = new List<decimal>();
            var includedTotalsNetOfZus = new List<decimal>();

            foreach (var row in request.MonthlySalaries)
            {
                bool underHalf = row.DaysObligatory > 0 && row.DaysWorked * 2 < row.DaysObligatory;
                if (underHalf && !allUnderHalf)
                {
                    summary.MonthsExcludedUnderHalfTime++;
                    continue;
                }

                decimal fixedPart = row.FixedGross;
                decimal varPropPart = row.ExcludeVariableDueToEmployerNote ? 0m : row.VariableProportionalGross;
                decimal varActualPart = row.ExcludeVariableDueToEmployerNote ? 0m : row.VariableActualGross;

                // Uzupełnienie zmiennych proporcjonalnych (art. 37 ust. 2 pkt 2 + art. 38 ust. 2)
                if (row.DaysObligatory > 0 && row.DaysWorked > 0 && row.DaysWorked < row.DaysObligatory && varPropPart > 0)
                {
                    varPropPart = varPropPart / row.DaysWorked * row.DaysObligatory;
                }

                decimal monthGross = fixedPart + varPropPart + varActualPart;
                decimal deductionFactor = 1m - (row.ContributionRatePercent / 100m);

                includedTotalsGross.Add(monthGross);
                includedTotalsNetOfZus.Add(monthGross * deductionFactor);
            }

            summary.MonthsIncluded = includedTotalsGross.Count;
            if (summary.MonthsIncluded == 0)
            {
                avgGross = request.AverageMonthlyGross;
                avgNetOfZus = avgGross * (1m - parameters.SocialSecurityDeductionRate);
            }
            else
            {
                avgGross = includedTotalsGross.Average();
                avgNetOfZus = includedTotalsNetOfZus.Average();
            }

            if (summary.MonthsExcludedUnderHalfTime > 0)
            {
                summary.CalculationNotes.Add($"Wyłączono {summary.MonthsExcludedUnderHalfTime} mies. z przepracowanym czasem poniżej połowy etatu (art. 38 ust. 2 pkt 1).");
            }

            if (request.MonthlySalaries.Any(m => m.ExcludeVariableDueToEmployerNote))
            {
                summary.CalculationNotes.Add("Zgodnie z uwagami na Z-3 pominięto składniki wypłacane mimo niezdolności do pracy / urlopu (art. 41 ust. 1).");
            }
        }
        else
        {
            summary.MonthsIncluded = 12;
            avgGross = request.AverageMonthlyGross;
            avgNetOfZus = avgGross * (1m - parameters.SocialSecurityDeductionRate);
        }

        // Premie kwartalne i roczne (1/12 po potrąceniu 13,71%) — art. 42
        decimal bonusGrossMonthly = (request.QuarterlyBonusesTotalLast4Quarters + request.AnnualBonusLastYear) / 12m;
        if (bonusGrossMonthly > 0)
        {
            avgGross += bonusGrossMonthly;
            avgNetOfZus += bonusGrossMonthly * (1m - parameters.SocialSecurityDeductionRate);
            summary.CalculationNotes.Add("Doliczono 1/12 premii kwartalnych i/lub rocznych (art. 42 ustawy zasiłkowej).");
        }

        // Limit górny 250% dla dobrowolnego ubezpieczenia chorobowego (JDG / zlecenie)
        bool isVoluntary = request.EmploymentTitle is EmploymentTitle.SelfEmployed or EmploymentTitle.MandateContract;
        if (isVoluntary && avgGross > parameters.MaxVoluntarySicknessBaseGross)
        {
            avgGross = parameters.MaxVoluntarySicknessBaseGross;
            avgNetOfZus = parameters.MaxVoluntaryBenefitBase;
            summary.VoluntaryMaxCapApplied = true;
            summary.CalculationNotes.Add($"Zastosowano ustawowy limit 250% podstawy dobrowolnego ubezpieczenia chorobowego ({parameters.MaxVoluntarySicknessBaseGross:N2} zł brutto -> {parameters.MaxVoluntaryBenefitBase:N2} zł).");
        }

        decimal finalBase = Round2(avgNetOfZus);

        // Minimalna podstawa dla pracownika pełnoetatowego (art. 45)
        decimal effectiveMinWage = request.MinimalWageOverrideForHistoricalCase ?? parameters.MinimalWageGross;
        decimal effectiveMinBase = Round2(effectiveMinWage * (1m - parameters.SocialSecurityDeductionRate));

        if (request.EmploymentTitle == EmploymentTitle.Employee && request.FteFraction >= 1.0m && finalBase < effectiveMinBase)
        {
            finalBase = effectiveMinBase;
            summary.MinimalFloorApplied = true;
            summary.CalculationNotes.Add($"Podniesiono podstawę wymiaru do minimum ustawowego dla pełnego etatu ({effectiveMinBase:N2} zł od płacy minimalnej {effectiveMinWage:N2} zł).");
        }
        else if (request.EmploymentTitle == EmploymentTitle.Employee && request.FteFraction > 0 && request.FteFraction < 1.0m)
        {
            decimal proportionalMinBase = Round2(effectiveMinBase * request.FteFraction);
            if (finalBase < proportionalMinBase)
            {
                finalBase = proportionalMinBase;
                summary.MinimalFloorApplied = true;
                summary.CalculationNotes.Add($"Podniesiono podstawę do proporcjonalnego minimum dla wymiaru {request.FteFraction:0.##} etatu ({proportionalMinBase:N2} zł).");
            }
        }

        summary.AverageGross = Round2(avgGross);
        summary.BenefitBase = finalBase;
        summary.DailyBase100Percent = Round2(finalBase / 30m);

        return summary;
    }

    public BaseCalculationSummary CalculateSimpleBase(decimal averageMonthlyGross, BenefitParameters parameters)
    {
        var netBase = Round2(averageMonthlyGross * (1m - parameters.SocialSecurityDeductionRate));
        return new BaseCalculationSummary
        {
            AverageGross = Round2(averageMonthlyGross),
            BenefitBase = netBase,
            DailyBase100Percent = Round2(netBase / 30m),
            MonthsIncluded = 12
        };
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}