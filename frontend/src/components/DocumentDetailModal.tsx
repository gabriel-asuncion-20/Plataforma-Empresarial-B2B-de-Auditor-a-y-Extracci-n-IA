import React, { useState } from 'react';
import {
  X,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  Info,
  ExternalLink,
  Code2,
  ShieldAlert,
  ShieldCheck,
  Cpu,
  Clock,
} from 'lucide-react';
import type { AuditDocumentDetailDto, UserDto } from '../types';

interface DocumentDetailModalProps {
  detail: AuditDocumentDetailDto;
  currentUser: UserDto;
  onClose: () => void;
  onReview: (id: string, approve: boolean, notes: string) => Promise<void>;
  onOpenPdf: (id: string) => Promise<void>;
  onSwitchToAdmin: () => Promise<void>;
}

const fmtUsd = (n?: number | null) =>
  n == null ? '—' : new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);

export const DocumentDetailModal: React.FC<DocumentDetailModalProps> = ({
  detail,
  currentUser,
  onClose,
  onReview,
  onOpenPdf,
  onSwitchToAdmin,
}) => {
  const [notes, setNotes] = useState(detail.reviewNotes ?? '');
  const [submitting, setSubmitting] = useState(false);
  const [reviewError, setReviewError] = useState<string | null>(null);
  const [showRawJson, setShowRawJson] = useState(false);

  const isAdmin = currentUser.role === 'Admin';
  const isProcessing = ['Queued', 'ExtractingText', 'AnalyzingWithAi', 'Reconciling'].includes(detail.status);

  const handleAction = async (approve: boolean) => {
    if (!approve && !notes.trim()) {
      setReviewError('Debes ingresar una justificación de auditoría para rechazar el documento.');
      return;
    }
    setReviewError(null);
    setSubmitting(true);
    try {
      await onReview(detail.id, approve, notes);
    } catch (err) {
      setReviewError(err instanceof Error ? err.message : 'Error al registrar dictamen.');
    } finally {
      setSubmitting(false);
    }
  };

  let prettyJson = detail.structuredExtractionJson ?? '';
  if (prettyJson) {
    try {
      prettyJson = JSON.stringify(JSON.parse(prettyJson), null, 2);
    } catch {
      // keep raw
    }
  }

  const po = detail.matchedPurchaseOrder;

  return (
    <div className="fixed inset-0 z-50 bg-black/75 backdrop-blur-xs flex items-center justify-center p-3 sm:p-6 overflow-y-auto">
      <div className="bg-slate-900 border border-slate-700 rounded-2xl w-full max-w-4xl max-h-[92vh] flex flex-col shadow-2xl overflow-hidden">
        {/* Header */}
        <div className="px-6 py-4 bg-slate-950 border-b border-slate-800 flex items-start justify-between gap-4">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <span className="text-xs font-semibold uppercase tracking-wider px-2.5 py-0.5 rounded bg-blue-500/20 text-blue-300">
                {detail.documentType === 'Contract' ? 'Contrato Legal B2B' : 'Factura Electrónica'}
              </span>
              <span className="text-xs px-2.5 py-0.5 rounded bg-slate-800 text-slate-300 font-mono">
                Estado: {detail.status}
              </span>
              <span
                className={`text-xs font-semibold px-2.5 py-0.5 rounded ${
                  detail.riskLevel === 'Critical' || detail.riskLevel === 'High'
                    ? 'bg-rose-500/20 text-rose-300'
                    : detail.riskLevel === 'Medium'
                    ? 'bg-amber-500/20 text-amber-300'
                    : 'bg-emerald-500/20 text-emerald-300'
                }`}
              >
                Riesgo {detail.riskLevel}
              </span>
            </div>
            <h2 className="text-lg font-bold text-white mt-1.5 break-all">{detail.originalFileName}</h2>
          </div>

          <div className="flex items-center gap-2 shrink-0">
            <button
              type="button"
              onClick={() => onOpenPdf(detail.id)}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-xs font-medium text-slate-200 transition cursor-pointer"
            >
              <ExternalLink className="w-3.5 h-3.5" />
              Abrir PDF
            </button>
            <button
              type="button"
              onClick={onClose}
              className="p-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-400 hover:text-white cursor-pointer"
            >
              <X className="w-5 h-5" />
            </button>
          </div>
        </div>

        {/* Body */}
        <div className="p-6 overflow-y-auto space-y-6 flex-1">
          {/* AI Summary + Telemetry */}
          <div className="bg-slate-950/90 border border-slate-800 rounded-xl p-4 space-y-3">
            <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-slate-400">
              <div className="flex items-center gap-4">
                <span className="inline-flex items-center gap-1.5 text-blue-300 font-medium">
                  <Cpu className="w-3.5 h-3.5" />
                  Modelo IA: {detail.aiModelUsed ?? 'Pendiente'}
                </span>
                {detail.processingTimeMs != null && (
                  <span className="inline-flex items-center gap-1.5">
                    <Clock className="w-3.5 h-3.5 text-slate-400" />
                    {detail.processingTimeMs} ms
                  </span>
                )}
                <span>{detail.pageCount || 1} pág(s)</span>
              </div>
              <span>Subido por: {detail.uploadedByName}</span>
            </div>

            <p className="text-sm text-slate-200 leading-relaxed">
              {detail.aiExecutiveSummary ?? 'Procesando análisis ejecutivo con Gemini IA...'}
            </p>
          </div>

          {/* Side-by-Side Comparison */}
          {detail.documentType === 'Invoice' && (
            <div className="grid md:grid-cols-2 gap-4">
              <div className="bg-slate-950/70 border border-slate-800 rounded-xl p-4 space-y-2 text-xs">
                <div className="text-[11px] font-semibold uppercase tracking-wider text-blue-400 mb-2">
                  Datos Extraídos del PDF (Structured Output IA)
                </div>
                <div className="flex justify-between py-1 border-b border-slate-800/70">
                  <span className="text-slate-400">Proveedor:</span>
                  <span className="text-white font-medium text-right">{detail.extractedSupplierName ?? '—'}</span>
                </div>
                <div className="flex justify-between py-1 border-b border-slate-800/70">
                  <span className="text-slate-400">RUC Extraído:</span>
                  <span className="font-mono text-white">{detail.extractedSupplierRuc ?? '—'}</span>
                </div>
                <div className="flex justify-between py-1 border-b border-slate-800/70">
                  <span className="text-slate-400">No. Factura / OC Ref:</span>
                  <span className="font-mono text-slate-200">
                    {detail.extractedInvoiceNumber ?? '—'} / {detail.extractedOrderNumber ?? 'Sin OC'}
                  </span>
                </div>
                <div className="flex justify-between py-1 border-b border-slate-800/70">
                  <span className="text-slate-400">Subtotal Facturado:</span>
                  <span className="font-mono text-white font-semibold">{fmtUsd(detail.extractedSubtotal)}</span>
                </div>
                <div className="flex justify-between py-1 border-b border-slate-800/70">
                  <span className="text-slate-400">IVA Declarado:</span>
                  <span className="font-mono text-white font-semibold">{fmtUsd(detail.extractedTaxAmount)}</span>
                </div>
                <div className="flex justify-between pt-1">
                  <span className="text-slate-300 font-semibold">Total Factura:</span>
                  <span className="font-mono text-sm text-blue-300 font-bold">{fmtUsd(detail.extractedTotalAmount)}</span>
                </div>
              </div>

              <div className="bg-slate-950/70 border border-slate-800 rounded-xl p-4 space-y-2 text-xs">
                <div className="text-[11px] font-semibold uppercase tracking-wider text-emerald-400 mb-2">
                  Orden de Compra Autorizada en ERP (Neon PostgreSQL)
                </div>
                {po ? (
                  <>
                    <div className="flex justify-between py-1 border-b border-slate-800/70">
                      <span className="text-slate-400">Proveedor Adjudicado:</span>
                      <span className="text-white font-medium text-right">{po.supplierName}</span>
                    </div>
                    <div className="flex justify-between py-1 border-b border-slate-800/70">
                      <span className="text-slate-400">RUC Oficial ERP:</span>
                      <span className="font-mono text-emerald-300">{po.supplierRuc}</span>
                    </div>
                    <div className="flex justify-between py-1 border-b border-slate-800/70">
                      <span className="text-slate-400">Orden de Compra:</span>
                      <span className="font-mono text-white font-semibold">{po.orderNumber}</span>
                    </div>
                    <div className="flex justify-between py-1 border-b border-slate-800/70">
                      <span className="text-slate-400">Subtotal Pactado:</span>
                      <span className="font-mono text-white font-semibold">{fmtUsd(po.subtotal)}</span>
                    </div>
                    <div className="flex justify-between py-1 border-b border-slate-800/70">
                      <span className="text-slate-400">IVA Legal (15%):</span>
                      <span className="font-mono text-white font-semibold">{fmtUsd(po.taxAmount)}</span>
                    </div>
                    <div className="flex justify-between pt-1">
                      <span className="text-slate-300 font-semibold">Total Autorizado:</span>
                      <span className="font-mono text-sm text-emerald-300 font-bold">{fmtUsd(po.totalAmount)}</span>
                    </div>
                  </>
                ) : (
                  <div className="h-36 flex flex-col items-center justify-center text-center text-rose-300 bg-rose-500/5 rounded-lg border border-rose-500/20 p-4">
                    <AlertTriangle className="w-6 h-6 mb-1.5 text-rose-400" />
                    <p className="font-medium">Sin Orden de Compra vinculada en base de datos</p>
                    <p className="text-[11px] text-slate-400 mt-1">
                      Referencia en PDF: {detail.extractedOrderNumber ?? 'Ninguna'}
                    </p>
                  </div>
                )}
              </div>
            </div>
          )}

          {/* Findings List */}
          <div>
            <h3 className="text-sm font-semibold text-white mb-3">
              Hallazgos de Auditoría y Reglas de Conciliación ({detail.findings.length})
            </h3>
            <div className="space-y-2.5">
              {detail.findings.map((f) => {
                const isCrit = f.severity === 'Critical';
                const isWarn = f.severity === 'Warning';
                return (
                  <div
                    key={f.id}
                    className={`rounded-xl p-4 border ${
                      isCrit
                        ? 'bg-rose-500/10 border-rose-500/30'
                        : isWarn
                        ? 'bg-amber-500/10 border-amber-500/30'
                        : 'bg-emerald-500/10 border-emerald-500/30'
                    }`}
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2 mb-1">
                      <div className="flex items-center gap-2">
                        {isCrit ? (
                          <XCircle className="w-4 h-4 text-rose-400 shrink-0" />
                        ) : isWarn ? (
                          <AlertTriangle className="w-4 h-4 text-amber-400 shrink-0" />
                        ) : (
                          <CheckCircle2 className="w-4 h-4 text-emerald-400 shrink-0" />
                        )}
                        <span className="text-sm font-semibold text-white">{f.title}</span>
                        <span className="text-[10px] font-mono px-2 py-0.5 rounded bg-slate-950/70 text-slate-300">
                          {f.code}
                        </span>
                      </div>
                      {f.financialImpactUsd != null && f.financialImpactUsd > 0 && (
                        <span className="text-xs font-mono font-bold px-2.5 py-0.5 rounded bg-rose-500/20 text-rose-200">
                          Impacto: {fmtUsd(f.financialImpactUsd)}
                        </span>
                      )}
                    </div>

                    <p className="text-xs text-slate-200 mt-1 leading-relaxed">{f.description}</p>

                    {(f.expectedValue || f.actualValue) && (
                      <div className="mt-2.5 grid sm:grid-cols-2 gap-2 text-[11px] bg-slate-950/60 rounded-lg p-2.5 font-mono">
                        <div>
                          <span className="text-slate-400">Esperado: </span>
                          <span className="text-emerald-300">{f.expectedValue ?? '—'}</span>
                        </div>
                        <div>
                          <span className="text-slate-400">Detectado en PDF: </span>
                          <span className={isCrit ? 'text-rose-300' : 'text-amber-200'}>
                            {f.actualValue ?? '—'}
                          </span>
                        </div>
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>

          {/* Raw JSON Toggle */}
          {prettyJson && (
            <div>
              <button
                type="button"
                onClick={() => setShowRawJson(!showRawJson)}
                className="inline-flex items-center gap-1.5 text-xs text-blue-400 hover:text-blue-300 cursor-pointer"
              >
                <Code2 className="w-4 h-4" />
                {showRawJson
                  ? 'Ocultar payload JSON tipado de Gemini'
                  : 'Ver payload JSON crudo (Structured Output)'}
              </button>
              {showRawJson && (
                <pre className="mt-2 p-4 rounded-xl bg-slate-950 border border-slate-800 text-[11px] text-emerald-300 font-mono overflow-x-auto max-h-64">
                  {prettyJson}
                </pre>
              )}
            </div>
          )}

          {/* Existing Review Info */}
          {detail.reviewedByName && (
            <div className="rounded-xl bg-blue-500/10 border border-blue-500/30 p-3.5 text-xs flex items-start gap-2.5">
              <Info className="w-4 h-4 text-blue-400 shrink-0 mt-0.5" />
              <div>
                <div className="font-semibold text-white">
                  Dictamen registrado por {detail.reviewedByName}
                </div>
                <div className="text-slate-300 mt-0.5">{detail.reviewNotes}</div>
              </div>
            </div>
          )}
        </div>

        {/* Footer RBAC Governance */}
        <div className="px-6 py-4 bg-slate-950 border-t border-slate-800">
          {isAdmin ? (
            <div className="space-y-3">
              <div className="flex items-center gap-2 text-xs text-blue-300 font-medium">
                <ShieldCheck className="w-4 h-4" />
                <span>Firma Ejecutiva Habilitada (Rol Admin) — Aprobar o Rechazar Auditoría</span>
              </div>
              {reviewError && <div className="text-xs text-rose-400">{reviewError}</div>}
              <div className="flex flex-col sm:flex-row gap-2.5">
                <input
                  type="text"
                  placeholder="Observación o motivo de auditoría (obligatorio para rechazar)..."
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  disabled={isProcessing || submitting}
                  className="flex-1 rounded-lg bg-slate-900 border border-slate-700 px-3 py-2 text-xs text-white focus:outline-none focus:border-blue-500"
                />
                <button
                  type="button"
                  disabled={isProcessing || submitting}
                  onClick={() => handleAction(true)}
                  className="px-4 py-2 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-xs font-semibold text-white transition cursor-pointer disabled:opacity-40"
                >
                  Aprobar Auditoría
                </button>
                <button
                  type="button"
                  disabled={isProcessing || submitting}
                  onClick={() => handleAction(false)}
                  className="px-4 py-2 rounded-lg bg-rose-600 hover:bg-rose-500 text-xs font-semibold text-white transition cursor-pointer disabled:opacity-40"
                >
                  Rechazar Documento
                </button>
              </div>
            </div>
          ) : (
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-amber-500/10 border border-amber-500/25 rounded-xl px-4 py-3">
              <div className="flex items-center gap-2.5 text-xs text-amber-200">
                <ShieldAlert className="w-4 h-4 text-amber-400 shrink-0" />
                <span>
                  <strong>Control RBAC Activo:</strong> Tu sesión actual es <code>Analyst</code>. Solo el rol{' '}
                  <code>Admin</code> puede emitir dictamen de aprobación o rechazo.
                </span>
              </div>
              <button
                type="button"
                onClick={onSwitchToAdmin}
                className="shrink-0 px-3 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-500 text-xs font-semibold text-white cursor-pointer"
              >
                Cambiar a Rol Admin
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
