# AuditAI — Plataforma Empresarial B2B de Auditoría y Extracción Inteligente de Documentos

Plataforma corporativa full-stack construida en **C# / .NET 10** bajo **Clean Architecture + CQRS**, diseñada para automatizar la **extracción estructurada de facturas y contratos PDF**, la **conciliación financiera/tributaria contra Órdenes de Compra (RUC 13 dígitos e IVA 15% Ecuador)** y la **detección de cláusulas legales de riesgo** en segundo plano con notificaciones en tiempo real vía **SignalR**.

---

## Arquitectura del Sistema

```mermaid
flowchart LR
    subgraph Client["Frontend B2B (React 19 + TS + Tailwind)"]
        UI["Dashboard Ejecutivo & Drag-and-Drop PDF"]
        SRClient["Cliente WebSocket SignalR"]
    end

    subgraph API["Capa Presentación (AuditAI.Api)"]
        Controllers["REST Controllers (JWT / RBAC)"]
        Hub["AuditHub (SignalR)"]
    end

    subgraph AppLayer["Capa Aplicación (AuditAI.Application)"]
        CQRS["CQRS Dispatcher (Commands & Queries)"]
        Engine["Motor Híbrido de Conciliación & Riesgo"]
    end

    subgraph Infra["Capa Infraestructura (AuditAI.Infrastructure)"]
        Channel["Channel<Guid> + AuditBackgroundWorker"]
        PdfPig["Extractor Nativo PdfPig + Generador QuestPDF"]
        Gemini["Adaptador IChatClient Gemini IA (IPv4 + Fallback)"]
        EFCore["EF Core 10 + Npgsql"]
    end

    subgraph Cloud["Servicios Cloud"]
        Neon[("Neon Serverless PostgreSQL\n(Esquema audit_ai)")]
        GeminiCloud["Google Gemini API\n(Structured Outputs JSON)"]
    end

    UI -- "POST /api/documents/upload (202 Accepted)" --> Controllers
    Controllers --> CQRS
    CQRS --> Channel
    Channel --> PdfPig
    Channel --> Gemini
    Gemini -- "gemini-3.5-flash-lite / 3.8-flash" --> GeminiCloud
    Channel --> Engine
    CQRS --> EFCore
    Channel --> EFCore
    EFCore --> Neon
    Channel --> Hub
    Hub -- "Eventos AuditUpdated en vivo" --> SRClient
```

---

## Flujo Asíncrono de Procesamiento y Tiempo Real

```mermaid
sequenceDiagram
    actor Usuario as Analista / Contralor (Admin)
    participant Web as React B2B Dashboard
    participant API as AuditAI.Api (.NET 10)
    participant Queue as Channel<Guid> + Worker
    participant AI as Gemini API (Structured Outputs)
    participant DB as Neon PostgreSQL

    Usuario->>Web: Arrastra lote de Facturas / Contratos PDF
    Web->>API: POST /api/documents/upload (Bearer JWT)
    API->>DB: Registra AuditDocument (Status = Queued)
    API->>Queue: EnqueueAsync(documentId)
    API-->>Web: 202 Accepted (sin bloquear hilo HTTP)
    Queue-->>Web: SignalR: "Extrayendo texto con PdfPig..."
    Queue->>AI: Inferencia tipada JSON (responseSchema estricto)
    AI-->>Queue: Payload tipado (RUC, OC, Ítems, IVA, Cláusulas)
    Queue-->>Web: SignalR: "Cruzando contra Orden de Compra ERP..."
    Queue->>DB: Consulta PurchaseOrder + Guarda AuditFindings
    Queue-->>Web: SignalR: "Conciliado Auto / Alerta de Discrepancias"
    opt Gobernanza RBAC (Solo Rol Admin)
        Usuario->>API: POST /api/documents/{id}/review (Aprobar / Rechazar)
        API->>DB: Registra firma de auditoría y justificación
        API-->>Web: SignalR: Estado actualizado en tiempo real
    end
```

---

## Características Principales de Ingeniería

* **Clean Architecture + CQRS Puro:** Separación estricta en 4 proyectos (`AuditAI.Domain`, `AuditAI.Application`, `AuditAI.Infrastructure`, `AuditAI.Api`) más suite de pruebas automatizadas `AuditAI.Tests`.
* **Ingesta No Bloqueante (`202 Accepted`):** Cola asíncrona en memoria de alto rendimiento basada en `System.Threading.Channels` y `BackgroundService`.
* **IA Corporativa Tipada (`Microsoft.Extensions.AI`):** Implementación desacoplada de `IChatClient` e `IAiDocumentAnalyzer` contra Google Gemini (`gemini-3.5-flash-lite` con fallback automático a `gemini-3.8-flash`), forzando **Structured Outputs** JSON y priorizando **IPv4** mediante `SocketsHttpHandler.ConnectCallback`.
* **Motor Determinista de Conciliación B2B (Ecuador):**
  * Validación estructural de **RUC ecuatoriano de 13 dígitos** (código provincial `01`–`24`/`30` y establecimiento activo).
  * Verificación aritmética de líneas de detalle, base imponible y **cálculo tributario exacto de IVA 15%**.
  * Cruce *2-Way Match* contra Órdenes de Compra aprobadas en base de datos para detectar **sobreprecios por ítem**, **proveedores no adjudicados** u **órdenes de compra fantasma**.
  * Auditoría legal de contratos identificando **renovaciones automáticas forzosas**, **multas unilaterales** y **exoneraciones abusivas de responsabilidad**.
