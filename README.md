# AuditAI — Plataforma B2B de Auditoría Documental Inteligente

Plataforma empresarial para la **extracción estructurada, conciliación financiera automatizada y análisis de riesgo contractual** sobre documentos PDF en segundo plano y en tiempo real.

## Descripción General

**AuditAI** automatiza la revisión contable y legal de lotes de facturas y contratos de proveedores. El sistema recibe documentos PDF de forma asíncrona (`202 Accepted`), extrae información tipada mediante IA generativa con salidas estructuradas (*Structured Outputs*), cruza automáticamente montos, RUC, ítems e impuestos (IVA 15%) contra las órdenes de compra registradas en base de datos y notifica hallazgos o discrepancias en vivo a través de un panel ejecutivo con control de acceso por roles.

## Arquitectura y Stack Tecnológico

* **Backend:** C# / .NET Web API bajo **Clean Architecture** (`Domain`, `Application`, `Infrastructure`, `Api`) y patrón **CQRS**.
* **Extracción e IA Corporativa:** Lectura nativa de PDFs (`PdfPig`) + integración desacoplada con LLMs (`Microsoft.Extensions.AI` / Gemini API) usando salidas fuertemente tipadas en C#.
* **Procesamiento Asíncrono y Tiempo Real:** Colas en segundo plano (`Channel<T>` + `BackgroundService`) y notificaciones en vivo vía **SignalR**.
* **Persistencia y Seguridad:** **Entity Framework Core** sobre **PostgreSQL Serverless (Neon)**, autenticación **JWT** y autorización basada en roles (**RBAC**: `Admin` / `Analyst`).
* **Frontend:** Dashboard B2B en **React**, **TypeScript**, **Vite** y **Tailwind CSS**.
* **Calidad y Despliegue:** Pruebas automatizadas con **xUnit** y configuración de contenedores (`Dockerfile` y `docker-compose.yml`).

## Módulos Principales

1. **Ingesta Asíncrona de Documentos:** Carga drag-and-drop de facturas y contratos PDF sin bloquear peticiones HTTP.
2. **Motor de Conciliación Financiera:** Validación cruzada contra órdenes de compra para detectar sobreprecios, diferencias en cálculo de IVA o inconsistencias de proveedor.
3. **Auditoría de Cláusulas de Riesgo:** Identificación automática de penalidades abusivas, renovaciones forzosas o vacíos de cumplimiento en contratos.
4. **Monitoreo y Aprobación por Rol:** Seguimiento del estado de procesamiento en tiempo real y flujo de aprobación/rechazo auditado.
