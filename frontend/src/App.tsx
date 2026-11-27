import { useCallback, useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import {
  FileSearch,
  UploadCloud,
  CheckCircle2,
  AlertTriangle,
  Clock,
  DollarSign,
  Radio,
  LogOut,
  RefreshCw,
  Play,
  Layers,
  FileText,
  Shield,
  Eye,
  Download,
} from 'lucide-react';
import { api, getApiBaseUrl } from './api';
import type {
  AuditDocumentDetailDto,
  AuditDocumentSummaryDto,
  AuthResponseDto,
  DashboardMetricsDto,
  PurchaseOrderDto,
  RealtimeAuditUpdateDto,
  SamplePdfInfoDto,
} from './types';
import { LoginScreen } from './components/LoginScreen';
import { DocumentDetailModal } from './components/DocumentDetailModal';
import { PurchaseOrdersView } from './components/PurchaseOrdersView';

const STORAGE_KEY = 'auditai_auth_session';

const fmtUsd = (n?: number | null) =>
  n == null ? '—' : new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);

const statusLabels: Record<string, { text: string; cls: string; pulse?: boolean }> = {
  Queued: { text: 'En Cola (202)', cls: 'bg-slate-700 text-slate-200', pulse: true },
  ExtractingText: { text: 'Extrayendo PDF', cls: 'bg-sky-500/20 text-sky-300 border border-sky-500/30', pulse: true },
  AnalyzingWithAi: { text: 'Inferencia Gemini IA', cls: 'bg-indigo-500/20 text-indigo-300 border border-indigo-500/30', pulse: true },
  Reconciling: { text: 'Conciliando OC', cls: 'bg-amber-500/20 text-amber-300 border border-amber-500/30', pulse: true },
  ApprovedAuto: { text: 'Conciliado Auto', cls: 'bg-emerald-500/15 text-emerald-300 border border-emerald-500/30' },
  ManuallyApproved: { text: 'Aprobado Admin', cls: 'bg-teal-500/20 text-teal-200 border border-teal-500/30' },
  FlaggedDiscrepancy: { text: 'Con Discrepancias', cls: 'bg-rose-500/15 text-rose-300 border border-rose-500/30' },
  Rejected: { text: 'Rechazado', cls: 'bg-red-900/50 text-red-200 border border-red-700/50' },
  Failed: { text: 'Error Pipeline', cls: 'bg-rose-900/50 text-rose-200' },
};

