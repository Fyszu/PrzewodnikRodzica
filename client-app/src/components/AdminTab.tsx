import { useEffect, useState } from 'react';
import {
  AlertCircle,
  CheckCircle2,
  Cpu,
  Gauge,
  Lock,
  LogOut,
  RotateCcw,
  Save,
  Unlock,
} from 'lucide-react';
import { api } from '../api/client';
import type { AiDisclosureResponse, BenefitParameters, QuotaStatus } from '../types/api';

export function AdminTab() {
  const [pinInput, setPinInput] = useState('ZUS2026');
  const [isAdmin, setIsAdmin] = useState(false);
  const [busy, setBusy] = useState(false);
  const [params, setParams] = useState<BenefitParameters | null>(null);
  const [quota, setQuota] = useState<QuotaStatus | null>(null);
  const [aiInfo, setAiInfo] = useState<AiDisclosureResponse | null>(null);
  const [message, setMessage] = useState<{ type: 'ok' | 'err'; text: string } | null>(null);

  useEffect(() => {
    api.getAdminStatus().then((s) => setIsAdmin(s.isAdmin)).catch(() => {});
    api.getAdminParameters().then(setParams).catch(() => {});
    api.getQuota().then(setQuota).catch(() => {});
    api.getAiDisclosure().then(setAiInfo).catch(() => {});
  }, []);

  const updateField = <K extends keyof BenefitParameters>(key: K, val: number) => {
    if (!params) return;
    setParams({ ...params, [key]: val });
  };

  const handleAdminLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setMessage(null);
    setBusy(true);
    try {
      await api.loginAdmin(pinInput);
      setIsAdmin(true);
      setMessage({
        type: 'ok',
        text: 'Zalogowano jako Administrator - możesz edytować parametry.',
      });
    } catch (err) {
      setMessage({ type: 'err', text: err instanceof Error ? err.message : 'Błąd logowania administratora' });
    } finally {
      setBusy(false);
    }
  };

  const handleAdminLogout = async () => {
    setMessage(null);
    setBusy(true);
    try {
      await api.logoutAdmin();
      setIsAdmin(false);
      setMessage({ type: 'ok', text: 'Wylogowano z roli Administratora.' });
    } finally {
      setBusy(false);
    }
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!params || !isAdmin) return;
    setMessage(null);
    setBusy(true);
    try {
      const saved = await api.updateAdminParameters(params);
      setParams(saved);
      setMessage({
        type: 'ok',
        text: 'Zapisano nowe parametry w SQLite — kalkulator natychmiast korzysta ze zaktualizowanych stawek.',
      });
    } catch (err) {
      setMessage({ type: 'err', text: err instanceof Error ? err.message : 'Błąd zapisu parametrów' });
    } finally {
      setBusy(false);
    }
  };

  const handleReset = async () => {
    if (!isAdmin) return;
    setMessage(null);
    setBusy(true);
    try {
      const defaults = await api.resetAdminParameters();
      setParams(defaults);
      setMessage({ type: 'ok', text: 'Przywrócono domyślne parametry prawne na rok 2026.' });
    } catch (err) {
      setMessage({ type: 'err', text: err instanceof Error ? err.message : 'Błąd przywracania ustawień' });
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Monitor budżetu Azure AI Document Intelligence + Deklaracja AI */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {quota && (
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs">
            <h3 className="flex items-center gap-2 text-base font-bold text-slate-900">
              <Gauge className="h-5 w-5 text-emerald-700" />
              Ochrona budżetu Azure  
            </h3>
            <p className="mt-1 text-xs text-slate-600">
              Jedynie na potrzeby demonstracji - licznik stron chroniący przed 
			  niekontrolowanym zużyciem planu Azure Document Intelligence S0 (PAYG). Ponowne
              wgranie tych samych spraw obsługuje cache (SHA-256).
            </p>
            <div className="mt-4 grid grid-cols-3 gap-3">
              <div className="rounded-xl bg-slate-50 p-3">
                <div className="text-xs text-slate-500">Zużyto stron</div>
                <div className="text-xl font-bold text-slate-900">
                  {quota.pagesUsedAzure} / {quota.hardPageBudget}
                </div>
              </div>
              <div className="rounded-xl bg-slate-50 p-3">
                <div className="text-xs text-slate-500">Pozostało</div>
                <div className="text-xl font-bold text-emerald-700">{quota.remainingPages}</div>
              </div>
            </div>
          </div>
        )}

        {aiInfo && (
          <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs">
            <h3 className="flex items-center gap-2 text-base font-bold text-slate-900">
              <Cpu className="h-5 w-5 text-emerald-700" />
              Jawna deklaracja zakresu użycia Sztucznej Inteligencji (AI)
            </h3>
            <div className="mt-3 space-y-2.5 text-xs">
              {aiInfo.scope.map((item, idx) => (
                <div key={idx} className="rounded-xl bg-slate-50 p-3">
                  <div className="font-bold text-slate-900">{item.module}</div>
                  <div className="mt-0.5 font-medium text-emerald-800">{item.technology}</div>
                  <div className="mt-0.5 text-slate-600">{item.role}</div>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>

      {/* Edytor parametrów legislacyjnych */}
      {params && (
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs space-y-5">
          <div className="flex flex-wrap items-center justify-between gap-3 border-b border-slate-100 pb-4">
            <div>
              <h2 className="flex items-center gap-2 text-lg font-bold text-slate-900">
                {isAdmin ? (
                  <Unlock className="h-5 w-5 text-emerald-700" />
                ) : (
                  <Lock className="h-5 w-5 text-slate-500" />
                )}
                Panel Administracyjny — Parametry Legislacyjne ({params.year})
              </h2>
              <p className="text-xs text-slate-500">
                Zmiany zapisują się w SQLite i natychmiast wpływają na wszystkie obliczenia bez rekompilacji aplikacji.
              </p>
            </div>

            {isAdmin ? (
              <div className="flex items-center gap-2">
                <span className="rounded-lg bg-emerald-100 px-3 py-1.5 text-xs font-bold text-emerald-800">
                  Aktywna rola: Admin
                </span>
                <button
                  type="button"
                  disabled={busy}
                  onClick={handleAdminLogout}
                  className="inline-flex items-center gap-1 rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-xs font-semibold text-slate-700 hover:bg-slate-100 disabled:opacity-50"
                >
                  <LogOut className="h-3.5 w-3.5" />
                  Wyloguj admina
                </button>
              </div>
            ) : (
              <form onSubmit={handleAdminLogin} className="flex flex-wrap items-center gap-2">
                <input
                  type="password"
                  className="w-32"
                  value={pinInput}
                  onChange={(e) => setPinInput(e.target.value)}
                  placeholder="PIN (ZUS2026)"
                />
                <button
                  type="submit"
                  disabled={busy}
                  className="rounded-lg bg-slate-900 px-3.5 py-2 text-xs font-bold text-white hover:bg-slate-800 disabled:opacity-50"
                >
                  Odblokuj edycję (PIN demo: ZUS2026)
                </button>
              </form>
            )}
          </div>

          <form onSubmit={handleSave} className="space-y-5">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Płaca minimalna brutto (zł)
                </label>
                <input
                  type="number"
                  step="1"
                  disabled={!isAdmin}
                  className="w-full disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500"
                  value={params.minimalWageGross}
                  onChange={(e) => updateField('minimalWageGross', Number(e.target.value))}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Limit 250% dobrowolnego (zł)
                </label>
                <input
                  type="number"
                  step="10"
                  disabled={!isAdmin}
                  className="w-full disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500"
                  value={params.maxVoluntarySicknessBaseGross}
                  onChange={(e) => updateField('maxVoluntarySicknessBaseGross', Number(e.target.value))}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Świadczenie rodzicielskie (zł)
                </label>
                <input
                  type="number"
                  step="10"
                  disabled={!isAdmin}
                  className="w-full disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500"
                  value={params.parentalBenefitFloorNet}
                  onChange={(e) => updateField('parentalBenefitFloorNet', Number(e.target.value))}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Zasiłek pogrzebowy (zł)
                </label>
                <input
                  type="number"
                  step="100"
                  disabled={!isAdmin}
                  className="w-full disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500"
                  value={params.funeralAllowanceStillbirth}
                  onChange={(e) => updateField('funeralAllowanceStillbirth', Number(e.target.value))}
                />
              </div>

              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Stawka 81,5% (ułamek, np. 0.815)
                </label>
                <input
                  type="number"
                  step="0.005"
                  disabled={!isAdmin}
                  className="w-full disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500"
                  value={params.unifiedMaternityAndParentalRate}
                  onChange={(e) => updateField('unifiedMaternityAndParentalRate', Number(e.target.value))}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Stawka rodzicielskiego (ułamek)
                </label>
                <input
                  type="number"
                  step="0.01"
                  disabled={!isAdmin}
                  className="w-full disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500"
                  value={params.parentalRate}
                  onChange={(e) => updateField('parentalRate', Number(e.target.value))}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Urlop macierzyński (1 dziecko, tyg.)
                </label>
                <input
                  type="number"
                  disabled={!isAdmin}
                  className="w-full disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500"
                  value={params.maternityWeeksSingle}
                  onChange={(e) => updateField('maternityWeeksSingle', Number(e.target.value))}
                />
              </div>
              <div>
                <label className="mb-1 block text-xs font-semibold text-slate-700">
                  Urlop rodzicielski (1 dziecko, tyg.)
                </label>
                <input
                  type="number"
                  disabled={!isAdmin}
                  className="w-full disabled:cursor-not-allowed disabled:bg-slate-100 disabled:text-slate-500"
                  value={params.parentalWeeksSingle}
                  onChange={(e) => updateField('parentalWeeksSingle', Number(e.target.value))}
                />
              </div>
            </div>

            <div className="flex flex-wrap gap-3">
              <button
                type="submit"
                disabled={!isAdmin || busy}
                className="inline-flex items-center gap-2 rounded-xl bg-emerald-700 px-5 py-2.5 text-sm font-bold text-white hover:bg-emerald-800 disabled:cursor-not-allowed disabled:opacity-50"
              >
                <Save className="h-4 w-4" />
                Zapisz parametry w bazie
              </button>
              <button
                type="button"
                disabled={!isAdmin || busy}
                onClick={handleReset}
                className="inline-flex items-center gap-2 rounded-xl border border-slate-300 bg-white px-4 py-2.5 text-sm font-semibold text-slate-700 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50"
              >
                <RotateCcw className="h-4 w-4" />
                Przywróć domyślne (2026)
              </button>
            </div>

            {message && (
              <div
                className={`flex items-center gap-2 rounded-xl border p-3 text-sm ${
                  message.type === 'ok'
                    ? 'border-emerald-200 bg-emerald-50 text-emerald-900'
                    : 'border-rose-300 bg-rose-50 text-rose-800'
                }`}
              >
                {message.type === 'ok' ? (
                  <CheckCircle2 className="h-4 w-4 shrink-0" />
                ) : (
                  <AlertCircle className="h-4 w-4 shrink-0" />
                )}
                {message.text}
              </div>
            )}
          </form>
        </div>
      )}
    </div>
  );
}