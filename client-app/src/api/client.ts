import type {
  AiDisclosureResponse,
  BenefitParameters,
  CalculationRequest,
  CalculationResult,
  DemoTestCaseItem,
  DocumentAnalysisResponse,
  PersonaListItem,
  QuotaStatus,
  SimulatedZusAccount,
  SubmittedApplicationRecord,
  SavedCalculationRecord,
} from '../types/api';

async function handleJson<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let msg = `Błąd HTTP ${res.status}`;
    try {
      const err = await res.json();
      if (err?.error) msg = err.error;
    } catch {
      // brak body JSON
    }
    throw new Error(msg);
  }
  return res.json() as Promise<T>;
}

export const api = {
  calculate: (request: CalculationRequest) =>
    fetch('/api/calculator/calculate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }).then((r) => handleJson<CalculationResult>(r)),

  getAiDisclosure: () =>
    fetch('/api/calculator/ai-disclosure').then((r) => handleJson<AiDisclosureResponse>(r)),

  analyzePdf: (file: File) => {
    const form = new FormData();
    form.append('file', file);
    return fetch('/api/documents/analyze', {
      method: 'POST',
      body: form,
    }).then((r) => handleJson<DocumentAnalysisResponse>(r));
  },

  getDemoCases: () =>
    fetch('/api/documents/demo-cases').then((r) => handleJson<DemoTestCaseItem[]>(r)),

  runDemoCase: (caseId: string) =>
    fetch(`/api/documents/demo-cases/${encodeURIComponent(caseId)}/run`, {
      method: 'POST',
    }).then((r) => handleJson<{ demoCase: DemoTestCaseItem; calculation: CalculationResult }>(r)),

  getPersonas: () =>
    fetch('/api/simulations/mobywatel/personas').then((r) => handleJson<PersonaListItem[]>(r)),

  loginMObywatel: (personaId: string) =>
    fetch('/api/simulations/mobywatel/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ personaId }),
    }).then((r) => handleJson<SimulatedZusAccount>(r)),

  askPue: (question: string) =>
    fetch('/api/simulations/pue/question', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question }),
    }).then((r) =>
      handleJson<{
        ticketId: string;
        answer: string;
        contactChannels: { hotline: string; email: string; eVisit: string };
      }>(r)
    ),

  submitApplication: (payload: {
    category: string;
    applicantName: string;
    childNameOrEventDate: string;
    recipient: string;
    details: string;
  }) =>
    fetch('/api/simulations/applications/submit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then((r) => handleJson<SubmittedApplicationRecord>(r)),

  getApplications: () =>
    fetch('/api/simulations/applications').then((r) => handleJson<SubmittedApplicationRecord[]>(r)),

  getAdminStatus: () =>
    fetch('/api/admin/status').then((r) => handleJson<{ isAdmin: boolean }>(r)),

  loginAdmin: (pin: string) =>
    fetch('/api/admin/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ pin }),
    }).then((r) => handleJson<{ isAdmin: boolean }>(r)),

  logoutAdmin: async () => {
    await fetch('/api/admin/logout', { method: 'POST' });
  },

  getAdminParameters: () =>
    fetch('/api/admin/parameters').then((r) => handleJson<BenefitParameters>(r)),

  updateAdminParameters: (params: BenefitParameters) =>
    fetch('/api/admin/parameters', {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(params),
    }).then((r) => handleJson<BenefitParameters>(r)),

  resetAdminParameters: () =>
    fetch('/api/admin/parameters/reset', {
      method: 'POST',
    }).then((r) => handleJson<BenefitParameters>(r)),

  getQuota: () => fetch('/api/admin/quota').then((r) => handleJson<QuotaStatus>(r)),
  
  logoutMObywatel: async () => {
    await fetch('/api/simulations/mobywatel/logout', { method: 'POST' });
  },

  getCurrentSession: () =>
    fetch('/api/simulations/mobywatel/me').then((r) =>
      handleJson<{ authenticated: boolean; account: SimulatedZusAccount | null }>(r)
    ),

  getSavedCalculations: () =>
    fetch('/api/calculator/saved').then((r) => handleJson<SavedCalculationRecord[]>(r)),

  saveCalculation: (payload: { name: string; request: CalculationRequest }) =>
    fetch('/api/calculator/saved', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then((r) => handleJson<SavedCalculationRecord>(r)),

  deleteSavedCalculation: async (id: string) => {
    const res = await fetch(`/api/calculator/saved/${encodeURIComponent(id)}`, {
      method: 'DELETE',
    });
    if (!res.ok) throw new Error(`Błąd usuwania (${res.status})`);
  },
};