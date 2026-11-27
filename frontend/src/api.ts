import type {
  AuditDocumentDetailDto,
  AuditDocumentSummaryDto,
  AuthResponseDto,
  DashboardMetricsDto,
  PurchaseOrderDto,
  SamplePdfInfoDto,
} from './types';

const API_BASE = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.replace(/\/$/, '') || 'http://localhost:5080';

export function getApiBaseUrl(): string {
  return API_BASE;
}

async function request<T>(path: string, token: string | null, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }
  if (!(init?.body instanceof FormData) && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers,
  });

  if (!res.ok) {
    let errorMsg = `HTTP ${res.status}`;
    try {
      const errJson = await res.json();
      if (errJson?.message) errorMsg = errJson.message;
    } catch {
      // ignore json parse error
    }
    throw new Error(errorMsg);
  }

  return res.json() as Promise<T>;
}

export const api = {
  demoLogin: (role: 'Admin' | 'Analyst') =>
    request<AuthResponseDto>('/api/auth/demo-login', null, {
      method: 'POST',
      body: JSON.stringify({ role }),
    }),

  login: (email: string, password: string) =>
    request<AuthResponseDto>('/api/auth/login', null, {
      method: 'POST',
      body: JSON.stringify({ email, password }),
    }),

  getMetrics: (token: string) => request<DashboardMetricsDto>('/api/documents/metrics', token),

  getDocuments: (token: string) => request<AuditDocumentSummaryDto[]>('/api/documents', token),

  getDocumentDetail: (token: string, id: string) =>
    request<AuditDocumentDetailDto>(`/api/documents/${id}`, token),

  uploadPdfs: (token: string, files: File[]) => {
    const formData = new FormData();
    for (const file of files) {
      formData.append('files', file);
    }
    return request<{ message: string; documents: AuditDocumentSummaryDto[] }>(
      '/api/documents/upload',
      token,
      {
        method: 'POST',
        body: formData,
      }
    );
  },

  getSamples: (token: string) => request<SamplePdfInfoDto[]>('/api/documents/samples', token),

  enqueueSample: (token: string, fileName: string) =>
    request<AuditDocumentSummaryDto>(
      `/api/documents/samples/${encodeURIComponent(fileName)}/enqueue`,
      token,
      { method: 'POST' }
    ),

  reviewDocument: (token: string, id: string, approve: boolean, notes: string) =>
    request<AuditDocumentDetailDto>(`/api/documents/${id}/review`, token, {
      method: 'POST',
      body: JSON.stringify({ approve, notes }),
    }),

  getPurchaseOrders: (token: string) => request<PurchaseOrderDto[]>('/api/purchase-orders', token),

  downloadDocumentBlobUrl: async (token: string, id: string): Promise<string> => {
    const res = await fetch(`${API_BASE}/api/documents/${id}/file`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (!res.ok) throw new Error('No se pudo abrir el PDF.');
    const blob = await res.blob();
    return URL.createObjectURL(blob);
  },

  getSampleDownloadUrl: (fileName: string) =>
    `${API_BASE}/api/documents/samples/${encodeURIComponent(fileName)}/download`,
};
