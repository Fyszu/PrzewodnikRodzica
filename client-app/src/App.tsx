import { useEffect, useState } from 'react';
import {
  AlertTriangle,
  Baby,
  Calculator,
  FileText,
  HeartHandshake,
  LogIn,
  LogOut,
  Settings,
  ShieldCheck,
  Smartphone,
  UserCheck,
  X,
} from 'lucide-react';
import { api } from './api/client';
import { AdminTab } from './components/AdminTab';
import { CalculatorTab } from './components/CalculatorTab';
import { DocumentsTab } from './components/DocumentsTab';
import { SimulationsTab } from './components/SimulationsTab';
import {
  createDefaultRequest,
  type CalculationRequest,
  type PersonaListItem,
  type SimulatedZusAccount,
} from './types/api';

type ActiveTab = 'calculator' | 'documents' | 'simulations' | 'admin';

export default function App() {
  const [activeTab, setActiveTab] = useState<ActiveTab>('calculator');
  const [calcRequest, setCalcRequest] = useState<CalculationRequest>(createDefaultRequest);
  const [requestVersion, setRequestVersion] = useState(0);
  const [sensitiveMode, setSensitiveMode] = useState(false);

  // Globalny stan autoryzacji mObywatel (symulacja)
  const [personas, setPersonas] = useState<PersonaListItem[]>([]);
  const [loggedAccount, setLoggedAccount] = useState<SimulatedZusAccount | null>(null);
  const [loginModalOpen, setLoginModalOpen] = useState(false);
  const [loginBusy, setLoginBusy] = useState(false);

  useEffect(() => {
    api.getPersonas().then(setPersonas).catch(() => {});
    api.getCurrentSession()
      .then((session) => {
        if (session.authenticated && session.account) {
          setLoggedAccount(session.account);
        }
      })
      .catch(() => {});
  }, []);

  const loadIntoCalculator = (nextReq: CalculationRequest, switchTab = true) => {
    setCalcRequest(nextReq);
    setRequestVersion((v) => v + 1);
    if (switchTab) {
      setActiveTab('calculator');
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  };

  const handleLoginPersona = async (personaId: string, alsoLoadZusData = false) => {
    setLoginBusy(true);
    try {
      const account = await api.loginMObywatel(personaId);
      setLoggedAccount(account);
      setLoginModalOpen(false);
      if (alsoLoadZusData) {
        loadIntoCalculator(account.prefilledCalculatorData, true);
      }
    } finally {
      setLoginBusy(false);
    }
  };

  const handleLogout = async () => {
    await api.logoutMObywatel();
    setLoggedAccount(null);
  };

  const navItems: { id: ActiveTab; label: string; icon: React.ReactNode }[] = [
    { id: 'calculator', label: 'Kalkulator', icon: <Calculator className="h-4 w-4" /> },
    { id: 'documents', label: 'Analiza plików PDF', icon: <FileText className="h-4 w-4" /> },
    { id: 'simulations', label: 'mObywatel / PUE / Wnioski (symulacja)', icon: <ShieldCheck className="h-4 w-4" /> },
    { id: 'admin', label: 'Panel Admina', icon: <Settings className="h-4 w-4" /> },
  ];

  return (
    <div className="min-h-screen flex flex-col bg-slate-50">
      {/* Nagłówek z globalnym statusem mObywatel */}
      <header
        className={`border-b transition-colors ${
          sensitiveMode
            ? 'border-slate-300 bg-slate-800 text-slate-100'
            : 'border-emerald-800 bg-emerald-800 text-white'
        }`}
      >
        <div className="mx-auto max-w-7xl px-4 py-4 sm:px-6 space-y-3">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-3">
              <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-white/15">
                {sensitiveMode ? <HeartHandshake className="h-6 w-6" /> : <Baby className="h-6 w-6" />}
              </div>
              <div>
                <h1 className="text-lg font-extrabold tracking-tight sm:text-xl">
                  Zasiłkowy Przewodnik Przyszłego Rodzica
                </h1>
                <p className="text-xs opacity-85">
                  Stan prawny 2026 (KP, ustawa zasiłkowa, rozporządzenie z 27.09.2026 r.) • HackNation 2026
                </p>
              </div>
            </div>

            {/* Globalny pasek autoryzacji mObywatel */}
            <div className="flex flex-wrap items-center gap-2">
              {loggedAccount ? (
                <>
                  <div className="inline-flex items-center gap-2 rounded-xl bg-white/15 px-3 py-1.5 text-xs font-semibold">
                    <UserCheck className="h-4 w-4 text-emerald-300" />
                    <span>mObywatel: {loggedAccount.fullName}</span>
                  </div>
                  <button
                    type="button"
                    onClick={() => loadIntoCalculator(loggedAccount.prefilledCalculatorData, true)}
                    className="rounded-xl bg-white px-3 py-1.5 text-xs font-bold text-emerald-900 hover:bg-emerald-50"
                  >
                    Wczytaj dane ZUS
                  </button>
                  <button
                    type="button"
                    onClick={() => setLoginModalOpen(true)}
                    className="rounded-xl bg-white/10 px-2.5 py-1.5 text-xs font-semibold hover:bg-white/20"
                  >
                    Zmień profil
                  </button>
                  <button
                    type="button"
                    onClick={handleLogout}
                    title="Wyloguj do trybu Gościa"
                    className="inline-flex items-center gap-1 rounded-xl bg-rose-600/90 px-2.5 py-1.5 text-xs font-bold text-white hover:bg-rose-700"
                  >
                    <LogOut className="h-3.5 w-3.5" />
                    Wyloguj
                  </button>
                </>
              ) : (
                <button
                  type="button"
                  onClick={() => setLoginModalOpen(true)}
                  className="inline-flex items-center gap-2 rounded-xl bg-white px-3.5 py-2 text-xs font-bold text-slate-900 shadow-xs transition hover:bg-emerald-50"
                >
                  <LogIn className="h-4 w-4 text-rose-600" />
                  Zaloguj przez mObywatel (symulacja)
                </button>
              )}
            </div>
          </div>

          <nav className="flex flex-wrap gap-2 border-t border-white/15 pt-3">
            {navItems.map((tab) => {
              const active = activeTab === tab.id;
              return (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => setActiveTab(tab.id)}
                  className={`inline-flex items-center gap-2 rounded-xl px-3.5 py-2 text-xs font-bold transition sm:text-sm ${
                    active
                      ? 'bg-white text-slate-900 shadow-xs'
                      : 'bg-white/10 text-white hover:bg-white/20'
                  }`}
                >
                  {tab.icon}
                  {tab.label}
                </button>
              );
            })}
          </nav>
        </div>
      </header>

      {/* Główna treść */}
      <main className="mx-auto w-full max-w-7xl flex-1 px-4 py-6 sm:px-6">
        {activeTab === 'calculator' && (
          <CalculatorTab
            request={calcRequest}
            requestVersion={requestVersion}
            loggedAccount={loggedAccount}
            onChangeRequest={setCalcRequest}
            onLoadSavedRequest={(req) => loadIntoCalculator(req, false)}
            onOpenMObywatelModal={() => setLoginModalOpen(true)}
            onSensitiveModeChange={setSensitiveMode}
          />
        )}
        {activeTab === 'documents' && (
          <DocumentsTab onLoadIntoCalculator={(req) => loadIntoCalculator(req, true)} />
        )}
        {activeTab === 'simulations' && (
          <SimulationsTab
            personas={personas}
            loggedAccount={loggedAccount}
            onLoginPersona={(id) => handleLoginPersona(id, false)}
            onLogout={handleLogout}
            onLoadIntoCalculator={(req) => loadIntoCalculator(req, true)}
          />
        )}
        {activeTab === 'admin' && <AdminTab />}
      </main>

      {/* Modal logowania mObywatel */}
      {loginModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 p-4 backdrop-blur-xs">
          <div className="w-full max-w-2xl rounded-2xl bg-white p-6 shadow-xl">
            <div className="flex items-start justify-between gap-4">
              <div className="flex items-center gap-2.5">
                <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-rose-50 text-rose-600">
                  <Smartphone className="h-5 w-5" />
                </div>
                <div>
                  <h2 className="text-base font-bold text-slate-900">
                    Zaloguj się przez mObywatel (symulacja Węzła Krajowego)
                  </h2>
                  <p className="text-xs text-slate-500">
                    Wybierz profil testowy, aby pobrać dane z ZUS i odblokować zapisywanie oraz porównywanie kalkulacji.
                  </p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setLoginModalOpen(false)}
                className="rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-2">
              {personas.map((p) => (
                <div
                  key={p.personaId}
                  className="flex flex-col justify-between rounded-xl border border-slate-200 bg-slate-50/70 p-4"
                >
                  <div>
                    <div className="font-bold text-slate-900">{p.fullName}</div>
                    <div className="mt-1 text-[11px] font-semibold text-emerald-800">
                      Kod ZUS: {p.insuranceTitleCode}
                    </div>
                    <div className="mt-1 text-xs text-slate-600">{p.insuranceTitleDescription}</div>
                    {p.socialSecurityDebtPln > 0 && (
                      <div className="mt-2 inline-flex items-center gap-1 rounded bg-rose-100 px-2 py-0.5 text-[11px] font-bold text-rose-800">
                        <AlertTriangle className="h-3 w-3" /> Zaległość: {p.socialSecurityDebtPln.toFixed(2)} zł
                      </div>
                    )}
                  </div>
                  <div className="mt-3 flex gap-2">
                    <button
                      type="button"
                      disabled={loginBusy}
                      onClick={() => handleLoginPersona(p.personaId, true)}
                      className="flex-1 rounded-lg bg-emerald-700 px-3 py-2 text-xs font-bold text-white hover:bg-emerald-800 disabled:opacity-50"
                    >
                      Zaloguj i wczytaj ZUS
                    </button>
                    <button
                      type="button"
                      disabled={loginBusy}
                      onClick={() => handleLoginPersona(p.personaId, false)}
                      className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-100"
                    >
                      Tylko zaloguj
                    </button>
                  </div>
                </div>
              ))}
            </div>

            <div className="mt-4 flex justify-end border-t border-slate-100 pt-3">
              <button
                type="button"
                onClick={() => setLoginModalOpen(false)}
                className="text-xs font-semibold text-slate-600 hover:text-slate-900"
              >
                Kontynuuj jako Gość (bez logowania)
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Stopka */}
      <footer className="border-t border-slate-200 bg-white py-4 text-center text-xs text-slate-500">
        Zasiłkowy Przewodnik Przyszłego Rodzica • Backend: ASP.NET Core (.NET 8) REST API + Deterministyczny Silnik Reguł •
        OCR PDF: Azure AI Document Intelligence (prebuilt-layout) • Interfejs: React + TypeScript (RWD)
      </footer>
    </div>
  );
}