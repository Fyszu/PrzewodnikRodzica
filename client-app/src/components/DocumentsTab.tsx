import { useEffect, useState } from 'react';
import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  Database,
  FileText,
  Play,
  Sparkles,
  Upload,
} from 'lucide-react';
import { api } from '../api/client';
import type {
  CalculationRequest,
  CalculationResult,
  DemoTestCaseItem,
  DocumentAnalysisResponse,
} from '../types/api';
import { ResultsPanel } from './ResultsPanel';

interface Props {
  onLoadIntoCalculator: (req: CalculationRequest) => void;
}

export function DocumentsTab({ onLoadIntoCalculator }: Props) {
  const [demoCases, setDemoCases] = useState<DemoTestCaseItem[]>([]);
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pdfResult, setPdfResult] = useState<DocumentAnalysisResponse | null>(null);
  const [activeDemoResult, setActiveDemoResult] = useState<{
    demoCase: DemoTestCaseItem;
    calculation: CalculationResult;
  } | null>(null);

  useEffect(() => {
    api.getDemoCases().then(setDemoCases).catch(() => {});
  }, []);

  const handleFileChange = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setUploading(true);
    setError(null);
    setActiveDemoResult(null);
    try {
      const res = await api.analyzePdf(file);
      setPdfResult(res);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Błąd analizy pliku PDF');
    } finally {
      setUploading(false);
      e.target.value = '';
    }
  };

  const handleRunDemo = async (caseId: string) => {
    setUploading(true);
    setError(null);
    setPdfResult(null);
    try {
      const res = await api.runDemoCase(caseId);
      setActiveDemoResult(res);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Błąd uruchomienia sprawy testowej');
    } finally {
      setUploading(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Sekcja Upload PDF */}
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs">
        <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
          <div>
            <div className="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-3 py-1 text-xs font-semibold text-emerald-800">
              <Sparkles className="h-3.5 w-3.5" />
              Moduł AI: Azure AI Document Intelligence (OCR + tabele + kratki wyboru) + deterministyczny silnik C#
            </div>
            <h2 className="mt-2 text-lg font-bold text-slate-900">
              Przetestuj sprawę z pliku PDF (Z-3, Z-3a, ZAM, ZAS-26, e-ZLA)
            </h2>
            <p className="mt-1 text-sm text-slate-600">
              Wgraj załączoną teczkę PDF — system automatycznie zweryfikuje tytuł ubezpieczenia, wyciągnie tabelę
              wynagrodzeń z Z-3 oraz wyjątki z sekcji „Uwagi” i przeliczy uprawnienia.
            </p>
          </div>

          <label className="inline-flex shrink-0 cursor-pointer items-center gap-2 rounded-xl bg-emerald-700 px-5 py-3 text-sm font-bold text-white shadow-xs transition hover:bg-emerald-800">
            <Upload className="h-4 w-4" />
            <span>{uploading ? 'Analizowanie PDF...' : 'Wybierz plik PDF z dysku'}</span>
            <input
              type="file"
              accept=".pdf,application/pdf"
              className="hidden"
              disabled={uploading}
              onChange={handleFileChange}
            />
          </label>
        </div>

        {error && (
          <div className="mt-4 flex items-center gap-2 rounded-xl border border-rose-300 bg-rose-50 p-3 text-sm text-rose-800">
            <AlertCircle className="h-4 w-4 shrink-0" />
            {error}
          </div>
        )}
      </div>

      {/* Gotowe sprawy demonstracyjne */}
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-xs">
        <h3 className="text-base font-bold text-slate-900">
          Lub uruchom gotową syntetyczną sprawę testową (1 kliknięcie)
        </h3>
        <p className="mt-1 text-xs text-slate-500">
          Sprawy odzwierciedlają kluczowe przypadki prawne z dokumentacji konkursowej (bez rozpowszechniania
          oryginalnych plików ZUS).
        </p>

        <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2">
          {demoCases.map((dc) => (
            <div
              key={dc.id}
              className="flex flex-col justify-between rounded-xl border border-slate-200 bg-slate-50/70 p-4"
            >
              <div>
                <span className="inline-block rounded-md bg-slate-800 px-2 py-0.5 text-[11px] font-semibold text-white">
                  {dc.category}
                </span>
                <h4 className="mt-2 font-bold text-slate-900">{dc.title}</h4>
                <p className="mt-1 text-xs text-slate-600">{dc.summary}</p>
                <p className="mt-2 text-xs font-medium text-emerald-800">
                  Oczekiwany rezultat: {dc.expectedOutcomeHint}
                </p>
              </div>
              <div className="mt-4 flex gap-2">
                <button
                  type="button"
                  onClick={() => handleRunDemo(dc.id)}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-emerald-700 px-3.5 py-2 text-xs font-bold text-white hover:bg-emerald-800"
                >
                  <Play className="h-3.5 w-3.5" />
                  Przelicz tutaj
                </button>
                <button
                  type="button"
                  onClick={() => onLoadIntoCalculator(dc.requestData)}
                  className="inline-flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3.5 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-100"
                >
                  Otwórz w kalkulatorze
                  <ArrowRight className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Wynik analizy wgranego PDF */}
      {pdfResult && (
        <div className="space-y-6">
          <div className="rounded-2xl border border-emerald-200 bg-white p-5 shadow-xs">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h3 className="flex items-center gap-2 text-base font-bold text-slate-900">
                  <FileText className="h-5 w-5 text-emerald-700" />
                  Raport ekstrakcji: {pdfResult.fileName} ({pdfResult.pageCount} str.)
                </h3>
                <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-slate-600">
                  <span className="rounded-full bg-slate-100 px-2.5 py-0.5 font-medium">
                    Silnik: {pdfResult.extractionEngine}
                  </span>
                  {pdfResult.servedFromCache && (
                    <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2.5 py-0.5 font-semibold text-emerald-800">
                      <Database className="h-3 w-3" /> Cache SHA-256 (0 zł)
                    </span>
                  )}
                </div>
              </div>

              <button
                type="button"
                onClick={() => onLoadIntoCalculator(pdfResult.parsedDocument.mappedRequest)}
                className="inline-flex items-center gap-1.5 rounded-xl bg-slate-900 px-4 py-2.5 text-xs font-bold text-white hover:bg-slate-800"
              >
                Edytuj wyekstrahowane dane w kalkulatorze
                <ArrowRight className="h-3.5 w-3.5" />
              </button>
            </div>

            {pdfResult.notice && (
              <div className="mt-3 rounded-lg border border-amber-200 bg-amber-50 p-3 text-xs text-amber-900">
                {pdfResult.notice}
              </div>
            )}

            <div className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2">
              <div className="rounded-xl bg-slate-50 p-4">
                <div className="text-xs font-bold uppercase text-slate-500">Weryfikacja ubezpieczenia</div>
                <div className="mt-1.5 flex items-start gap-2 text-sm font-medium text-slate-900">
                  {pdfResult.parsedDocument.hasConfirmedInsurance ? (
                    <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" />
                  ) : (
                    <AlertCircle className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
                  )}
                  {pdfResult.parsedDocument.insuranceSummary}
                </div>
                <div className="mt-3 text-xs font-bold uppercase text-slate-500">Wykryte formularze</div>
                <ul className="mt-1 list-disc space-y-0.5 pl-4 text-xs text-slate-700">
                  {pdfResult.parsedDocument.detectedForms.length > 0 ? (
                    pdfResult.parsedDocument.detectedForms.map((f) => <li key={f}>{f}</li>)
                  ) : (
                    <li>Nie rozpoznano standardowych nagłówków formularzy ZUS (skan może wymagać OCR Azure).</li>
                  )}
                </ul>
              </div>

              <div className="rounded-xl bg-slate-50 p-4">
                <div className="text-xs font-bold uppercase text-slate-500">Wyekstrahowane fakty (deterministycznie)</div>
                <ul className="mt-1.5 list-disc space-y-1 pl-4 text-xs text-slate-700">
                  {pdfResult.parsedDocument.extractedFacts.map((fact, idx) => (
                    <li key={idx}>{fact}</li>
                  ))}
                </ul>
              </div>
            </div>
          </div>

          <ResultsPanel result={pdfResult.calculation} />
        </div>
      )}

      {/* Wynik sprawy demonstracyjnej */}
      {activeDemoResult && (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3">
            <span className="text-sm font-bold text-emerald-950">
              Wynik symulacji: {activeDemoResult.demoCase.title}
            </span>
            <button
              type="button"
              onClick={() => onLoadIntoCalculator(activeDemoResult.demoCase.requestData)}
              className="inline-flex items-center gap-1 text-xs font-bold text-emerald-800 underline"
            >
              Modyfikuj parametry w kalkulatorze <ArrowRight className="h-3.5 w-3.5" />
            </button>
          </div>
          <ResultsPanel result={activeDemoResult.calculation} />
        </div>
      )}
    </div>
  );
}