export function App() {
  const [session, setSession] = useState<AuthResponseDto | null>(() => {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as AuthResponseDto) : null;
    } catch {
      return null;
    }
  });

  const [authLoading, setAuthLoading] = useState(false);
  const [authError, setAuthError] = useState<string | null>(null);

  const [activeTab, setActiveTab] = useState<'audit' | 'orders'>('audit');
  const [statusFilter, setStatusFilter] = useState<string>('ALL');

  const [metrics, setMetrics] = useState<DashboardMetricsDto | null>(null);
  const [documents, setDocuments] = useState<AuditDocumentSummaryDto[]>([]);
  const [samples, setSamples] = useState<SamplePdfInfoDto[]>([]);
  const [purchaseOrders, setPurchaseOrders] = useState<PurchaseOrderDto[]>([]);

  const [selectedDetail, setSelectedDetail] = useState<AuditDocumentDetailDto | null>(null);
  const [hubConnected, setHubConnected] = useState(false);
  const [liveBanner, setLiveBanner] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [dragActive, setDragActive] = useState(false);

  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const selectedIdRef = useRef<string | null>(null);
  selectedIdRef.current = selectedDetail?.id ?? null;

  const saveSession = (auth: AuthResponseDto | null) => {
    setSession(auth);
    if (auth) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(auth));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  };

  const handleDemoLogin = async (role: 'Admin' | 'Analyst') => {
    setAuthLoading(true);
    setAuthError(null);
    try {
      const auth = await api.demoLogin(role);
      saveSession(auth);
    } catch (err) {
      setAuthError(err instanceof Error ? err.message : 'Error conectando con la API.');
    } finally {
      setAuthLoading(false);
    }
  };

  const handleCredentialsLogin = async (email: string, password: string) => {
    setAuthLoading(true);
    setAuthError(null);
    try {
      const auth = await api.login(email, password);
      saveSession(auth);
    } catch (err) {
      setAuthError(err instanceof Error ? err.message : 'Credenciales inválidas.');
    } finally {
      setAuthLoading(false);
    }
  };

  const refreshDashboard = useCallback(async (token: string) => {
    const [m, docs, smps, pos] = await Promise.all([
      api.getMetrics(token),
      api.getDocuments(token),
      api.getSamples(token),
      api.getPurchaseOrders(token),
    ]);
    setMetrics(m);
    setDocuments(docs);
    setSamples(smps);
    setPurchaseOrders(pos);
  }, []);

  useEffect(() => {
    if (!session?.accessToken) return;
    const token = session.accessToken;

    refreshDashboard(token).catch((err) => {
      if (String(err.message).includes('401')) {
        saveSession(null);
      }
    });

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${getApiBaseUrl()}/hubs/audit`, {
        accessTokenFactory: () => token,
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('HubConnected', () => setHubConnected(true));

    connection.on('AuditUpdated', (update: RealtimeAuditUpdateDto) => {
      setLiveBanner(`${update.fileName}: ${update.stageMessage}`);
      setDocuments((prev) => {
        const exists = prev.some((d) => d.id === update.document.id);
        if (exists) {
          return prev.map((d) => (d.id === update.document.id ? update.document : d));
        }
        return [update.document, ...prev];
      });

      api.getMetrics(token).then(setMetrics).catch(() => {});

      if (selectedIdRef.current === update.documentId) {
        api.getDocumentDetail(token, update.documentId).then(setSelectedDetail).catch(() => {});
      }
    });

    connection.onreconnecting(() => setHubConnected(false));
    connection.onreconnected(() => setHubConnected(true));
    connection.onclose(() => setHubConnected(false));

    connection
      .start()
      .then(() => setHubConnected(true))
      .catch(() => setHubConnected(false));

    return () => {
      connection.stop().catch(() => {});
    };
  }, [session?.accessToken, refreshDashboard]);

  const handleUploadFiles = async (fileList: FileList | File[]) => {
    if (!session) return;
    const pdfs = Array.from(fileList).filter((f) => f.name.toLowerCase().endsWith('.pdf'));
    if (pdfs.length === 0) return;

    setUploading(true);
    try {
      const res = await api.uploadPdfs(session.accessToken, pdfs);
      setLiveBanner(res.message);
      await refreshDashboard(session.accessToken);
    } catch (err) {
      setLiveBanner(err instanceof Error ? err.message : 'Error al subir archivo PDF.');
    } finally {
      setUploading(false);
    }
  };

  const handleEnqueueSample = async (fileName: string) => {
    if (!session) return;
    setUploading(true);
    try {
      await api.enqueueSample(session.accessToken, fileName);
      setLiveBanner(`Escenario '${fileName}' encolado (202 Accepted).`);
    } catch (err) {
      setLiveBanner(err instanceof Error ? err.message : 'Error al encolar PDF de prueba.');
    } finally {
      setUploading(false);
    }
  };

  const handleEnqueueAllSamples = async () => {
    if (!session || samples.length === 0) return;
    setUploading(true);
    try {
      for (const s of samples) {
        await api.enqueueSample(session.accessToken, s.fileName);
      }
      setLiveBanner(`Lote completo de ${samples.length} PDFs enviado a la cola asíncrona.`);
    } finally {
      setUploading(false);
    }
  };

  const openDetail = async (id: string) => {
    if (!session) return;
    const detail = await api.getDocumentDetail(session.accessToken, id);
    setSelectedDetail(detail);
  };

  const handleReview = async (id: string, approve: boolean, notes: string) => {
    if (!session) return;
    const updated = await api.reviewDocument(session.accessToken, id, approve, notes);
    setSelectedDetail(updated);
    await refreshDashboard(session.accessToken);
  };

  const handleOpenPdf = async (id: string) => {
    if (!session) return;
    const url = await api.downloadDocumentBlobUrl(session.accessToken, id);
    window.open(url, '_blank', 'noopener,noreferrer');
  };

  if (!session) {
    return (
      <LoginScreen
        onDemoLogin={handleDemoLogin}
        onCredentialsLogin={handleCredentialsLogin}
        loading={authLoading}
        error={authError}
      />
    );
  }

  const filteredDocs = documents.filter((d) => {
    if (statusFilter === 'IN_PROGRESS') {
      return ['Queued', 'ExtractingText', 'AnalyzingWithAi', 'Reconciling'].includes(d.status);
    }
    if (statusFilter === 'FLAGGED') return d.status === 'FlaggedDiscrepancy';
    if (statusFilter === 'APPROVED') return d.status === 'ApprovedAuto' || d.status === 'ManuallyApproved';
    if (statusFilter === 'REJECTED') return d.status === 'Rejected';
    return true;
  });

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col">
      {/* Top Bar */}
      <header className="sticky top-0 z-30 bg-slate-950/90 backdrop-blur-md border-b border-slate-800">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 h-16 flex items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-xl bg-blue-600 flex items-center justify-center shadow-md shadow-blue-600/20">
              <FileSearch className="w-5 h-5 text-white" />
            </div>
            <div>
              <div className="font-bold text-sm sm:text-base text-white leading-tight">
                AuditAI Enterprise
              </div>
              <div className="text-[11px] text-slate-400 hidden sm:block">
                Conciliación Financiera & Auditoría Legal IA (.NET 10 + Neon + SignalR)
              </div>
            </div>
          </div>

          <div className="flex items-center gap-2.5">
            <div
              className={`hidden md:inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs border ${
                hubConnected
                  ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300'
                  : 'bg-amber-500/10 border-amber-500/30 text-amber-300'
              }`}
            >
              <Radio className={`w-3.5 h-3.5 ${hubConnected ? 'animate-pulse' : ''}`} />
              <span>{hubConnected ? 'SignalR en Vivo' : 'Reconectando Hub...'}</span>
            </div>

            <button
              type="button"
              onClick={() =>
                handleDemoLogin(session.user.role === 'Admin' ? 'Analyst' : 'Admin')
              }
              title="Cambiar rol instantáneamente para probar permisos RBAC"
              className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-semibold border transition cursor-pointer ${
                session.user.role === 'Admin'
                  ? 'bg-blue-500/15 border-blue-500/40 text-blue-200 hover:bg-blue-500/25'
                  : 'bg-emerald-500/15 border-emerald-500/40 text-emerald-200 hover:bg-emerald-500/25'
              }`}
            >
              <Shield className="w-3.5 h-3.5" />
              <span>Rol: {session.user.role}</span>
              <span className="text-[10px] opacity-75 hidden sm:inline">(Cambiar)</span>
            </button>

            <button
              type="button"
              onClick={() => refreshDashboard(session.accessToken)}
              className="p-2 rounded-lg bg-slate-900 border border-slate-800 hover:border-slate-700 text-slate-300 cursor-pointer"
              title="Actualizar métricas"
            >
              <RefreshCw className="w-4 h-4" />
            </button>

            <button
              type="button"
              onClick={() => saveSession(null)}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-900 border border-slate-800 hover:border-rose-500/50 text-xs text-slate-300 hover:text-rose-300 cursor-pointer"
            >
              <LogOut className="w-3.5 h-3.5" />
              <span className="hidden sm:inline">Salir</span>
            </button>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-7xl w-full mx-auto px-4 sm:px-6 py-6 space-y-6 flex-1">
        {/* Sub-navigation + Live Event Feed */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
          <div className="inline-flex bg-slate-900 p-1 rounded-xl border border-slate-800 self-start">
            <button
              type="button"
              onClick={() => setActiveTab('audit')}
              className={`px-4 py-1.5 rounded-lg text-xs font-semibold transition cursor-pointer ${
                activeTab === 'audit'
                  ? 'bg-blue-600 text-white shadow'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              Cola y Auditoría IA ({documents.length})
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('orders')}
              className={`px-4 py-1.5 rounded-lg text-xs font-semibold transition cursor-pointer ${
                activeTab === 'orders'
                  ? 'bg-blue-600 text-white shadow'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              Órdenes de Compra ERP ({purchaseOrders.length})
            </button>
          </div>

          {liveBanner && (
            <div className="flex items-center gap-2 px-3.5 py-2 rounded-xl bg-blue-500/10 border border-blue-500/30 text-xs text-blue-200">
              <span className="w-2 h-2 rounded-full bg-blue-400 animate-ping shrink-0" />
              <span className="truncate">{liveBanner}</span>
            </div>
          )}
        </div>

        {/* Executive KPI Cards */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4">
            <div className="flex items-center justify-between text-xs text-slate-400">
              <span>Documentos Auditados</span>
              <FileText className="w-4 h-4 text-blue-400" />
            </div>
            <div className="text-2xl font-bold text-white mt-2">{metrics?.totalDocuments ?? 0}</div>
            <div className="text-[11px] text-slate-400 mt-1">
              {metrics?.inProgressCount ? (
                <span className="text-sky-400 font-medium">
                  {metrics.inProgressCount} procesándose en cola...
                </span>
              ) : (
                <span>Latencia media IA: {metrics?.averageProcessingTimeMs ?? 0} ms</span>
              )}
            </div>
          </div>

          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4">
            <div className="flex items-center justify-between text-xs text-slate-400">
              <span>Conciliados / Aprobados</span>
              <CheckCircle2 className="w-4 h-4 text-emerald-400" />
            </div>
            <div className="text-2xl font-bold text-emerald-400 mt-2">
              {metrics?.approvedCount ?? 0}
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              Validados con Orden de Compra e IVA 15%
            </div>
          </div>

          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4">
            <div className="flex items-center justify-between text-xs text-slate-400">
              <span>Alertas y Discrepancias</span>
              <AlertTriangle className="w-4 h-4 text-rose-400" />
            </div>
            <div className="text-2xl font-bold text-rose-400 mt-2">
              {metrics?.flaggedCount ?? 0}
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              {metrics?.highRiskCount ?? 0} de riesgo Alto/Crítico · {metrics?.rejectedCount ?? 0} rechazados
            </div>
          </div>

          <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4">
            <div className="flex items-center justify-between text-xs text-slate-400">
              <span>Exposición / Sobreprecios</span>
              <DollarSign className="w-4 h-4 text-amber-400" />
            </div>
            <div className="text-2xl font-bold text-amber-300 mt-2">
              {fmtUsd(metrics?.totalDetectedVarianceUsd ?? 0)}
            </div>
            <div className="text-[11px] text-slate-400 mt-1">
              Volumen auditado: {fmtUsd(metrics?.totalAuditedAmountUsd ?? 0)}
            </div>
          </div>
        </div>

        {activeTab === 'orders' ? (
          <PurchaseOrdersView orders={purchaseOrders} />
        ) : (
          <>
            {/* Upload + Sample Scenarios Grid */}
            <div className="grid lg:grid-cols-12 gap-5">
              {/* Drag and Drop Uploader */}
              <div
                onDragOver={(e) => {
                  e.preventDefault();
                  setDragActive(true);
                }}
                onDragLeave={() => setDragActive(false)}
                onDrop={(e) => {
                  e.preventDefault();
                  setDragActive(false);
                  if (e.dataTransfer.files?.length) {
                    handleUploadFiles(e.dataTransfer.files);
                  }
                }}
                onClick={() => fileInputRef.current?.click()}
                className={`lg:col-span-5 rounded-2xl border-2 border-dashed p-6 flex flex-col items-center justify-center text-center transition cursor-pointer ${
                  dragActive
                    ? 'border-blue-400 bg-blue-500/10'
                    : 'border-slate-700 bg-slate-900/70 hover:border-blue-500/60 hover:bg-slate-900'
                }`}
              >
                <input
                  ref={fileInputRef}
                  type="file"
                  accept="application/pdf,.pdf"
                  multiple
                  className="hidden"
                  onChange={(e) => {
                    if (e.target.files?.length) {
                      handleUploadFiles(e.target.files);
                      e.target.value = '';
                    }
                  }}
                />
                <div className="w-12 h-12 rounded-2xl bg-blue-600/20 text-blue-400 flex items-center justify-center mb-3">
                  <UploadCloud className="w-6 h-6" />
                </div>
                <h3 className="text-sm font-semibold text-white">
                  Arrastra tus Facturas o Contratos PDF aquí
                </h3>
                <p className="text-xs text-slate-400 mt-1 max-w-xs">
                  Soporta carga por lotes. El backend responde <code className="text-blue-300">202 Accepted</code> al instante y procesa con PdfPig + Gemini en segundo plano.
                </p>
                <span className="mt-4 inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-xs font-semibold text-white">
                  {uploading ? 'Encolando...' : 'Seleccionar Archivos PDF'}
                </span>
              </div>

              {/* Sample Scenarios */}
              <div className="lg:col-span-7 bg-slate-900 border border-slate-800 rounded-2xl p-5 flex flex-col justify-between">
                <div className="flex flex-wrap items-center justify-between gap-2 mb-3">
                  <div>
                    <h3 className="text-sm font-semibold text-white">
                      Laboratorio de Escenarios Reales (/sample-pdfs)
                    </h3>
                    <p className="text-xs text-slate-400">
                      5 PDFs generados con QuestPDF listos para auditar en vivo con Gemini IA o descargar.
                    </p>
                  </div>
                  <button
                    type="button"
                    disabled={uploading || samples.length === 0}
                    onClick={handleEnqueueAllSamples}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-xs font-semibold text-white transition cursor-pointer disabled:opacity-50"
                  >
                    <Layers className="w-3.5 h-3.5" />
                    Auditar Lote Completo (5 PDFs)
                  </button>
                </div>

                <div className="space-y-2 max-h-60 overflow-y-auto pr-1">
                  {samples.map((s) => (
                    <div
                      key={s.fileName}
                      className="flex items-center justify-between gap-3 bg-slate-950/80 border border-slate-800/90 rounded-xl px-3.5 py-2.5"
                    >
                      <div className="min-w-0">
                        <div className="text-xs font-semibold text-white truncate">{s.title}</div>
                        <div className="text-[11px] text-slate-400 truncate">{s.scenarioDescription}</div>
                      </div>
                      <div className="flex items-center gap-1.5 shrink-0">
                        <a
                          href={api.getSampleDownloadUrl(s.fileName)}
                          target="_blank"
                          rel="noreferrer"
                          onClick={(e) => e.stopPropagation()}
                          title="Descargar PDF real"
                          className="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300"
                        >
                          <Download className="w-3.5 h-3.5" />
                        </a>
                        <button
                          type="button"
                          disabled={uploading}
                          onClick={() => handleEnqueueSample(s.fileName)}
                          className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg bg-blue-600/20 hover:bg-blue-600 text-blue-300 hover:text-white border border-blue-500/30 text-xs font-medium transition cursor-pointer disabled:opacity-50"
                        >
                          <Play className="w-3 h-3" />
                          Auditar
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>

            {/* Audit Queue & Results Table */}
            <div className="bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden">
              <div className="px-5 py-4 border-b border-slate-800 flex flex-wrap items-center justify-between gap-3">
                <h3 className="text-sm font-semibold text-white flex items-center gap-2">
                  <Clock className="w-4 h-4 text-blue-400" />
                  Cola de Auditoría en Tiempo Real e Historial de Conciliación
                </h3>

                <div className="flex flex-wrap gap-1.5">
                  {[
                    { id: 'ALL', label: 'Todos' },
                    { id: 'IN_PROGRESS', label: 'En Proceso' },
                    { id: 'FLAGGED', label: 'Con Discrepancias' },
                    { id: 'APPROVED', label: 'Aprobados' },
                    { id: 'REJECTED', label: 'Rechazados' },
                  ].map((f) => (
                    <button
                      key={f.id}
                      type="button"
                      onClick={() => setStatusFilter(f.id)}
                      className={`px-2.5 py-1 rounded-lg text-xs font-medium transition cursor-pointer ${
                        statusFilter === f.id
                          ? 'bg-blue-600 text-white'
                          : 'bg-slate-950 text-slate-400 hover:text-white border border-slate-800'
                      }`}
                    >
                      {f.label}
                    </button>
                  ))}
                </div>
              </div>

              {filteredDocs.length === 0 ? (
                <div className="p-12 text-center text-sm text-slate-400">
                  No hay documentos en esta vista todavía. Haz clic en <strong>"Auditar Lote Completo (5 PDFs)"</strong> o arrastra cualquier archivo PDF para ver la extracción con Gemini IA y SignalR en tiempo real.
                </div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full text-left border-collapse">
                    <thead>
                      <tr className="bg-slate-950/70 text-[11px] uppercase tracking-wider text-slate-400 border-b border-slate-800">
                        <th className="py-3 px-4">Documento / Tipo</th>
                        <th className="py-3 px-4">Proveedor / OC</th>
                        <th className="py-3 px-4">Estado Tiempo Real</th>
                        <th className="py-3 px-4">Riesgo</th>
                        <th className="py-3 px-4 text-right">Total PDF</th>
                        <th className="py-3 px-4 text-right">Discrepancia</th>
                        <th className="py-3 px-4 text-right">Acción</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-slate-800/80 text-xs">
                      {filteredDocs.map((doc) => {
                        const st = statusLabels[doc.status] ?? {
                          text: doc.status,
                          cls: 'bg-slate-800 text-slate-300',
                        };
                        return (
                          <tr
                            key={doc.id}
                            onClick={() => openDetail(doc.id)}
                            className="hover:bg-slate-800/40 transition cursor-pointer"
                          >
                            <td className="py-3.5 px-4">
                              <div className="font-semibold text-white truncate max-w-[230px]">
                                {doc.originalFileName}
                              </div>
                              <div className="text-[11px] text-slate-400 mt-0.5">
                                {doc.documentType === 'Contract' ? 'Contrato Legal' : 'Factura B2B'}
                                {doc.processingTimeMs != null ? ` · ${doc.processingTimeMs} ms` : ''}
                              </div>
                            </td>

                            <td className="py-3.5 px-4">
                              <div className="text-slate-200 truncate max-w-[200px]">
                                {doc.extractedSupplierName ?? 'Extrayendo...'}
                              </div>
                              <div className="text-[11px] font-mono text-slate-400">
                                {doc.extractedOrderNumber
                                  ? `Ref: ${doc.extractedOrderNumber}`
                                  : doc.extractedSupplierRuc ?? '—'}
                              </div>
                            </td>

                            <td className="py-3.5 px-4">
                              <span
                                className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-medium ${st.cls}`}
                              >
                                {st.pulse && (
                                  <span className="w-1.5 h-1.5 rounded-full bg-current animate-ping" />
                                )}
                                {st.text}
                              </span>
                            </td>

                            <td className="py-3.5 px-4">
                              <span
                                className={`font-semibold ${
                                  doc.riskLevel === 'Critical' || doc.riskLevel === 'High'
                                    ? 'text-rose-400'
                                    : doc.riskLevel === 'Medium'
                                    ? 'text-amber-300'
                                    : 'text-emerald-400'
                                }`}
                              >
                                {doc.riskLevel}
                              </span>
                              {doc.findingsCount > 0 && (
                                <div className="text-[11px] text-slate-400">
                                  {doc.findingsCount} hallazgo(s)
                                </div>
                              )}
                            </td>

                            <td className="py-3.5 px-4 text-right font-mono text-slate-200">
                              {fmtUsd(doc.extractedTotalAmount)}
                            </td>

                            <td className="py-3.5 px-4 text-right font-mono">
                              {doc.detectedVarianceAmount && doc.detectedVarianceAmount > 0 ? (
                                <span className="text-rose-400 font-semibold">
                                  +{fmtUsd(doc.detectedVarianceAmount)}
                                </span>
                              ) : (
                                <span className="text-emerald-400">$0.00</span>
                              )}
                            </td>

                            <td className="py-3.5 px-4 text-right">
                              <button
                                type="button"
                                onClick={(e) => {
                                  e.stopPropagation();
                                  openDetail(doc.id);
                                }}
                                className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg bg-slate-800 hover:bg-blue-600 text-slate-200 hover:text-white transition cursor-pointer"
                              >
                                <Eye className="w-3.5 h-3.5" />
                                Detalle
                              </button>
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          </>
        )}
      </main>

      {selectedDetail && (
        <DocumentDetailModal
          detail={selectedDetail}
          currentUser={session.user}
          onClose={() => setSelectedDetail(null)}
          onReview={handleReview}
          onOpenPdf={handleOpenPdf}
          onSwitchToAdmin={() => handleDemoLogin('Admin')}
        />
      )}
    </div>
  );
}
