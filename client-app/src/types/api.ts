export type EmploymentTitle =
  | 'Employee'
  | 'MandateContract'
  | 'SelfEmployed'
  | 'ContractForSpecificWork'
  | 'UninsuredOrUlgaNaStart';

export type ParenthoodScenario =
  | 'BiologicalBirth'
  | 'Adoption'
  | 'FosterCareNonProfessional'
  | 'StillbirthOrChildDeath';

export type SpecialTerminationReason =
  | 'None'
  | 'EmployerBankruptcyOrLiquidation'
  | 'EmployerDeath'
  | 'UnlawfulTerminationCourtConfirmed'
  | 'FixedTermExtendedToBirth'
  | 'StandardTerminationBeforeBirth';

export type RiskSeverity = 'Info' | 'Warning' | 'Critical';

export interface MonthlySalaryRecord {
  year: number;
  month: number;
  daysWorked: number;
  daysObligatory: number;
  fixedGross: number;
  variableProportionalGross: number;
  variableActualGross: number;
  contributionRatePercent: number;
  excludeVariableDueToEmployerNote: boolean;
  absenceReason?: string | null;
}

export interface CalculationRequest {
  employmentTitle: EmploymentTitle;
  hasVoluntarySicknessInsurance: boolean;
  continuousInsuranceDays: number;
  exemptFromWaitingPeriod: boolean;
  employerInsuredCount: number;
  fteFraction: number;
  isOver50YearsOld: boolean;
  terminationReason: SpecialTerminationReason;

  jdgSocialSecurityDebt: number;
  suddenBaseIncreaseInPregnancy: boolean;
  shortContractJustBeforePregnancy: boolean;

  averageMonthlyGross: number;
  monthlySalaries: MonthlySalaryRecord[];
  quarterlyBonusesTotalLast4Quarters: number;
  annualBonusLastYear: number;
  continueBaseFromPreviousBenefit: boolean;
  previousBenefitBaseNetOfZUS: number | null;
  minimalWageOverrideForHistoricalCase?: number | null;

  includePregnancySickness: boolean;
  plannedPregnancySickDays: number;
  hasCodeBOnEzla: boolean;
  priorNonPregnancySickDaysInYear: number;
  needsRehabilitationBenefit: boolean;

  scenario: ParenthoodScenario;
  eventDate: string;
  leaveStartDate: string | null;
  childrenCount: number;
  hasZaZyciemCertificate: boolean;

  childBirthDate: string | null;
  hasSchoolDeferralDecision: boolean;
  childDeathDate: string | null;

  calculateSupplementaryMaternity: boolean;
  gestationWeekAtBirth: number | null;
  lowestBirthWeightGrams: number | null;
  hospitalizationDaysWithinLimit: number;
  termBabyHospitalizedMin2DaysBetweenDay5And28: boolean;

  submitFullApplicationWithin21Days: boolean;
  motherMaternityWeeksTaken: number;
  primaryParentParentalWeeks: number;
  secondParentInsured: boolean;
  secondParentAverageMonthlyGross: number;
  secondParentParentalWeeks: number;
  includePaternityLeave: boolean;

  combineWorkWithParentalFte: number;
  parentalWeeksCombinedWithWork: number;
  combineExtendedPartWithWork: boolean;

  includeCareAllowance: boolean;
  careAllowanceDays: number;
  careRecipientType: number;
}

export interface BaseCalculationSummary {
  averageGross: number;
  benefitBase: number;
  dailyBase100Percent: number;
  monthsIncluded: number;
  monthsExcludedUnderHalfTime: number;
  minimalFloorApplied: boolean;
  voluntaryMaxCapApplied: boolean;
  calculationNotes: string[];
}

export interface PregnancySicknessSummary {
  isEligible: boolean;
  eligibilityReason: string;
  employerPaidDays: number;
  zusPaidDays: number;
  maxBenefitPeriodDays: number;
  ratePercent: number;
  dailyGrossAmount: number;
  totalEstimatedGross: number;
  eligibleForAllowanceInMaternityAmount: boolean;
  specialNote?: string | null;
}

export interface CareAllowanceSummary {
  isEligible: boolean;
  maxAnnualDays: number;
  calculatedDays: number;
  ratePercent: number;
  dailyGrossAmount: number;
  totalGrossAmount: number;
  formCode: string;
}

