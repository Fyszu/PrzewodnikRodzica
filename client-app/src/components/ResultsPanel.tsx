import {
  AlertOctagon,
  AlertTriangle,
  Banknote,
  Building2,
  Calendar,
  CheckCircle2,
  Clock,
  ExternalLink,
  FileCheck2,
  HeartHandshake,
  Info,
} from 'lucide-react';
import type { CalculationResult, RiskSeverity } from '../types/api';

interface Props {
  result: CalculationResult;
}

const fmtPln = (v: number) =>
  new Intl.NumberFormat('pl-PL', { style: 'currency', currency: 'PLN' }).format(v);

const colorBarMap: Record<string, string> = {
  emerald: 'bg-emerald-600',
  teal: 'bg-teal-600',
  blue: 'bg-blue-600',
  indigo: 'bg-indigo-600',
  amber: 'bg-amber-500',
  cyan: 'bg-cyan-600',
  sky: 'bg-sky-500',
  slate: 'bg-slate-600',
};

const severityStyles: Record<RiskSeverity, string> = {
  Critical: 'border-rose-300 bg-rose-50 text-rose-900',
  Warning: 'border-amber-300 bg-amber-50 text-amber-900',
  Info: 'border-sky-200 bg-sky-50 text-sky-900',
};

export function ResultsPanel({ result }: Props) {
  const totalTimelineDays = Math.max(
    1,
    result.timeline.reduce((acc, b) => acc + b.days, 0)
  );

  return (
    <div className="space-y-6">
      {/* Baner trybu trudnej sytuacji */}
      {result.isSensitiveSituationMode && (
        <div className="rounded-2xl border border-slate-300 bg-slate-100 p-5 text-slate-800 shadow-xs">
          <div className="flex items-start gap-3">
            <HeartHandshake className="mt-0.5 h-6 w-6 shrink-0 text-slate-700" />
            <div>
              <h3 className="text-base font-semibold text-slate-900">
                Tryb szczególnego wsparcia
              </h3>
              <p className="mt-1 text-sm text-slate-700">
                Poniżej oprócz dokładnego wymiaru przysługujących świadczeń oraz listy wymaganych dokumentów, 
                znajdziesz również sprawdzone organizacje charytatywne oferujące bezpłatną pomoc.
              </p>
            </div>
          </div>
        </div>
      )}

      {/* Główny werdykt uprawnień i płatnik */}
      <div
        className={`rounded-2xl border p-5 shadow-xs ${
          result.isSensitiveSituationMode
            ? 'border-slate-300 bg-white'
            : result.isEligibleForMaternityBenefit
              ? 'border-emerald-200 bg-emerald-50/70'
              : 'border-rose-200 bg-rose-50/70'
        }`}
      >
        <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
          <div className="flex items-start gap-3">
            {result.isEligibleForMaternityBenefit ? (
              <CheckCircle2
                className={`mt-0.5 h-6 w-6 shrink-0 ${
                  result.isSensitiveSituationMode ? 'text-slate-700' : 'text-emerald-700'
                }`}
              />
            ) : (
              <AlertOctagon className="mt-0.5 h-6 w-6 shrink-0 text-rose-700" />
            )}
            <div>
              <h2 className="text-lg font-bold text-slate-900">{result.eligibilityHeadline}</h2>
              <div className="mt-2 flex items-center gap-2 text-sm font-medium text-slate-800">
                <Building2 className="h-4 w-4 text-slate-500" />
                <span>Płatnik świadczenia: {result.benefitPayer}</span>
              </div>
              <p className="mt-1 text-sm text-slate-600">{result.payerExplanation}</p>
            </div>
          </div>
        </div>
      </div>

      {/* Karty KPI */}
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-slate-500">
            <Banknote className="h-4 w-4 text-emerald-600" />
            Podstawa wymiaru (po 13,71%)
          </div>
          <div className="mt-2 text-2xl font-bold text-slate-900">
            {fmtPln(result.primaryParentBase.benefitBase)}
          </div>
          <div className="mt-1 text-xs text-slate-500">
            Stawka dzienna (100%): {fmtPln(result.primaryParentBase.dailyBase100Percent)} / dzień
          </div>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-slate-500">
            <Clock className="h-4 w-4 text-blue-600" />
            Łączny okres urlopów
          </div>
          <div className="mt-2 text-2xl font-bold text-slate-900">
            {result.totalCombinedLeaveDays} dni
          </div>
          <div className="mt-1 text-xs text-slate-500">
            ok. {(result.totalCombinedLeaveDays / 7).toFixed(1)} tygodni
            {result.extendedParentalExtraDays > 0 && ` (w tym +${result.extendedParentalExtraDays} dni wydłużenia)`}
          </div>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-slate-500">
            <Calendar className="h-4 w-4 text-teal-600" />
            Łączna suma świadczeń (brutto)
          </div>
          <div className="mt-2 text-2xl font-bold text-slate-900">
            {fmtPln(result.totalEstimatedBenefitsGross)}
          </div>
          <div className="mt-1 text-xs text-slate-500">
            {result.funeralAllowanceAmount
              ? `+ zasiłek pogrzebowy ${fmtPln(result.funeralAllowanceAmount)}`
              : 'Suma szacunkowa z wybranych modułów'}
          </div>
        </div>

        <div className="rounded-xl border border-slate-200 bg-white p-4 shadow-xs">
          <div className="flex items-center gap-2 text-xs font-medium uppercase tracking-wide text-slate-500">
            <Info className="h-4 w-4 text-amber-600" />
            Status podstawy / dodatki
          </div>
          <div className="mt-2 text-sm font-semibold text-slate-900">
            {result.primaryParentBase.monthsIncluded} mies. w podstawie
          </div>
          <div className="mt-1 text-xs text-slate-500">
            {result.primaryParentBase.minimalFloorApplied && 'Podniesiono do minimum ustawowego. '}
            {result.primaryParentBase.voluntaryMaxCapApplied && 'Zastosowano limit 250% dobrowolnego chorobowego. '}
            {result.supplementaryMaternityWeeks > 0 && `Uzupełniający: +${result.supplementaryMaternityWeeks} tyg.`}
            {!result.primaryParentBase.minimalFloorApplied &&
              !result.primaryParentBase.voluntaryMaxCapApplied &&
              result.supplementaryMaternityWeeks === 0 &&
              'Standardowe wyliczenie podstawy.'}
          </div>
        </div>
      </div>

      {/* Notatki z wyliczenia podstawy */}
      {result.primaryParentBase.calculationNotes.length > 0 && (
        <div className="rounded-xl border border-slate-200 bg-white p-4 text-sm text-slate-700">
          <div className="font-semibold text-slate-900">Szczegóły ustalenia podstawy wymiaru:</div>
          <ul className="mt-2 list-disc space-y-1 pl-5">
            {result.primaryParentBase.calculationNotes.map((note, idx) => (
              <li key={idx}>{note}</li>
            ))}
          </ul>
        </div>
      )}

      {/* Zasiłek chorobowy w ciąży */}
      {result.pregnancySickness && (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
          <h3 className="text-base font-bold text-slate-900">
            Świadczenia w czasie ciąży (chorobowe / rehabilitacyjne)
          </h3>
          <p className="mt-1 text-sm text-slate-700">{result.pregnancySickness.eligibilityReason}</p>
          {result.pregnancySickness.isEligible && (
            <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-4">
              <div className="rounded-lg bg-slate-50 p-3">
                <div className="text-xs text-slate-500">Wynagrodzenie od pracodawcy</div>
                <div className="text-lg font-bold text-slate-900">{result.pregnancySickness.employerPaidDays} dni</div>
              </div>
              <div className="rounded-lg bg-slate-50 p-3">
                <div className="text-xs text-slate-500">Zasiłek chorobowy (FUS)</div>
                <div className="text-lg font-bold text-slate-900">{result.pregnancySickness.zusPaidDays} dni</div>
              </div>
              <div className="rounded-lg bg-slate-50 p-3">
                <div className="text-xs text-slate-500">Stawka / Limit okresu</div>
                <div className="text-lg font-bold text-slate-900">
                  {result.pregnancySickness.ratePercent}% / {result.pregnancySickness.maxBenefitPeriodDays} dni
                </div>
              </div>
              <div className="rounded-lg bg-slate-50 p-3">
                <div className="text-xs text-slate-500">Szacunkowo brutto</div>
                <div className="text-lg font-bold text-emerald-700">
                  {fmtPln(result.pregnancySickness.totalEstimatedGross)}
                </div>
              </div>
            </div>
          )}
          {result.pregnancySickness.specialNote && (
            <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900">
              {result.pregnancySickness.specialNote}
            </div>
          )}
        </div>
      )}

      {/* Interaktywna Oś Czasu Urlopów */}
      {result.timeline.length > 0 && (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h3 className="text-base font-bold text-slate-900">
              Harmonogram i oś czasu urlopów oraz wypłat (oboje rodzice)
            </h3>
            <span className="text-xs text-slate-500">
              Kwoty miesięczne liczone za 30 dni kalendarzowych (szacunek netto po 12% zaliczce PIT)
            </span>
          </div>

          {/* Wizualny pasek proporcjonalny */}
          <div className="mt-4 flex h-9 w-full overflow-hidden rounded-xl bg-slate-100">
            {result.timeline.map((block) => {
              const pct = Math.max(6, (block.days / totalTimelineDays) * 100);
              return (
                <div
                  key={block.id}
                  style={{ width: `${pct}%` }}
                  title={`${block.title} (${block.weeks} tyg., ${block.ratePercent}%)`}
                  className={`flex items-center justify-center border-r border-white/40 px-2 text-xs font-semibold text-white last:border-r-0 ${
                    colorBarMap[block.colorToken] ?? 'bg-emerald-600'
                  }`}
                >
                  <span className="truncate">{block.weeks} tyg.</span>
                </div>
              );
            })}
          </div>

          {/* Karty / tabela bloków (RWD) */}
          <div className="mt-5 space-y-3">
            {result.timeline.map((block) => (
              <div
                key={block.id}
                className="flex flex-col gap-3 rounded-xl border border-slate-200 p-4 md:flex-row md:items-center md:justify-between"
              >
                <div className="space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span
                      className={`inline-block h-3 w-3 rounded-full ${
                        colorBarMap[block.colorToken] ?? 'bg-emerald-600'
                      }`}
                    />
                    <span className="font-semibold text-slate-900">{block.title}</span>
                    <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-700">
                      {block.parentRole}
                    </span>
                    <span className="rounded-full bg-emerald-50 px-2.5 py-0.5 text-xs font-semibold text-emerald-800">
                      Stawka {block.ratePercent}%
                    </span>
                    {block.workFteDuringLeave > 0 && (
                      <span className="rounded-full bg-indigo-50 px-2.5 py-0.5 text-xs font-semibold text-indigo-800">
                        Praca: {block.workFteDuringLeave} etatu
                      </span>
                    )}
                  </div>
                  <div className="text-sm text-slate-600">
                    {block.startDate} → {block.endDate} ({block.days} dni / {block.weeks} tyg.)
                  </div>
                  <div className="text-xs text-slate-500">Podstawa prawna: {block.legalBasis}</div>
                  {block.parentalFloorTopUpApplied && (
                    <div className="inline-block rounded-md bg-amber-50 px-2 py-1 text-xs font-medium text-amber-900">
                      Zastosowano podwyższenie do świadczenia rodzicielskiego (+{fmtPln(block.monthlyTopUpAmount)}/mies., art. 31 ust. 3a)
                    </div>
                  )}
                </div>

                <div className="flex flex-row gap-4 border-t border-slate-100 pt-3 md:flex-col md:items-end md:gap-1 md:border-t-0 md:pt-0">
                  <div className="text-right">
                    <div className="text-xs text-slate-500">Miesięcznie (szac. netto / brutto)</div>
                    <div className="text-base font-bold text-slate-900">
                      {fmtPln(block.monthlyNetEstimate)}{' '}
                      <span className="text-xs font-normal text-slate-500">
                        ({fmtPln(block.monthlyGrossEstimate)} brutto)
                      </span>
                    </div>
                  </div>
                  <div className="text-right text-xs text-slate-500">
                    Łącznie za etap: <strong className="text-slate-700">{fmtPln(block.totalBlockGross)} brutto</strong>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Zasiłek opiekuńczy */}
      {result.careAllowance && (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
          <h3 className="text-base font-bold text-slate-900">Zasiłek opiekuńczy (formularz {result.careAllowance.formCode})</h3>
          <p className="mt-1 text-sm text-slate-600">
            Limit roczny dla wybranej kategorii: <strong>{result.careAllowance.maxAnnualDays} dni</strong> (brak okresu wyczekiwania, stawka {result.careAllowance.ratePercent}%).
            Za {result.careAllowance.calculatedDays} dni szacunkowa kwota to{' '}
            <strong className="text-emerald-700">{fmtPln(result.careAllowance.totalGrossAmount)} brutto</strong>.
          </p>
        </div>
      )}

      {/* Radar Ryzyk */}
      {result.risks.length > 0 && (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
          <h3 className="mb-3 flex items-center gap-2 text-base font-bold text-slate-900">
            <AlertTriangle className="h-5 w-5 text-amber-600" />
            Uwaga
          </h3>
          <div className="space-y-3">
            {result.risks.map((risk) => (
              <div key={risk.code} className={`rounded-xl border p-4 ${severityStyles[risk.severity]}`}>
                <div className="font-semibold">{risk.title}</div>
                <p className="mt-1 text-sm opacity-90">{risk.description}</p>
                <div className="mt-2 text-xs font-medium opacity-80">
                  Zalecane działanie: {risk.recommendedAction} ({risk.legalBasis})
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Checklista wymaganych dokumentów i terminów */}
      {result.requiredDocuments.length > 0 && (
        <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
          <h3 className="mb-3 flex items-center gap-2 text-base font-bold text-slate-900">
            <FileCheck2 className="h-5 w-5 text-emerald-700" />
            Wymagane dokumenty, terminy i adresat zgłoszenia
          </h3>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            {result.requiredDocuments.map((doc, idx) => (
              <div key={idx} className="rounded-xl border border-slate-200 bg-slate-50/70 p-4">
                <div className="flex items-center justify-between gap-2">
                  <span className="rounded-md bg-emerald-700 px-2.5 py-0.5 text-xs font-bold text-white">
                    {doc.formCode}
                  </span>
                  <span className="text-xs font-medium text-slate-600">{doc.deadline}</span>
                </div>
                <div className="mt-2 font-semibold text-slate-900">{doc.name}</div>
                <div className="mt-1 text-xs font-medium text-emerald-800">Gdzie złożyć: {doc.submitTo}</div>
                <p className="mt-1.5 text-xs text-slate-600">{doc.notes}</p>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Organizacje charytatywne (wyświetlane w sytuacjach trudnych / szczególnych) */}
      {result.charitySupport.length > 0 && (
        <div className="rounded-2xl border border-slate-300 bg-white p-5 shadow-xs">
          <h3 className="mb-3 flex items-center gap-2 text-base font-bold text-slate-900">
            <HeartHandshake className="h-5 w-5 text-slate-700" />
            Organizacje charytatywne — bezpłatne wsparcie w trudnych sytuacjach
          </h3>
          <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
            {result.charitySupport.map((org) => (
              <a
                key={org.name}
                href={org.websiteUrl}
                target="_blank"
                rel="noreferrer"
                className="group rounded-xl border border-slate-200 p-4 transition hover:border-slate-400 hover:bg-slate-50"
              >
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-slate-900 group-hover:underline">{org.name}</span>
                  <ExternalLink className="h-4 w-4 text-slate-400" />
                </div>
                <div className="mt-1 text-xs font-medium text-slate-600">{org.focusArea}</div>
                <p className="mt-1.5 text-xs text-slate-600">{org.description}</p>
              </a>
            ))}
          </div>
        </div>
      )}

      {/* Oficjalne linki instytucji */}
      <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
        <h3 className="mb-3 text-sm font-bold uppercase tracking-wide text-slate-600">
          Oficjalne źródła i portale państwowe
        </h3>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
          {result.officialLinks.map((link) => (
            <a
              key={link.url}
              href={link.url}
              target="_blank"
              rel="noreferrer"
              className="flex items-center justify-between rounded-lg border border-slate-200 px-3 py-2.5 text-sm transition hover:border-emerald-500 hover:bg-emerald-50/40"
            >
              <div>
                <div className="font-medium text-slate-900">{link.title}</div>
                <div className="text-xs text-slate-500">{link.institution}</div>
              </div>
              <ExternalLink className="h-4 w-4 shrink-0 text-emerald-700" />
            </a>
          ))}
        </div>
      </div>
    </div>
  );
}