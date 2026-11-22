import { useEffect, useState } from 'react';
import {
  Baby,
  Bookmark,
  Briefcase,
  Calculator,
  GitCompare,
  HeartPulse,
  LogIn,
  RefreshCw,
  Stethoscope,
  Trash2,
  Users,
} from 'lucide-react';
import { api } from '../api/client';
import type {
  CalculationRequest,
  CalculationResult,
  EmploymentTitle,
  ParenthoodScenario,
  SavedCalculationRecord,
  SimulatedZusAccount,
  SpecialTerminationReason,
} from '../types/api';
import { ResultsPanel } from './ResultsPanel';

interface Props {
  request: CalculationRequest;
  requestVersion: number;
  loggedAccount: SimulatedZusAccount | null;
  onChangeRequest: (next: CalculationRequest) => void;
  onLoadSavedRequest: (req: CalculationRequest) => void;
  onOpenMObywatelModal: () => void;
  onSensitiveModeChange: (sensitive: boolean) => void;
}

const fmtPln = (v: number) =>
  new Intl.NumberFormat('pl-PL', { style: 'currency', currency: 'PLN' }).format(v);

export function CalculatorTab({
  request,
  requestVersion,
  loggedAccount,
  onChangeRequest,
  onLoadSavedRequest,
  onOpenMObywatelModal,
  onSensitiveModeChange,
}: Props) {
  const [result, setResult] = useState<CalculationResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Zapisane symulacje użytkownika w SQLite
  const [savedList, setSavedList] = useState<SavedCalculationRecord[]>([]);
  const [scenarioName, setScenarioName] = useState('');
  const [savingScenario, setSavingScenario] = useState(false);
  const [selectedCompareIds, setSelectedCompareIds] = useState<string[]>([]);

  const runCalc = async (payload: CalculationRequest) => {
    setLoading(true);
    setError(null);
    try {
      const res = await api.calculate(payload);
      setResult(res);
      onSensitiveModeChange(res.isSensitiveSituationMode);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Błąd obliczeń');
    } finally {
      setLoading(false);
    }
  };
  
  useEffect(() => {
    runCalc(request);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [requestVersion]);

  useEffect(() => {
    if (!loggedAccount) {
      setSavedList([]);
      setSelectedCompareIds([]);
      return;
    }
    api.getSavedCalculations().then(setSavedList).catch(() => {});
  }, [loggedAccount]);

  const handleSaveScenario = async () => {
    if (!loggedAccount) return;
    setSavingScenario(true);
    try {
      const saved = await api.saveCalculation({
        name: scenarioName.trim() || `Wariant ${savedList.length + 1}`,
        request,
      });
      setSavedList((prev) => [saved, ...prev]);
      setScenarioName('');
    } finally {
      setSavingScenario(false);
    }
  };

  const handleDeleteScenario = async (id: string) => {
    if (!loggedAccount) return;
    await api.deleteSavedCalculation(id);
    setSavedList((prev) => prev.filter((item) => item.id !== id));
    setSelectedCompareIds((prev) => prev.filter((x) => x !== id));
  };

  const toggleCompare = (id: string) => {
    setSelectedCompareIds((prev) =>
      prev.includes(id)
        ? prev.filter((x) => x !== id)
        : prev.length < 2
          ? [...prev, id]
          : [prev[1], id]
    );
  };

  const comparedItems = savedList.filter((s) => selectedCompareIds.includes(s.id));

  const update = <K extends keyof CalculationRequest>(key: K, value: CalculationRequest[K]) => {
    const next = { ...request, [key]: value };
    onChangeRequest(next);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    runCalc(request);
  };

  const isVoluntary =
    request.employmentTitle === 'SelfEmployed' || request.employmentTitle === 'MandateContract';

  return (
    <div className="space-y-6">
      {/* Pasek zapisywania i porównywania scenariuszy (tryb hybrydowy Gość vs mObywatel) */}
      {!loggedAccount ? (
        <div className="flex flex-col gap-3 rounded-2xl border border-slate-200 bg-white p-4 shadow-xs sm:flex-row sm:items-center sm:justify-between">
          <div className="text-sm text-slate-700">
            <strong className="text-slate-900">Tryb Gościa (kalkulacja anonimowa):</strong> możesz swobodnie wyliczać
            wszystkie świadczenia. Aby pobrać dane z ZUS oraz <strong>zapisywać i porównywać warianty</strong> w bazie,
            zaloguj się przez mObywatel.
          </div>
          <button
            type="button"
            onClick={onOpenMObywatelModal}
            className="inline-flex shrink-0 items-center gap-2 rounded-xl bg-slate-900 px-4 py-2 text-xs font-bold text-white hover:bg-slate-800"
          >
            <LogIn className="h-3.5 w-3.5 text-rose-400" />
            Zaloguj przez mObywatel
          </button>
        </div>
      ) : (
        <div className="rounded-2xl border border-emerald-200 bg-white p-5 shadow-xs space-y-4">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            <div>
              <h3 className="flex items-center gap-2 text-sm font-bold text-slate-900">
                <Bookmark className="h-4 w-4 text-emerald-700" />
                Moje zapisane kalkulacje ({loggedAccount.fullName})
              </h3>
              <p className="text-xs text-slate-500">
                Zapisuj różne warianty (np. 81,5% vs 100%/70% + powrót na 1/2 etatu) i zaznacz 2, aby porównać różnice.
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <input
                type="text"
                placeholder="Nazwa wariantu (np. 81,5% bez pracy)"
                className="w-60"
                value={scenarioName}
                onChange={(e) => setScenarioName(e.target.value)}
              />
              <button
                type="button"
                disabled={savingScenario}
                onClick={handleSaveScenario}
                className="inline-flex items-center gap-1.5 rounded-xl bg-emerald-700 px-4 py-2 text-xs font-bold text-white hover:bg-emerald-800 disabled:opacity-50"
              >
                <Bookmark className="h-3.5 w-3.5" />
                {savingScenario ? 'Zapisywanie...' : 'Zapisz bieżący wariant'}
              </button>
            </div>
          </div>

          {savedList.length > 0 && (
            <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
              {savedList.map((item) => {
                const isSelected = selectedCompareIds.includes(item.id);
                return (
                  <div
                    key={item.id}
                    className={`rounded-xl border p-3.5 transition ${
                      isSelected ? 'border-emerald-600 bg-emerald-50/50' : 'border-slate-200 bg-slate-50/60'
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <label className="flex cursor-pointer items-center gap-2 text-sm font-bold text-slate-900">
                        <input
                          type="checkbox"
                          checked={isSelected}
                          onChange={() => toggleCompare(item.id)}
                        />
                        {item.name}
                      </label>
                      <button
                        type="button"
                        onClick={() => handleDeleteScenario(item.id)}
                        title="Usuń symulację"
                        className="text-slate-400 hover:text-rose-600"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                    <div className="mt-2 flex flex-wrap gap-x-4 gap-y-1 text-xs text-slate-600">
                      <span>
                        Urlopy: <strong>{item.totalLeaveDays} dni</strong>
                      </span>
                      <span>
                        Suma brutto:{' '}
                        <strong className="text-emerald-800">{fmtPln(item.totalEstimatedBenefitsGross)}</strong>
                      </span>
                    </div>
                    <div className="mt-2.5 flex items-center justify-between">
                      <span className="text-[11px] text-slate-400">
                        {new Date(item.savedUtc).toLocaleString('pl-PL')}
                      </span>
                      <button
                        type="button"
                        onClick={() => onLoadSavedRequest(item.request)}
                        className="text-xs font-bold text-emerald-700 hover:underline"
                      >
                        Wczytaj do kalkulatora
                      </button>
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {/* Tabela porównawcza 2 zaznaczonych wariantów */}
          {comparedItems.length === 2 && (
            <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
              <div className="mb-2 flex items-center gap-2 text-xs font-bold uppercase tracking-wide text-slate-700">
                <GitCompare className="h-4 w-4 text-emerald-700" />
                Porównanie wybranych wariantów
              </div>
              <div className="overflow-x-auto">
                <table className="w-full text-left text-xs">
                  <thead className="border-b border-slate-200 text-slate-500">
                    <tr>
                      <th className="py-1.5 pr-4">Wskaźnik</th>
                      <th className="py-1.5 pr-4">{comparedItems[0].name}</th>
                      <th className="py-1.5 pr-4">{comparedItems[1].name}</th>
                      <th className="py-1.5">Różnica (B − A)</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200/70">
                    <tr>
                      <td className="py-2 pr-4 font-medium text-slate-700">Łączny okres urlopów</td>
                      <td className="py-2 pr-4 font-bold">{comparedItems[0].totalLeaveDays} dni</td>
                      <td className="py-2 pr-4 font-bold">{comparedItems[1].totalLeaveDays} dni</td>
                      <td className="py-2 font-bold text-blue-700">
                        {comparedItems[1].totalLeaveDays - comparedItems[0].totalLeaveDays >= 0 ? '+' : ''}
                        {comparedItems[1].totalLeaveDays - comparedItems[0].totalLeaveDays} dni
                      </td>
                    </tr>
                    <tr>
                      <td className="py-2 pr-4 font-medium text-slate-700">Łączna suma świadczeń (brutto)</td>
                      <td className="py-2 pr-4 font-bold">{fmtPln(comparedItems[0].totalEstimatedBenefitsGross)}</td>
                      <td className="py-2 pr-4 font-bold">{fmtPln(comparedItems[1].totalEstimatedBenefitsGross)}</td>
                      <td className="py-2 font-bold text-emerald-700">
                        {comparedItems[1].totalEstimatedBenefitsGross - comparedItems[0].totalEstimatedBenefitsGross >= 0
                          ? '+'
                          : ''}
                        {fmtPln(
                          comparedItems[1].totalEstimatedBenefitsGross - comparedItems[0].totalEstimatedBenefitsGross
                        )}
                      </td>
                    </tr>
                    <tr>
                      <td className="py-2 pr-4 font-medium text-slate-700">Podstawa wymiaru (po 13,71%)</td>
                      <td className="py-2 pr-4">{fmtPln(comparedItems[0].primaryBenefitBase)}</td>
                      <td className="py-2 pr-4">{fmtPln(comparedItems[1].primaryBenefitBase)}</td>
                      <td className="py-2">
                        {fmtPln(comparedItems[1].primaryBenefitBase - comparedItems[0].primaryBenefitBase)}
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </div>
      )}

      <div className="grid grid-cols-1 gap-6 xl:grid-cols-12">
        {/* Lewa kolumna: Zintegrowany formularz */}
        <form onSubmit={handleSubmit} className="space-y-5 xl:col-span-5">
          {/* Sekcja 1: Zatrudnienie i podstawa */}
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
            <h3 className="mb-4 flex items-center gap-2 text-base font-bold text-slate-900">
              <Briefcase className="h-5 w-5 text-emerald-700" />
              1. Tytuł ubezpieczenia i wynagrodzenie
            </h3>

            <div className="space-y-3">
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-700">Forma zatrudnienia / tytuł</label>
                <select
                  className="w-full"
                  value={request.employmentTitle}
                  onChange={(e) => update('employmentTitle', e.target.value as EmploymentTitle)}
                >
                  <option value="Employee">Umowa o pracę (obowiązkowe chorobowe)</option>
                  <option value="SelfEmployed">Działalność gospodarcza — JDG (dobrowolne chorobowe)</option>
                  <option value="MandateContract">Umowa zlecenie (dobrowolne chorobowe)</option>
                  <option value="ContractForSpecificWork">Umowa o dzieło (brak chorobowego)</option>
                  <option value="UninsuredOrUlgaNaStart">Brak ubezpieczenia / Ulga na start / Student &lt;26</option>
                </select>
              </div>

              {isVoluntary && (
                <label className="flex items-center gap-2 text-sm text-slate-800">
                  <input
                    type="checkbox"
                    checked={request.hasVoluntarySicknessInsurance}
                    onChange={(e) => update('hasVoluntarySicknessInsurance', e.target.checked)}
                  />
                  Zgłoszono do dobrowolnego ubezpieczenia chorobowego (ZUS ZUA)
                </label>
              )}

              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div>
                  <label className="mb-1 block text-xs font-semibold text-slate-700">
                    Średnie wynagrodzenie / podstawa brutto (12 mies.)
                  </label>
                  <input
                    type="number"
                    step="100"
                    min={0}
                    className="w-full"
                    value={request.averageMonthlyGross}
                    onChange={(e) => {
                      onChangeRequest({
                        ...request,
                        averageMonthlyGross: Number(e.target.value),
                        monthlySalaries: [],
                      });
                    }}
                  />
                  {request.monthlySalaries.length > 0 && (
                    <p className="mt-1 text-xs text-emerald-700">
                      Aktywna tabela Z-3 ({request.monthlySalaries.length} mies. z dokumentu)
                    </p>
                  )}
                </div>

                <div>
                  <label className="mb-1 block text-xs font-semibold text-slate-700">
                    Wymiar etatu (np. 1 lub 0.63)
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    min={0.1}
                    max={1}
                    className="w-full"
                    value={request.fteFraction}
                    onChange={(e) => update('fteFraction', Number(e.target.value))}
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div>
                  <label className="mb-1 block text-xs font-semibold text-slate-700">
                    Liczba ubezpieczonych u płatnika (na 30.11)
                  </label>
                  <input
                    type="number"
                    min={1}
                    className="w-full"
                    value={request.employerInsuredCount}
                    onChange={(e) => update('employerInsuredCount', Number(e.target.value))}
                  />
                  <span className="text-[11px] text-slate-500">&le;20: płaci ZUS | &gt;20: płaci pracodawca</span>
                </div>

                <div>
                  <label className="mb-1 block text-xs font-semibold text-slate-700">
                    Nieprzerwany staż chorobowy (dni)
                  </label>
                  <input
                    type="number"
                    min={0}
                    className="w-full"
                    value={request.continuousInsuranceDays}
                    onChange={(e) => update('continuousInsuranceDays', Number(e.target.value))}
                  />
                </div>
              </div>

              {request.employmentTitle === 'SelfEmployed' && (
                <div>
                  <label className="mb-1 block text-xs font-semibold text-rose-700">
                    Stan zadłużenia składek ZUS na dzień powstania prawa (zł)
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    min={0}
                    className="w-full"
                    value={request.jdgSocialSecurityDebt}
                    onChange={(e) => update('jdgSocialSecurityDebt', Number(e.target.value))}
                  />
                </div>
              )}

              {request.employmentTitle === 'Employee' && (
                <div>
                  <label className="mb-1 block text-xs font-semibold text-slate-700">
                    Sytuacja szczególna zatrudnienia (ustanie umowy)
                  </label>
                  <select
                    className="w-full"
                    value={request.terminationReason}
                    onChange={(e) => update('terminationReason', e.target.value as SpecialTerminationReason)}
                  >
                    <option value="None">Zatrudnienie trwa bez zmian</option>
                    <option value="FixedTermExtendedToBirth">Umowa na czas określony przedłużona do dnia porodu</option>
                    <option value="EmployerBankruptcyOrLiquidation">Upadłość lub likwidacja pracodawcy w ciąży</option>
                    <option value="EmployerDeath">Śmierć pracodawcy w ciąży</option>
                    <option value="UnlawfulTerminationCourtConfirmed">Naruszenie prawa przy rozwiązaniu (wyrok sądu)</option>
                    <option value="StandardTerminationBeforeBirth">Zakończenie ubezpieczenia przed porodem (utrata tytułu)</option>
                  </select>
                </div>
              )}

              <div className="flex flex-wrap gap-4 pt-1 text-xs text-slate-700">
                <label className="flex items-center gap-1.5">
                  <input
                    type="checkbox"
                    checked={request.suddenBaseIncreaseInPregnancy}
                    onChange={(e) => update('suddenBaseIncreaseInPregnancy', e.target.checked)}
                  />
                  Nagłe podwyższenie podstawy w ciąży
                </label>
                <label className="flex items-center gap-1.5">
                  <input
                    type="checkbox"
                    checked={request.shortContractJustBeforePregnancy}
                    onChange={(e) => update('shortContractJustBeforePregnancy', e.target.checked)}
                  />
                  Krótka umowa tuż przed ciążą
                </label>
              </div>
            </div>
          </section>

          {/* Sekcja 2: Scenariusz i dziecko */}
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
            <h3 className="mb-4 flex items-center gap-2 text-base font-bold text-slate-900">
              <Baby className="h-5 w-5 text-emerald-700" />
              2. Scenariusz rodzicielski i dane dziecka
            </h3>

            <div className="space-y-3">
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-700">Rodzaj sytuacji</label>
                <select
                  className="w-full"
                  value={request.scenario}
                  onChange={(e) => update('scenario', e.target.value as ParenthoodScenario)}
                >
                  <option value="BiologicalBirth">Urodzenie dziecka</option>
                  <option value="Adoption">Adopcja (wniosek o przysposobienie)</option>
                  <option value="FosterCareNonProfessional">Rodzina zastępcza niezawodowa</option>
                  <option value="StillbirthOrChildDeath">Sytuacja trudna: urodzenie martwego dziecka / zgon dziecka</option>
                </select>
              </div>

              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <div>
                  <label className="mb-1 block text-xs font-semibold text-slate-700">
                    {request.scenario === 'BiologicalBirth' || request.scenario === 'StillbirthOrChildDeath'
                      ? 'Data porodu (planowana / faktyczna)'
                      : 'Data przyjęcia dziecka na wychowanie'}
                  </label>
                  <input
                    type="date"
                    className="w-full"
                    value={request.eventDate}
                    onChange={(e) => update('eventDate', e.target.value)}
                  />
                </div>

                {request.scenario !== 'StillbirthOrChildDeath' && (
                  <div>
                    <label className="mb-1 block text-xs font-semibold text-slate-700">Liczba dzieci</label>
                    <select
                      className="w-full"
                      value={request.childrenCount}
                      onChange={(e) => update('childrenCount', Number(e.target.value))}
                    >
                      <option value={1}>1 dziecko (20 tyg. + 41 tyg.)</option>
                      <option value={2}>2 dzieci / bliźniaki (31 tyg. + 43 tyg.)</option>
                      <option value={3}>3 dzieci (33 tyg. + 43 tyg.)</option>
                      <option value={4}>4 dzieci (35 tyg. + 43 tyg.)</option>
                      <option value={5}>5+ dzieci (37 tyg. + 43 tyg.)</option>
                    </select>
                  </div>
                )}
              </div>

              {(request.scenario === 'Adoption' || request.scenario === 'FosterCareNonProfessional') && (
                <div className="grid grid-cols-1 gap-3 rounded-xl bg-slate-50 p-3 sm:grid-cols-2">
                  <div>
                    <label className="mb-1 block text-xs font-semibold text-slate-700">Data urodzenia dziecka</label>
                    <input
                      type="date"
                      className="w-full"
                      value={request.childBirthDate ?? ''}
                      onChange={(e) => update('childBirthDate', e.target.value || null)}
                    />
                  </div>
                  {request.scenario === 'FosterCareNonProfessional' && (
                    <label className="flex items-center gap-2 self-end pb-2 text-xs text-slate-700">
                      <input
                        type="checkbox"
                        checked={request.hasSchoolDeferralDecision}
                        onChange={(e) => update('hasSchoolDeferralDecision', e.target.checked)}
                      />
                      Odroczenie obowiązku szkolnego (do 10 lat)
                    </label>
                  )}
                </div>
              )}

              {request.scenario !== 'StillbirthOrChildDeath' && (
                <div className="space-y-2 pt-1">
                  <label className="flex items-center gap-2 text-sm text-slate-800">
                    <input
                      type="checkbox"
                      checked={request.hasZaZyciemCertificate}
                      onChange={(e) => update('hasZaZyciemCertificate', e.target.checked)}
                    />
                    Dziecko posiada zaświadczenie „Za życiem” (wydłużony urlop 65/67 tyg.)
                  </label>

                  <label className="flex items-center gap-2 text-sm text-slate-800">
                    <input
                      type="checkbox"
                      checked={request.calculateSupplementaryMaternity}
                      onChange={(e) => update('calculateSupplementaryMaternity', e.target.checked)}
                    />
                    Wcześniak / hospitalizacja — uzupełniający urlop macierzyński (art. 180² KP)
                  </label>

                  {request.calculateSupplementaryMaternity && (
                    <div className="grid grid-cols-1 gap-3 rounded-xl bg-amber-50/70 p-3 sm:grid-cols-3">
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-700">Tydzień ciąży</label>
                        <input
                          type="number"
                          min={20}
                          max={42}
                          className="w-full"
                          value={request.gestationWeekAtBirth ?? 34}
                          onChange={(e) => update('gestationWeekAtBirth', Number(e.target.value))}
                        />
                      </div>
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-700">Najniższa masa (g)</label>
                        <input
                          type="number"
                          min={300}
                          max={6000}
                          className="w-full"
                          value={request.lowestBirthWeightGrams ?? 1500}
                          onChange={(e) => update('lowestBirthWeightGrams', Number(e.target.value))}
                        />
                      </div>
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-700">Dni w szpitalu</label>
                        <input
                          type="number"
                          min={0}
                          max={120}
                          className="w-full"
                          value={request.hospitalizationDaysWithinLimit}
                          onChange={(e) => update('hospitalizationDaysWithinLimit', Number(e.target.value))}
                        />
                      </div>
                      <label className="col-span-full flex items-center gap-2 text-xs text-slate-700">
                        <input
                          type="checkbox"
                          checked={request.termBabyHospitalizedMin2DaysBetweenDay5And28}
                          onChange={(e) =>
                            update('termBabyHospitalizedMin2DaysBetweenDay5And28', e.target.checked)
                          }
                        />
                        Dla dziecka &ge;37 tyg.: pobyt min. 2 kolejne dni rozpoczęty między 5. a 28. dniem po porodzie
                      </label>
                    </div>
                  )}
                </div>
              )}
            </div>
          </section>

          {/* Sekcja 3: Podział między oboje rodziców i łączenie z pracą */}
          {request.scenario !== 'StillbirthOrChildDeath' && (
            <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs">
              <h3 className="mb-4 flex items-center gap-2 text-base font-bold text-slate-900">
                <Users className="h-5 w-5 text-emerald-700" />
                3. Stawka, podział opieki (2 rodziców) i łączenie z pracą
              </h3>

              <div className="space-y-3">
                <label className="flex items-center gap-2 text-sm font-medium text-slate-800">
                  <input
                    type="checkbox"
                    checked={request.submitFullApplicationWithin21Days}
                    onChange={(e) => update('submitFullApplicationWithin21Days', e.target.checked)}
                  />
                  Wniosek o pełny wymiar w ciągu 21 dni (stawka 81,5% zamiast 100% / 70%)
                </label>

                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <div>
                    <label className="mb-1 block text-xs font-semibold text-slate-700">
                      Urlop macierzyński matki (min. 14 tyg.)
                    </label>
                    <input
                      type="number"
                      min={9}
                      max={37}
                      className="w-full"
                      value={request.motherMaternityWeeksTaken}
                      onChange={(e) => update('motherMaternityWeeksTaken', Number(e.target.value))}
                    />
                  </div>
                  <div>
                    <label className="mb-1 block text-xs font-semibold text-slate-700">
                      Urlop rodzicielski głównego rodzica (tyg.)
                    </label>
                    <input
                      type="number"
                      min={0}
                      max={58}
                      className="w-full"
                      value={request.primaryParentParentalWeeks}
                      onChange={(e) => update('primaryParentParentalWeeks', Number(e.target.value))}
                    />
                  </div>
                </div>

                <div className="rounded-xl bg-slate-50 p-3 space-y-3">
                  <label className="flex items-center gap-2 text-sm font-medium text-slate-800">
                    <input
                      type="checkbox"
                      checked={request.secondParentInsured}
                      onChange={(e) => update('secondParentInsured', e.target.checked)}
                    />
                    Drugi rodzic (ojciec) jest ubezpieczony chorobowo
                  </label>

                  {request.secondParentInsured && (
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-700">
                          Wynagrodzenie brutto 2. rodzica
                        </label>
                        <input
                          type="number"
                          step="100"
                          min={0}
                          className="w-full"
                          value={request.secondParentAverageMonthlyGross}
                          onChange={(e) => update('secondParentAverageMonthlyGross', Number(e.target.value))}
                        />
                      </div>
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-700">
                          Tygodnie rodzicielskiego 2. rodzica (9 tyg. wyłączne)
                        </label>
                        <input
                          type="number"
                          min={0}
                          max={43}
                          className="w-full"
                          value={request.secondParentParentalWeeks}
                          onChange={(e) => update('secondParentParentalWeeks', Number(e.target.value))}
                        />
                      </div>
                      <label className="col-span-full flex items-center gap-2 text-xs text-slate-700">
                        <input
                          type="checkbox"
                          checked={request.includePaternityLeave}
                          onChange={(e) => update('includePaternityLeave', e.target.checked)}
                        />
                        Uwzględnij 2-tygodniowy urlop ojcowski (100% podstawy)
                      </label>
                    </div>
                  )}
                </div>

                {/* Łączenie z pracą */}
                {request.employmentTitle === 'Employee' && (
                  <div className="rounded-xl border border-indigo-100 bg-indigo-50/50 p-3 space-y-2">
                    <div className="text-xs font-bold text-indigo-900">
                      Łączenie urlopu rodzicielskiego z pracą (maks. 0,5 etatu — art. 182¹f KP)
                    </div>
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-700">
                          Wymiar pracy (0 – 0.5)
                        </label>
                        <input
                          type="number"
                          step="0.05"
                          min={0}
                          max={0.75}
                          className="w-full"
                          value={request.combineWorkWithParentalFte}
                          onChange={(e) => update('combineWorkWithParentalFte', Number(e.target.value))}
                        />
                      </div>
                      <div>
                        <label className="mb-1 block text-xs font-semibold text-slate-700">
                          Tygodni łączonych z pracą
                        </label>
                        <input
                          type="number"
                          min={0}
                          max={58}
                          className="w-full"
                          value={request.parentalWeeksCombinedWithWork}
                          onChange={(e) => update('parentalWeeksCombinedWithWork', Number(e.target.value))}
                        />
                      </div>
                    </div>
                    <label className="flex items-center gap-2 text-xs text-slate-700">
                      <input
                        type="checkbox"
                        checked={request.combineExtendedPartWithWork}
                        onChange={(e) => update('combineExtendedPartWithWork', e.target.checked)}
                      />
                      Łączę z pracą również powstałą część wydłużoną (art. 182¹f § 7 KP)
                    </label>
                  </div>
                )}
              </div>
            </section>
          )}

          {/* Sekcja 4: Chorobowe w ciąży i zasiłek opiekuńczy */}
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-xs space-y-3">
            <h3 className="flex items-center gap-2 text-base font-bold text-slate-900">
              <HeartPulse className="h-5 w-5 text-emerald-700" />
              4. Dodatkowe świadczenia (ciąża L4 i opiekuńczy)
            </h3>

            <label className="flex items-center gap-2 text-sm font-medium text-slate-800">
              <input
                type="checkbox"
                checked={request.includePregnancySickness}
                onChange={(e) => update('includePregnancySickness', e.target.checked)}
              />
              Policz zwolnienie lekarskie w ciąży (e-ZLA kod B)
            </label>

            {request.includePregnancySickness && (
              <div className="grid grid-cols-1 gap-3 rounded-xl bg-slate-50 p-3 sm:grid-cols-2">
                <div>
                  <label className="mb-1 block text-xs font-semibold text-slate-700">Liczba dni L4 w ciąży</label>
                  <input
                    type="number"
                    min={1}
                    max={360}
                    className="w-full"
                    value={request.plannedPregnancySickDays}
                    onChange={(e) => update('plannedPregnancySickDays', Number(e.target.value))}
                  />
                </div>
                <label className="flex items-center gap-2 self-end pb-2 text-xs text-slate-700">
                  <input
                    type="checkbox"
                    checked={request.hasCodeBOnEzla}
                    onChange={(e) => update('hasCodeBOnEzla', e.target.checked)}
                  />
                  Kod literowy „B” na e-ZLA (100%)
                </label>
              </div>
            )}

            <label className="flex items-center gap-2 text-sm font-medium text-slate-800">
              <input
                type="checkbox"
                checked={request.includeCareAllowance}
                onChange={(e) => update('includeCareAllowance', e.target.checked)}
              />
              <Stethoscope className="h-4 w-4 text-slate-500" />
              Policz zasiłek opiekuńczy (80% podstawy)
            </label>

            {request.includeCareAllowance && (
              <div className="grid grid-cols-1 gap-3 rounded-xl bg-slate-50 p-3 sm:grid-cols-2">
                <div>
                  <label className="mb-1 block text-xs font-semibold text-slate-700">Opieka nad</label>
                  <select
                    className="w-full"
                    value={request.careRecipientType}
                    onChange={(e) => update('careRecipientType', Number(e.target.value))}
                  >
                    <option value={0}>Dziecko do 14 lat (limit 60 dni)</option>
                    <option value={1}>Dziecko niepełnosprawne 14–18 lat (30 dni)</option>
                    <option value={2}>Inny członek rodziny / &gt;14 lat (14 dni)</option>
                  </select>
                </div>
                <div>
                  <label className="mb-1 block text-xs font-semibold text-slate-700">Liczba dni opieki</label>
                  <input
                    type="number"
                    min={1}
                    max={60}
                    className="w-full"
                    value={request.careAllowanceDays}
                    onChange={(e) => update('careAllowanceDays', Number(e.target.value))}
                  />
                </div>
              </div>
            )}
          </section>

          <button
            type="submit"
            disabled={loading}
            className="flex w-full items-center justify-center gap-2 rounded-xl bg-emerald-700 px-5 py-3.5 text-sm font-bold text-white shadow-sm transition hover:bg-emerald-800 disabled:opacity-60"
          >
            {loading ? <RefreshCw className="h-4 w-4 animate-spin" /> : <Calculator className="h-4 w-4" />}
            {loading ? 'Przeliczanie...' : 'Przelicz uprawnienia i zaktualizuj oś czasu'}
          </button>

          {error && (
            <div className="rounded-xl border border-rose-300 bg-rose-50 p-3 text-sm text-rose-800">{error}</div>
          )}
        </form>

        {/* Prawa kolumna: Wyniki */}
        <div className="xl:col-span-7">
          {result ? (
            <ResultsPanel result={result} />
          ) : (
            <div className="rounded-2xl border border-dashed border-slate-300 bg-white p-10 text-center text-slate-500">
              Wypełnij dane po lewej stronie i kliknij „Przelicz uprawnienia”.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}