export interface TimelineBlock {
  id: string;
  title: string;
  parentRole: string;
  startDate: string;
  endDate: string;
  days: number;
  weeks: number;
  ratePercent: number;
  workFteDuringLeave: number;
  dailyGrossAmount: number;
  monthlyGrossEstimate: number;
  monthlyNetEstimate: number;
  parentalFloorTopUpApplied: boolean;
  monthlyTopUpAmount: number;
  totalBlockGross: number;
  legalBasis: string;
  colorToken: string;
}

export interface RiskAlert {
  code: string;
  severity: RiskSeverity;
  title: string;
  description: string;
  legalBasis: string;
  recommendedAction: string;
}

export interface RequiredDocumentItem {
  formCode: string;
  name: string;
  submitTo: string;
  deadline: string;
  notes: string;
}

export interface CharityOrganization {
  name: string;
  focusArea: string;
  description: string;
  websiteUrl: string;
  helpline?: string | null;
}

export interface ExternalLinkItem {
  title: string;
  institution: string;
  url: string;
}

export interface CalculationResult {
  isEligibleForMaternityBenefit: boolean;
  isSensitiveSituationMode: boolean;
  eligibilityHeadline: string;
  benefitPayer: string;
  payerExplanation: string;

  primaryParentBase: BaseCalculationSummary;
  secondParentBase?: BaseCalculationSummary | null;
  pregnancySickness?: PregnancySicknessSummary | null;
  careAllowance?: CareAllowanceSummary | null;

  supplementaryMaternityWeeks: number;
  extendedParentalExtraDays: number;
  totalCombinedLeaveDays: number;
  totalEstimatedBenefitsGross: number;
  funeralAllowanceAmount?: number | null;
  recommendKosiniakoweFallback: boolean;

  timeline: TimelineBlock[];
  risks: RiskAlert[];
  requiredDocuments: RequiredDocumentItem[];
  charitySupport: CharityOrganization[];
  officialLinks: ExternalLinkItem[];
}

export interface QuotaStatus {
  pagesUsedAzure: number;
  hardPageBudget: number;
  remainingPages: number;
  estimatedCostPln: number;
  azureEnabledByQuota: boolean;
}

export interface ParsedDocumentResult {
  detectedForms: string[];
  hasConfirmedInsurance: boolean;
  insuranceSummary: string;
  extractedFacts: string[];
  mappedRequest: CalculationRequest;
}

export interface DocumentAnalysisResponse {
  fileName: string;
  pageCount: number;
  extractionEngine: string;
  usedAiEngine: boolean;
  servedFromCache: boolean;
  notice?: string | null;
  parsedDocument: ParsedDocumentResult;
  calculation: CalculationResult;
  quota: QuotaStatus;
}

export interface DemoTestCaseItem {
  id: string;
  title: string;
  category: string;
  summary: string;
  expectedOutcomeHint: string;
  requestData: CalculationRequest;
}

export interface PersonaListItem {
  personaId: string;
  fullName: string;
  insuranceTitleCode: string;
  insuranceTitleDescription: string;
  socialSecurityDebtPln: number;
}

export interface SimulatedEzlaItem {
  seriesNumber: string;
  from: string;
  to: string;
  days: number;
  code: string;
  doctorNote: string;
}

export interface SimulatedZusAccount {
  personaId: string;
  fullName: string;
  maskedPesel: string;
  insuranceTitleCode: string;
  insuranceTitleDescription: string;
  hasActiveSicknessInsurance: boolean;
  continuousInsuranceDays: number;
  socialSecurityDebtPln: number;
  debtCertificateStatus: string;
  ezlaCertificates: SimulatedEzlaItem[];
  prefilledCalculatorData: CalculationRequest;
}

export interface SubmittedApplicationRecord {
  id: string;
  category: string;
  title: string;
  recipient: string;
  payloadSummary: string;
  status: string;
  submittedUtc: string;
}

