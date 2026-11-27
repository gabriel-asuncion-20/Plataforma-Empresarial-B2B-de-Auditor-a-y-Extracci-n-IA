import React, { useState } from 'react';
import { ShieldCheck, UserCheck, Lock, Sparkles, FileSearch, ArrowRight } from 'lucide-react';

interface LoginScreenProps {
  onDemoLogin: (role: 'Admin' | 'Analyst') => Promise<void>;
  onCredentialsLogin: (email: string, password: string) => Promise<void>;
  loading: boolean;
  error: string | null;
}

export const LoginScreen: React.FC<LoginScreenProps> = ({
  onDemoLogin,
  onCredentialsLogin,
  loading,
  error,
}) => {
  const [email, setEmail] = useState('admin@auditai.ec');
  const [password, setPassword] = useState('Admin2026!');
  const [showManual, setShowManual] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await onCredentialsLogin(email, password);
  };

  return (
    <div className="min-h-screen bg-slate-950 flex flex-col justify-center items-center px-4 py-12 relative overflow-hidden">
      <div className="absolute inset-0 bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-blue-900/25 via-slate-950 to-slate-950 pointer-events-none" />

      <div className="w-full max-w-4xl z-10 grid md:grid-cols-5 gap-8 items-center">
        <div className="md:col-span-2 space-y-5">
          <div className="inline-flex items-center gap-2.5 px-3 py-1.5 rounded-full bg-blue-500/10 border border-blue-500/30 text-blue-300 text-xs font-medium">
            <Sparkles className="w-3.5 h-3.5" />
            <span>Clean Architecture · .NET 10 · Gemini IA · SignalR</span>
          </div>

          <div className="flex items-center gap-3">
            <div className="w-11 h-11 rounded-xl bg-blue-600 flex items-center justify-center shadow-lg shadow-blue-600/30">
              <FileSearch className="w-6 h-6 text-white" />
            </div>
            <div>
              <h1 className="text-2xl font-bold tracking-tight text-white">AuditAI Enterprise</h1>
              <p className="text-xs text-slate-400">Auditoría Financiera & Legal B2B</p>
            </div>
          </div>

          <p className="text-sm text-slate-300 leading-relaxed">
            Plataforma corporativa para extracción estructurada de PDFs en segundo plano, conciliación automática contra órdenes de compra ERP (RUC e IVA 15%) y detección de riesgo contractual en tiempo real.
          </p>

          <div className="space-y-2.5 pt-2 text-xs text-slate-400">
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-emerald-400" />
              <span>Ingesta asíncrona HTTP 202 Accepted con Channel&lt;T&gt;</span>
            </div>
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-blue-400" />
              <span>Salidas tipadas con Gemini Structured Outputs</span>
            </div>
            <div className="flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-amber-400" />
              <span>Gobernanza RBAC con JWT (Contralor Admin vs Analista)</span>
            </div>
          </div>
        </div>

        <div className="md:col-span-3 bg-slate-900/90 border border-slate-800 rounded-2xl p-6 sm:p-8 shadow-2xl">
          <h2 className="text-lg font-semibold text-white mb-1">Acceso Rápido de Demostración (1 Clic)</h2>
          <p className="text-xs text-slate-400 mb-5">
            Selecciona un rol para probar permisos RBAC, carga de facturas/contratos y notificaciones en vivo.
          </p>

          {error && (
            <div className="mb-4 rounded-xl bg-rose-500/10 border border-rose-500/30 px-4 py-3 text-xs text-rose-300">
              {error}
            </div>
          )}

          <div className="grid sm:grid-cols-2 gap-3.5 mb-6">
            <button
              type="button"
              disabled={loading}
              onClick={() => onDemoLogin('Admin')}
              className="group text-left p-4 rounded-xl bg-slate-800/80 hover:bg-blue-600/20 border border-slate-700 hover:border-blue-500 transition cursor-pointer disabled:opacity-50"
            >
              <div className="flex items-center justify-between mb-2.5">
                <div className="w-9 h-9 rounded-lg bg-blue-500/20 text-blue-400 flex items-center justify-center">
                  <ShieldCheck className="w-5 h-5" />
                </div>
                <span className="text-[11px] font-semibold uppercase tracking-wider px-2 py-0.5 rounded bg-blue-500/20 text-blue-300">
                  Rol Admin
                </span>
              </div>
              <div className="font-semibold text-sm text-white group-hover:text-blue-200 flex items-center gap-1.5">
                Valeria Mendoza
                <ArrowRight className="w-3.5 h-3.5 opacity-0 group-hover:opacity-100 transition" />
              </div>
              <p className="text-xs text-slate-400 mt-1">
                Contralora General · Puede auditar, aprobar con excepción y rechazar documentos.
              </p>
            </button>

            <button
              type="button"
              disabled={loading}
              onClick={() => onDemoLogin('Analyst')}
              className="group text-left p-4 rounded-xl bg-slate-800/80 hover:bg-emerald-600/15 border border-slate-700 hover:border-emerald-500/70 transition cursor-pointer disabled:opacity-50"
            >
              <div className="flex items-center justify-between mb-2.5">
                <div className="w-9 h-9 rounded-lg bg-emerald-500/20 text-emerald-400 flex items-center justify-center">
                  <UserCheck className="w-5 h-5" />
                </div>
                <span className="text-[11px] font-semibold uppercase tracking-wider px-2 py-0.5 rounded bg-emerald-500/20 text-emerald-300">
                  Rol Analyst
                </span>
              </div>
              <div className="font-semibold text-sm text-white group-hover:text-emerald-200 flex items-center gap-1.5">
                Carlos Viteri
                <ArrowRight className="w-3.5 h-3.5 opacity-0 group-hover:opacity-100 transition" />
              </div>
              <p className="text-xs text-slate-400 mt-1">
                Analista Cuentas por Pagar · Carga PDFs e inspecciona hallazgos (sin firma final).
              </p>
            </button>
          </div>

          <div className="border-t border-slate-800 pt-4">
            <button
              type="button"
              onClick={() => setShowManual(!showManual)}
              className="text-xs text-slate-400 hover:text-slate-200 flex items-center gap-1.5 cursor-pointer"
            >
              <Lock className="w-3.5 h-3.5" />
              <span>{showManual ? 'Ocultar acceso con credenciales JWT' : 'Iniciar sesión manual con correo y contraseña'}</span>
            </button>

            {showManual && (
              <form onSubmit={handleSubmit} className="mt-4 space-y-3">
                <div className="grid sm:grid-cols-2 gap-3">
                  <div>
                    <label className="block text-xs text-slate-400 mb-1">Correo Corporativo</label>
                    <input
                      type="email"
                      value={email}
                      onChange={(e) => setEmail(e.target.value)}
                      className="w-full rounded-lg bg-slate-950 border border-slate-700 px-3 py-2 text-sm text-white focus:outline-none focus:border-blue-500"
                      required
                    />
                  </div>
                  <div>
                    <label className="block text-xs text-slate-400 mb-1">Contraseña</label>
                    <input
                      type="password"
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      className="w-full rounded-lg bg-slate-950 border border-slate-700 px-3 py-2 text-sm text-white focus:outline-none focus:border-blue-500"
                      required
                    />
                  </div>
                </div>
                <button
                  type="submit"
                  disabled={loading}
                  className="w-full py-2.5 rounded-lg bg-blue-600 hover:bg-blue-500 text-sm font-medium text-white transition cursor-pointer disabled:opacity-50"
                >
                  {loading ? 'Autenticando...' : 'Autenticar con JWT'}
                </button>
              </form>
            )}
          </div>
        </div>
      </div>
    </div>
  );
};