* **Seguridad JWT + Control de Roles (RBAC):**
  * `Analyst`: Puede cargar lotes PDF, ejecutar escenarios de prueba, monitorear la cola SignalR e inspeccionar hallazgos.
  * `Admin`: Posee todos los permisos anteriores más la facultad exclusiva (`[Authorize(Roles = "Admin")]`) de **aprobar con excepción** o **rechazar formalmente** una auditoría.
* **Laboratorio Integrado de PDFs Reales (`/sample-pdfs`):** Al arrancar, `QuestPDF` genera automáticamente 5 documentos PDF corporativos reales listos para arrastrar o encolar con 1 clic desde la interfaz.

---

## Estructura de la Solución

```text
├── src/
│   ├── AuditAI.Domain/           # Entidades ricas, Enums y reglas fiscales (EcuadorTaxRules)
│   ├── AuditAI.Application/      # CQRS Dispatcher, Comandos/Queries, DTOs y ReconciliationEngine
│   ├── AuditAI.Infrastructure/   # EF Core + Neon Npgsql, PdfPig, QuestPDF, Gemini IChatClient, Worker
│   └── AuditAI.Api/              # Controladores REST, AuditHub (SignalR), JWT/RBAC y Swagger OpenAPI
├── tests/
│   └── AuditAI.Tests/            # Pruebas unitarias y de integración con xUnit
├── frontend/                     # SPA ejecutiva en React 19 + TypeScript + Vite + Tailwind CSS v4
├── sample-pdfs/                  # 5 facturas y contratos PDF reales autogenerados para pruebas
├── Dockerfile                    # Imagen multi-etapa de producción para el backend .NET 10
└── docker-compose.yml            # Orquestación declarativa API + Frontend Nginx
```

---

## Escenarios de Prueba Incluidos en `/sample-pdfs`

| Archivo PDF | Escenario Simulado | Resultado Esperado del Motor + IA |
| :--- | :--- | :--- |
| `01_factura_conforme_OC-2026-001.pdf` | Factura de *TecnoAndina S.A.* ($12,650.00) cuadrada al centavo con `OC-2026-001`. | `ApprovedAuto` · Riesgo `Low` · Variación `$0.00` |
| `02_factura_sobreprecio_OC-2026-002.pdf` | *CloudLatam* factura 50 licencias a `$58/u` en lugar de `$48/u` pactados en `OC-2026-002`. | `FlaggedDiscrepancy` · `OVERPRICE_DETECTED` (`+$500.00`) |
| `03_factura_error_iva_OC-2026-003.pdf` | *Logística Pacífico* aplica 20% de IVA (`$760`) sobre `$3,800` en vez del 15% legal (`$570`). | `FlaggedDiscrepancy` · `IVA_CALCULATION_ERROR` (`+$190.00`) |
| `04_factura_ruc_invalido_oc_inexistente.pdf` | Proveedor con RUC inválido (`9912345670000`) y referencia a `OC-2026-999` inexistente. | `FlaggedDiscrepancy` · Riesgo `Critical` · Bloqueo total |
| `05_contrato_servicios_clausulas_riesgo.pdf` | Contrato cloud con multa unilateral de `$25,000`, renovación forzosa a 36 meses y tope de responsabilidad de `$100`. | `FlaggedDiscrepancy` · Riesgo `Critical` · Cláusulas legales marcadas |

---

## Requisitos y Ejecución Local

### Prerrequisitos
* **.NET SDK 10.0+**
* **Node.js 20+ / 26+** y `npm`
* Base de datos **Neon Serverless PostgreSQL** y clave de **Google Gemini API**

### 1. Configurar Credenciales Locales
Crea el archivo `src/AuditAI.Api/appsettings.Development.json` (ignorado en Git) tomando como guía `src/AuditAI.Api/appsettings.json` o `.env.example`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "postgresql://usuario:password@tu-endpoint.neon.tech/neondb?sslmode=require"
  },
  "Gemini": {
    "ApiKey": "TU_GEMINI_API_KEY",
    "PrimaryModel": "gemini-3.5-flash-lite",
    "FallbackModel": "gemini-3.8-flash",
    "PreferIpv4": true
  },
  "Jwt": {
    "Issuer": "AuditAI.Enterprise.Api",
    "Audience": "AuditAI.Enterprise.Dashboard",
    "SecretKey": "UNA_CLAVE_SECRETA_SEGURA_DE_MINIMO_32_CARACTERES"
  }
}
```

### 2. Iniciar el Backend (.NET 10 Web API)
Al arrancar, la API aplica automáticamente las migraciones de EF Core en el esquema `audit_ai` de Neon, siembra los usuarios/órdenes de compra y genera los PDFs en `/sample-pdfs`:

```powershell
dotnet run --project src/AuditAI.Api/AuditAI.Api.csproj --urls http://localhost:5080
```
* **Healthcheck:** `http://localhost:5080/health`
* **Swagger UI:** `http://localhost:5080/swagger`

### 3. Iniciar el Dashboard B2B (React + Vite)
En una segunda terminal:

```powershell
cd frontend
npm install
npm run dev
```
Abre `http://localhost:5173` e ingresa con **1 clic** como **Contralora (`Admin`)** o **Analista (`Analyst`)**.

### 4. Ejecutar Suite de Pruebas Automatizadas (`xUnit`)
```powershell
dotnet test AuditAI.sln
```

---

## Credenciales Semilla de Demostración

| Rol | Correo | Contraseña | Permisos |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@auditai.ec` | `Admin2026!` | Carga PDF, monitoreo en vivo, inspección y firma de Aprobación / Rechazo |
| **Analyst** | `analista@auditai.ec` | `Analyst2026!` | Carga PDF, monitoreo en vivo e inspección técnica (sin firma final) |