export interface BenefitParameters {
  year: number;
  minimalWageGross: number;
  socialSecurityDeductionRate: number;
  maxVoluntarySicknessBaseGross: number;
  parentalBenefitFloorNet: number;
  estimatedPitAdvanceRate: number;
  jdgDebtThresholdPercentOfMinWage: number;
  funeralAllowanceStillbirth: number;
  pregnancySicknessRate: number;
  standardSicknessRate: number;
  rehabPregnancyRate: number;
  maternityRate: number;
  supplementaryMaternityRate: number;
  paternityRate: number;
  parentalRate: number;
  unifiedMaternityAndParentalRate: number;
  secondParentExclusiveParentalRate: number;
  careAllowanceRate: number;
  mandatoryWaitingPeriodDays: number;
  voluntaryWaitingPeriodDays: number;
  employerSickPayDaysUnder50: number;
  employerSickPayDays50Plus: number;
  pregnancyBenefitPeriodDays: number;
  standardBenefitPeriodDays: number;
  largePayerInsuredThreshold: number;
  maternityWeeksSingle: number;
  maternityWeeksTwins: number;
  maternityWeeksTriplets: number;
  maternityWeeksQuadruplets: number;
  maternityWeeksQuintupletsPlus: number;
  motherMandatoryPostBirthWeeks: number;
  adoptionMinMaternityWeeks: number;
  parentalWeeksSingle: number;
  parentalWeeksMultiple: number;
  parentalWeeksSingleZaZyciem: number;
  parentalWeeksMultipleZaZyciem: number;
  parentalWeeksMinAdoption: number;
  parentalWeeksMinAdoptionZaZyciem: number;
  nonTransferableParentalWeeksPerParent: number;
  maxExtendedParentalWeeksSingle: number;
  maxExtendedParentalWeeksMultiple: number;
  maxExtendedParentalWeeksSingleZaZyciem: number;
  maxExtendedParentalWeeksMultipleZaZyciem: number;
  paternityWeeks: number;
  stillbirthMaternityWeeks: number;
  postChildDeathMinDays: number;
  minBenefitBaseFullTime?: number;
  maxVoluntaryBenefitBase?: number;
  jdgDebtThresholdAmount?: number;
}

export interface AiDisclosureResponse {
  usesAi: boolean;
  scope: { module: string; technology: string; role: string }[];
}

export function createDefaultRequest(): CalculationRequest {
  const today = new Date().toISOString().slice(0, 10);
  return {
    employmentTitle: 'Employee',
    hasVoluntarySicknessInsurance: true,
    continuousInsuranceDays: 365,
    exemptFromWaitingPeriod: false,
    employerInsuredCount: 15,
    fteFraction: 1.0,
    isOver50YearsOld: false,
    terminationReason: 'None',

    jdgSocialSecurityDebt: 0,
    suddenBaseIncreaseInPregnancy: false,
    shortContractJustBeforePregnancy: false,

    averageMonthlyGross: 7500,
    monthlySalaries: [],
    quarterlyBonusesTotalLast4Quarters: 0,
    annualBonusLastYear: 0,
    continueBaseFromPreviousBenefit: false,
    previousBenefitBaseNetOfZUS: null,

    includePregnancySickness: false,
    plannedPregnancySickDays: 90,
    hasCodeBOnEzla: true,
    priorNonPregnancySickDaysInYear: 0,
    needsRehabilitationBenefit: false,

    scenario: 'BiologicalBirth',
    eventDate: today,
    leaveStartDate: null,
    childrenCount: 1,
    hasZaZyciemCertificate: false,

    childBirthDate: null,
    hasSchoolDeferralDecision: false,
    childDeathDate: null,

    calculateSupplementaryMaternity: false,
    gestationWeekAtBirth: 39,
    lowestBirthWeightGrams: 3200,
    hospitalizationDaysWithinLimit: 0,
    termBabyHospitalizedMin2DaysBetweenDay5And28: false,

    submitFullApplicationWithin21Days: true,
    motherMaternityWeeksTaken: 20,
    primaryParentParentalWeeks: 32,
    secondParentInsured: true,
    secondParentAverageMonthlyGross: 8500,
    secondParentParentalWeeks: 9,
    includePaternityLeave: true,

    combineWorkWithParentalFte: 0,
    parentalWeeksCombinedWithWork: 0,
    combineExtendedPartWithWork: false,

    includeCareAllowance: false,
    careAllowanceDays: 14,
    careRecipientType: 0,
  };
}

export interface SavedCalculationRecord {
  id: string;
  personaId: string;
  name: string;
  request: CalculationRequest;
  totalLeaveDays: number;
  totalEstimatedBenefitsGross: number;
  primaryBenefitBase: number;
  eligibilityHeadline: string;
  savedUtc: string;
}