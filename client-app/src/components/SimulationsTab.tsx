import { useEffect, useState } from 'react';
import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  ExternalLink,
  FileCheck,
  FileSpreadsheet,
  MessageSquare,
  Send,
  Shield,
  Smartphone,
} from 'lucide-react';
import { api } from '../api/client';
import type {
  CalculationRequest,
  PersonaListItem,
  SimulatedZusAccount,
  SubmittedApplicationRecord,
} from '../types/api';

interface Props {
  personas: PersonaListItem[];
  loggedAccount: SimulatedZusAccount | null;
  onLoginPersona: (personaId: string) => Promise<void>;
  onLogout: () => void;
  onLoadIntoCalculator: (req: CalculationRequest) => void;
}

export function SimulationsTab({
  personas,
  loggedAccount,
  onLoginPersona,
  onLoadIntoCalculator,
}: Props) {
  const [applications, setApplications] = useState<SubmittedApplicationRecord[]>([]);

  // PUE/eZUS Q&A
  const [pueQuestion, setPueQuestion] = useState('');
  const [pueReply, setPueReply] = useState<{ ticketId: string; answer: string } | null>(null);

  // Generator wniosków (Urlop KP / ZAM / ZAS-53 / USC)
  const [appCategory, setAppCategory] = useState('ZUS_ZAM');
  const [applicantName, setApplicantName] = useState(loggedAccount?.fullName ?? 'Anna Kowalska');
  const [childInfo, setChildInfo] = useState('2026-11-21');
  const [recipient, setRecipient] = useState('Zakład Ubezpieczeń Społecznych (eZUS)');
  const [details, setDetails] = useState('Wniosek o pełny wymiar urlopu macierzyńskiego i rodzicielskiego (stawka 81,5%).');
  const [submitNotice, setSubmitNotice] = useState<string | null>(null);

  const refreshApps = () => {
    api.getApplications().then(setApplications).catch(() => {});
  };

  useEffect(() => {
    refreshApps();
  }, []);

  useEffect(() => {
    if (loggedAccount) {
      setApplicantName(loggedAccount.fullName);
    }
  }, [loggedAccount]);

  const handleLogin = async (personaId: string) => {
    await onLoginPersona(personaId);
  };

  const handleAskPue = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!pueQuestion.trim()) return;
    const res = await api.askPue(pueQuestion);
    setPueReply({ ticketId: res.ticketId, answer: res.answer });
    setPueQuestion('');
    refreshApps();
  };

  const handleSubmitApp = async (e: React.FormEvent) => {
    e.preventDefault();
    const saved = await api.submitApplication({
      category: appCategory,
      applicantName,
      childNameOrEventDate: childInfo,
      recipient,
      details,
    });
    setSubmitNotice(`Złożono pomyślnie! Numer referencyjny UPO (symulacja): ${saved.id}`);
    refreshApps();
  };

  return (
    <div className="space-y-6">
      {/* 1. Symulacja logowania mObywatel i pobrania konta ZUS */}
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs">
        <div className="flex items-center gap-2 text-base font-bold text-slate-900">
          <Smartphone className="h-5 w-5 text-rose-600" />
          1. Symulacja logowania przez mObywatel i pobrania danych z ZUS
        </div>
        <p className="mt-1 text-sm text-slate-600">
          Wybierz profil demonstracyjny, aby zasymulować autoryzację mObywatel i pobrać status ubezpieczenia
          chorobowego, stan zadłużenia składek (JDG) oraz listę zaświadczeń e-ZLA.
        </p>

        <div className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          {personas.map((p) => (
            <button
              key={p.personaId}
              type="button"
              onClick={() => handleLogin(p.personaId)}
              className={`rounded-xl border p-4 text-left transition ${
                loggedAccount?.personaId === p.personaId
                  ? 'border-emerald-600 bg-emerald-50/70 ring-2 ring-emerald-600/20'
                  : 'border-slate-200 bg-slate-50/60 hover:border-emerald-400'
              }`}
            >
              <div className="font-bold text-slate-900">{p.fullName}</div>
              <div className="mt-1 text-[11px] font-semibold text-emerald-800">Kod tytułu: {p.insuranceTitleCode}</div>
              <div className="mt-1 text-xs text-slate-600">{p.insuranceTitleDescription}</div>
              {p.socialSecurityDebtPln > 0 && (
                <div className="mt-2 inline-flex items-center gap-1 rounded bg-rose-100 px-2 py-0.5 text-[11px] font-bold text-rose-800">
                  <AlertTriangle className="h-3 w-3" /> Zaległość: {p.socialSecurityDebtPln.toFixed(2)} zł
                </div>
              )}
            </button>
          ))}
        </div>

        {/* Widok pobranego konta ZUS */}
        {loggedAccount && (
          <div className="mt-5 rounded-xl border border-emerald-200 bg-slate-50 p-5">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <div className="flex items-center gap-2 text-sm font-bold text-slate-900">
                  <Shield className="h-4 w-4 text-emerald-700" />
                  Zalogowano przez mObywatel: {loggedAccount.fullName} (PESEL: {loggedAccount.maskedPesel})
                </div>
                <div className="mt-1 text-xs text-slate-600">
                  Tytuł: <strong>{loggedAccount.insuranceTitleCode}</strong> — {loggedAccount.insuranceTitleDescription} |
                  Staż chorobowy: <strong>{loggedAccount.continuousInsuranceDays} dni</strong>
                </div>
              </div>
              <button
                type="button"
                onClick={() => onLoadIntoCalculator(loggedAccount.prefilledCalculatorData)}
                className="inline-flex items-center gap-1.5 rounded-xl bg-emerald-700 px-4 py-2.5 text-xs font-bold text-white hover:bg-emerald-800"
              >
                Wczytaj dane z ZUS do kalkulatora
                <ArrowRight className="h-3.5 w-3.5" />
              </button>
            </div>

            <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2">
              <div
                className={`rounded-xl border p-4 ${
                  loggedAccount.socialSecurityDebtPln > 0
                    ? 'border-rose-300 bg-rose-50 text-rose-900'
                    : 'border-emerald-200 bg-white text-slate-800'
                }`}
              >
                <div className="text-xs font-bold uppercase tracking-wide opacity-75">
                  Stan zadłużenia składek (symulacja zaświadczenia ZUS)
                </div>
                <div className="mt-1 text-lg font-bold">
                  {loggedAccount.socialSecurityDebtPln.toFixed(2)} zł
                </div>
                <div className="mt-1 text-xs">{loggedAccount.debtCertificateStatus}</div>
              </div>

              <div className="rounded-xl border border-slate-200 bg-white p-4">
                <div className="flex items-center gap-1.5 text-xs font-bold uppercase tracking-wide text-slate-500">
                  <FileSpreadsheet className="h-4 w-4 text-emerald-700" />
                  Zwolnienia lekarskie e-ZLA (symulacja rejestru ZUS)
                </div>
                {loggedAccount.ezlaCertificates.length === 0 ? (
                  <p className="mt-2 text-xs text-slate-500">Brak wystawionych zaświadczeń e-ZLA w bieżącym okresie.</p>
                ) : (
                  <ul className="mt-2 space-y-2 text-xs">
                    {loggedAccount.ezlaCertificates.map((z) => (
                      <li key={z.seriesNumber} className="rounded-lg bg-slate-50 p-2">
                        <div className="font-semibold text-slate-900">
                          {z.seriesNumber} | {z.from} → {z.to} ({z.days} dni, kod „{z.code}”)
                        </div>
                        <div className="text-slate-600">{z.doctorNote}</div>
                      </li>
                    ))}
                  </ul>
                )}
              </div>
            </div>
          </div>
        )}
      </div>

      {/* 2. Generator wniosków (Urlop KP / ZAM / ZAS-53 / USC) oraz PUE/eZUS */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <form onSubmit={handleSubmitApp} className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs space-y-4">
          <div className="flex items-center justify-between">
            <h3 className="flex items-center gap-2 text-base font-bold text-slate-900">
              <FileCheck className="h-5 w-5 text-emerald-700" />
              2. Symulacja złożenia wniosku (Urlop / ZUS / USC)
            </h3>
            <a
              href="https://www.gov.pl/web/cyfryzacja/rejestr-stanu-cywilnego"
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 text-xs font-semibold text-emerald-700 hover:underline"
            >
              Portal USC <ExternalLink className="h-3 w-3" />
            </a>
          </div>

          <div>
            <label className="mb-1 block text-xs font-semibold text-slate-700">Rodzaj wniosku</label>
            <select
              className="w-full"
              value={appCategory}
              onChange={(e) => {
                const val = e.target.value;
                setAppCategory(val);
                if (val === 'URLOP_PRACODAWCA') setRecipient('Pracodawca (Dział Kadr)');
                else if (val === 'USC_AKT') setRecipient('Urząd Stanu Cywilnego (Rejestr Stanu Cywilnego)');
                else setRecipient('Zakład Ubezpieczeń Społecznych (eZUS)');
              }}
            >
              <option value="ZUS_ZAM">Wniosek ZAM — zasiłek macierzyński / rodzicielski / ojcowski (ZUS)</option>
              <option value="URLOP_PRACODAWCA">Wniosek pracowniczy o urlop macierzyński / rodzicielski (KP)</option>
              <option value="ZUS_ZAS53">Wniosek ZAS-53 — zasiłek chorobowy w ciąży (JDG)</option>
              <option value="USC_AKT">Wniosek o odpis skrócony/zupełny aktu urodzenia dziecka (USC)</option>
            </select>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">Wnioskodawca</label>
              <input
                type="text"
                className="w-full"
                value={applicantName}
                onChange={(e) => setApplicantName(e.target.value)}
                required
              />
            </div>
            <div>
              <label className="mb-1 block text-xs font-semibold text-slate-700">Data zdarzenia / imię dziecka</label>
              <input
                type="text"
                className="w-full"
                value={childInfo}
                onChange={(e) => setChildInfo(e.target.value)}
                required
              />
            </div>
          </div>

          <div>
            <label className="mb-1 block text-xs font-semibold text-slate-700">Adresat</label>
            <input
              type="text"
              className="w-full"
              value={recipient}
              onChange={(e) => setRecipient(e.target.value)}
              required
            />
          </div>

          <div>
            <label className="mb-1 block text-xs font-semibold text-slate-700">Treść / szczegóły wniosku</label>
            <textarea
              rows={3}
              className="w-full"
              value={details}
              onChange={(e) => setDetails(e.target.value)}
            />
          </div>

          <button
            type="submit"
            className="inline-flex w-full items-center justify-center gap-2 rounded-xl bg-emerald-700 px-4 py-2.5 text-sm font-bold text-white hover:bg-emerald-800"
          >
            <Send className="h-4 w-4" />
            Wyślij wniosek (symulacja e-Doręczeń / eZUS)
          </button>

          {submitNotice && (
            <div className="flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-xs font-semibold text-emerald-900">
              <CheckCircle2 className="h-4 w-4 shrink-0" />
              {submitNotice}
            </div>
          )}
        </form>

        {/* 3. Symulacja połączenia z PUE/eZUS (Zadaj pytanie) */}
        <div className="flex flex-col justify-between rounded-2xl border border-slate-200 bg-white p-6 shadow-xs space-y-4">
          <div className="space-y-4">
            <h3 className="flex items-center gap-2 text-base font-bold text-slate-900">
              <MessageSquare className="h-5 w-5 text-emerald-700" />
              3. Symulacja kontaktu z PUE / eZUS (baza wiedzy ZUS 2026)
            </h3>
            <p className="text-xs text-slate-600">
              Zapytaj np. o <em>zadłużenie JDG</em>, <em>termin 21 dni i 81,5%</em>, <em>likwidację ZUR/ZAO/Z-3b</em>,{' '}
              <em>uzupełniający urlop dla wcześniaka</em>, <em>akt urodzenia z USC</em> lub <em>zasiłek pogrzebowy</em>.
            </p>

            <form onSubmit={handleAskPue} className="space-y-3">
              <textarea
                rows={3}
                className="w-full"
                placeholder="Wpisz swoje pytanie do konsultanta PUE/eZUS..."
                value={pueQuestion}
                onChange={(e) => setPueQuestion(e.target.value)}
              />
              <button
                type="submit"
                className="inline-flex items-center gap-2 rounded-xl bg-slate-900 px-4 py-2.5 text-xs font-bold text-white hover:bg-slate-800"
              >
                <Send className="h-3.5 w-3.5" />
                Wyślij zapytanie przez PUE/eZUS
              </button>
            </form>

            {pueReply && (
              <div className="rounded-xl border border-emerald-200 bg-emerald-50/70 p-4 text-sm text-slate-800">
                <div className="text-xs font-bold text-emerald-900">
                  Odpowiedź z bazy wiedzy ZUS (zgłoszenie {pueReply.ticketId}):
                </div>
                <p className="mt-1.5 leading-relaxed">{pueReply.answer}</p>
              </div>
            )}
          </div>

          <div className="rounded-xl bg-slate-50 p-3 text-xs text-slate-600">
            Oficjalny kontakt ZUS dla rodziców: infolinia <strong>22 560 16 00</strong> | e-mail:{' '}
            <strong>mama@zus.pl</strong> |{' '}
            <a href="https://www.zus.pl/e-wizyta" target="_blank" rel="noreferrer" className="text-emerald-700 underline">
              umów e-wizytę
            </a>
          </div>
        </div>
      </div>

      {/* Historia złożonych wniosków w SQLite */}
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs">
        <h3 className="text-base font-bold text-slate-900">
          Rejestr wysłanych wniosków i zapytań
        </h3>
        {applications.length === 0 ? (
          <p className="mt-2 text-sm text-slate-500">Brak złożonych wniosków w tej sesji testowej.</p>
        ) : (
          <div className="mt-3 overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="border-b border-slate-200 text-slate-500">
                <tr>
                  <th className="py-2 pr-4">Nr ref.</th>
                  <th className="py-2 pr-4">Rodzaj</th>
                  <th className="py-2 pr-4">Adresat</th>
                  <th className="py-2 pr-4">Szczegóły</th>
                  <th className="py-2">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {applications.map((a) => (
                  <tr key={a.id}>
                    <td className="py-2.5 pr-4 font-mono font-semibold text-slate-900">{a.id}</td>
                    <td className="py-2.5 pr-4 font-medium text-slate-800">{a.title}</td>
                    <td className="py-2.5 pr-4 text-slate-600">{a.recipient}</td>
                    <td className="py-2.5 pr-4 text-slate-600">{a.payloadSummary}</td>
                    <td className="py-2.5 font-semibold text-emerald-700">{a.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}