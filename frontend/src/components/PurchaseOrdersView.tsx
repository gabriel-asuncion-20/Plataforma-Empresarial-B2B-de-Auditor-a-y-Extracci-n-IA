import React from 'react';
import { Building2, CheckCircle2, FileSpreadsheet } from 'lucide-react';
import type { PurchaseOrderDto } from '../types';

interface PurchaseOrdersViewProps {
  orders: PurchaseOrderDto[];
}

const fmtUsd = (n: number) =>
  new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);

export const PurchaseOrdersView: React.FC<PurchaseOrdersViewProps> = ({ orders }) => {
  return (
    <div className="space-y-4">
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold text-white flex items-center gap-2">
            <FileSpreadsheet className="w-5 h-5 text-blue-400" />
            Órdenes de Compra Registradas en Neon PostgreSQL (ERP Maestro)
          </h2>
          <p className="text-xs text-slate-400 mt-0.5">
            El motor híbrido de conciliación cruza automáticamente cada factura PDF contra estas órdenes aprobadas (RUC, ítems, subtotal e IVA 15%).
          </p>
        </div>
        <span className="inline-flex items-center gap-1.5 text-xs px-3 py-1.5 rounded-full bg-emerald-500/10 border border-emerald-500/30 text-emerald-300 font-medium">
          <CheckCircle2 className="w-3.5 h-3.5" />
          {orders.length} Órdenes Activas
        </span>
      </div>

      <div className="grid lg:grid-cols-2 gap-4">
        {orders.map((po) => (
          <div
            key={po.id}
            className="bg-slate-900 border border-slate-800 rounded-2xl p-5 space-y-4 hover:border-slate-700 transition"
          >
            <div className="flex items-start justify-between gap-3">
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-sm font-bold text-blue-400 font-mono">{po.orderNumber}</span>
                  <span className="text-[11px] px-2 py-0.5 rounded bg-emerald-500/15 text-emerald-300 font-medium">
                    {po.status}
                  </span>
                </div>
                <h3 className="text-sm font-semibold text-white mt-1 flex items-center gap-1.5">
                  <Building2 className="w-4 h-4 text-slate-400" />
                  {po.supplierName}
                </h3>
                <p className="text-xs text-slate-400 font-mono mt-0.5">RUC Autorizado: {po.supplierRuc}</p>
              </div>

              <div className="text-right">
                <div className="text-xs text-slate-400">Total Aprobado</div>
                <div className="text-lg font-bold text-white">{fmtUsd(po.totalAmount)}</div>
                <div className="text-[11px] text-slate-400">
                  Sub: {fmtUsd(po.subtotal)} + IVA 15%: {fmtUsd(po.taxAmount)}
                </div>
              </div>
            </div>

            <div className="border-t border-slate-800 pt-3">
              <div className="text-[11px] uppercase tracking-wider text-slate-500 font-semibold mb-2">
                Líneas de Detalle Pactadas
              </div>
              <div className="space-y-1.5">
                {po.items.map((item) => (
                  <div
                    key={item.id}
                    className="flex items-center justify-between text-xs bg-slate-950/70 rounded-lg px-3 py-2 border border-slate-800/80"
                  >
                    <div className="truncate pr-3">
                      <span className="font-mono text-blue-300 mr-2">{item.sku}</span>
                      <span className="text-slate-300">{item.description}</span>
                    </div>
                    <div className="text-right shrink-0 font-mono text-slate-200">
                      {item.quantity} x {fmtUsd(item.unitPrice)} = <span className="font-semibold">{fmtUsd(item.lineTotal)}</span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
};
