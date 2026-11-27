using AuditAI.Application.Common.Interfaces;
using AuditAI.Application.DTOs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AuditAI.Infrastructure.Pdf;

public sealed class QuestPdfSampleGenerator : ISamplePdfGenerator
{
    private readonly string _samplesDir;

    private static readonly SampleSpec[] Specs =
    [
        new(
            "01_factura_conforme_OC-2026-001.pdf",
            "Factura Cuadrada (OC-2026-001)",
            "Factura electrónica de TecnoAndina S.A. con RUC válido, ítems exactos e IVA 15% correcto.",
            "Aprobación automática (Riesgo Bajo, Variación $0.00)"),
        new(
            "02_factura_sobreprecio_OC-2026-002.pdf",
            "Factura con Sobreprecio +$500 (OC-2026-002)",
            "CloudLatam factura licencias a $58/u en lugar de los $48/u pactados en la OC-2026-002.",
            "Alerta Crítica por Sobreprecio ($500.00 USD sobre subtotal)"),
        new(
            "03_factura_error_iva_OC-2026-003.pdf",
            "Factura con Error de IVA 20% (OC-2026-003)",
            "Logística Pacífico respeta el subtotal ($3,800) pero aplica $760 de IVA en vez de $570 (15%).",
            "Alerta Crítica Tributaria por cobro indebido de IVA (+$190.00 USD)"),
        new(
            "04_factura_ruc_invalido_oc_inexistente.pdf",
            "Factura con RUC Inválido y OC Fantasma",
            "Proveedor con RUC estructuralmente inválido (9912345670000) y referencia a OC-2026-999 no autorizada.",
            "Bloqueo Crítico por Identidad Fiscal y Orden de Compra Inexistente"),
        new(
            "05_contrato_servicios_clausulas_riesgo.pdf",
            "Contrato B2B con Cláusulas de Alto Riesgo",
            "Contrato tecnológico con renovación forzosa a 36 meses, multa unilateral de $25,000 y exoneración por fuga de datos.",
            "Alerta Legal de Riesgo Crítico (3 cláusulas abusivas detectadas)")
    ];

    public QuestPdfSampleGenerator(string contentRootPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var repoRoot = Path.GetFullPath(Path.Combine(contentRootPath, "..", ".."));
        if (!Directory.Exists(Path.Combine(repoRoot, "src")))
        {
            repoRoot = contentRootPath;
        }

        _samplesDir = Path.Combine(repoRoot, "sample-pdfs");
        Directory.CreateDirectory(_samplesDir);
    }

    public string SamplesDirectory => _samplesDir;

    public Task<IReadOnlyList<SamplePdfInfoDto>> EnsureSamplePdfsGeneratedAsync(CancellationToken ct = default)
    {
        GenerateInvoicePdf(
            Path.Combine(_samplesDir, Specs[0].FileName),
            invoiceNumber: "001-002-000045821",
            orderNumber: "OC-2026-001",
            supplierName: "TecnoAndina Soluciones S.A.",
            supplierRuc: "0992847561001",
            supplierAddress: "Av. Francisco de Orellana, Edif. World Trade Center, Guayaquil - Ecuador",
            issueDate: "2026-11-25",
            items:
            [
                ("SRV-DELL-R650 - Servidor Rack Empresarial Dell PowerEdge R650 Dual Xeon", 2, 4200.00m, 8400.00m),
                ("SW-FORTI-200F - Appliance Firewall Corporativo FortiGate 200F + UTM 1 Año", 2, 1300.00m, 2600.00m)
            ],
            subtotal: 11000.00m,
            taxLabel: "IVA 15%",
            taxRatePercent: 15m,
            taxAmount: 1650.00m,
            totalAmount: 12650.00m);

        GenerateInvoicePdf(
            Path.Combine(_samplesDir, Specs[1].FileName),
            invoiceNumber: "002-001-000019304",
            orderNumber: "OC-2026-002",
            supplierName: "CloudLatam Infraestructura Cia. Ltda.",
            supplierRuc: "1792458312001",
            supplierAddress: "Av. República del Salvador N36-84, Quito - Ecuador",
            issueDate: "2026-11-26",
            items:
            [
                ("LIC-M365-E5 - Suscripción Corporativa Seguridad Cloud E5 (Asientos Anuales)", 50, 58.00m, 2900.00m),
                ("SEC-SOC-MON - Servicio Mensual Monitoreo SOC 24/7 y Respuesta a Incidentes", 1, 1600.00m, 1600.00m)
            ],
            subtotal: 4500.00m,
            taxLabel: "IVA 15%",
            taxRatePercent: 15m,
            taxAmount: 675.00m,
            totalAmount: 5175.00m);

        GenerateInvoicePdf(
            Path.Combine(_samplesDir, Specs[2].FileName),
            invoiceNumber: "001-004-000088120",
            orderNumber: "OC-2026-003",
            supplierName: "Logística Pacífico Express S.A.",
            supplierRuc: "0993124578001",
            supplierAddress: "Km 14.5 Vía Daule, Parque Industrial Pascuales, Guayaquil - Ecuador",
            issueDate: "2026-11-26",
            items:
            [
                ("LOG-GYE-UIO - Flete Corporativo Refrigerado Ruta Guayaquil - Quito", 4, 850.00m, 3400.00m),
                ("SEG-CARGA-B2B - Póliza de Seguro Integral de Mercadería en Tránsito", 1, 400.00m, 400.00m)
            ],
            subtotal: 3800.00m,
            taxLabel: "IVA 20% (Tarifa Aplicada)",
            taxRatePercent: 20m,
            taxAmount: 760.00m,
            totalAmount: 4560.00m);

        GenerateInvoicePdf(
            Path.Combine(_samplesDir, Specs[3].FileName),
            invoiceNumber: "003-001-000000991",
            orderNumber: "OC-2026-999",
            supplierName: "Servicios Comerciales Litoral Express S.A.",
            supplierRuc: "9912345670000",
            supplierAddress: "Zona Comercial Norte, Lote 42, Ecuador",
            issueDate: "2026-11-27",
            items:
            [
                ("CONS-EXT-99 - Consultoría Técnica de Integración No Catalogada", 1, 6200.00m, 6200.00m)
            ],
            subtotal: 6200.00m,
            taxLabel: "IVA 15%",
            taxRatePercent: 15m,
            taxAmount: 930.00m,
            totalAmount: 7130.00m);

        GenerateContractPdf(Path.Combine(_samplesDir, Specs[4].FileName));

        return Task.FromResult(GetAvailableSamplePdfs());
    }

    public IReadOnlyList<SamplePdfInfoDto> GetAvailableSamplePdfs()
    {
        return Specs.Select(s =>
        {
            var fullPath = Path.Combine(_samplesDir, s.FileName);
            var size = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0L;
            return new SamplePdfInfoDto(s.FileName, s.Title, s.ScenarioDescription, s.ExpectedOutcome, size);
        }).ToList();
    }

    private static void GenerateInvoicePdf(
        string outputPath,
        string invoiceNumber,
        string orderNumber,
        string supplierName,
        string supplierRuc,
        string supplierAddress,
        string issueDate,
        IReadOnlyList<(string Description, int Qty, decimal UnitPrice, decimal LineTotal)> items,
        decimal subtotal,
        string taxLabel,
        decimal taxRatePercent,
        decimal taxAmount,
        decimal totalAmount)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(supplierName).Bold().FontSize(15).FontColor(Colors.Blue.Darken3);
                            left.Item().Text($"RUC: {supplierRuc}").SemiBold();
                            left.Item().Text($"Dirección Matriz: {supplierAddress}").FontSize(9);
                            left.Item().Text("Obligado a llevar contabilidad: SÍ").FontSize(9);
                        });

                        row.ConstantItem(210).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(right =>
                        {
                            right.Item().Text("FACTURA ELECTRÓNICA SRI").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                            right.Item().Text($"No. Factura: {invoiceNumber}").SemiBold();
                            right.Item().Text($"Orden de Compra: {orderNumber}").Bold();
                            right.Item().Text($"Fecha Emisión: {issueDate}");
                            right.Item().Text("Moneda: USD (Dólares)");
                        });
                    });

                    col.Item().PaddingTop(12).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(8).Column(buyer =>
                    {
                        buyer.Item().Text("Razón Social Cliente: CORPORACIÓN ANDINA HOLDING S.A. | RUC Cliente: 0991234567001").SemiBold();
                        buyer.Item().Text($"Referencia ERP / Orden de Compra Asociada: {orderNumber}");
                    });
                });

                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken3).Padding(6).Text("Descripción / SKU").FontColor(Colors.White).SemiBold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(6).AlignRight().Text("Cant.").FontColor(Colors.White).SemiBold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(6).AlignRight().Text("P. Unitario").FontColor(Colors.White).SemiBold();
                            header.Cell().Background(Colors.Blue.Darken3).Padding(6).AlignRight().Text("Subtotal").FontColor(Colors.White).SemiBold();
                        });

                        foreach (var item in items)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(item.Description);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text(item.Qty.ToString());
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text($"${item.UnitPrice:F2}");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).AlignRight().Text($"${item.LineTotal:F2}");
                        }
                    });

                    col.Item().PaddingTop(18).AlignRight().Width(240).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(totals =>
                    {
                        totals.Item().Row(r =>
                        {
                            r.RelativeItem().Text("Subtotal Base Imponible:");
                            r.ConstantItem(90).AlignRight().Text($"${subtotal:F2}").SemiBold();
                        });
                        totals.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Text($"{taxLabel} ({taxRatePercent:F0}%):");
                            r.ConstantItem(90).AlignRight().Text($"${taxAmount:F2}").SemiBold();
                        });
                        totals.Item().PaddingTop(6).BorderTop(1).BorderColor(Colors.Grey.Medium).Row(r =>
                        {
                            r.RelativeItem().Text("VALOR TOTAL USD:").Bold().FontSize(11);
                            r.ConstantItem(90).AlignRight().Text($"${totalAmount:F2}").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                        });
                    });
                });

                page.Footer().AlignCenter().Text("Documento tributario electrónico generado para pruebas de conciliación B2B AuditAI.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf(outputPath);
    }

    private static void GenerateContractPdf(string outputPath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(42);
                page.DefaultTextStyle(x => x.FontSize(10).LineHeight(1.35f).FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Text("CONTRATO MARCO DE SERVICIOS DE PROCESAMIENTO Y CUSTODIA CLOUD B2B")
                        .Bold().FontSize(13).FontColor(Colors.Red.Darken3);
                    col.Item().PaddingTop(4).Text("Contrato No. LEG-2026-089 | Proveedor: DataCore Outsourcing S.A. | RUC: 1793019284001 | Valor Anual: $48,000.00 USD")
                        .SemiBold().FontSize(9.5f);
                    col.Item().PaddingTop(6).BorderBottom(1).BorderColor(Colors.Grey.Lighten1);
                });

                page.Content().PaddingVertical(14).Column(col =>
                {
                    col.Spacing(10);

                    col.Item().Text("CLÁUSULA PRIMERA.- COMPARECIENTES Y OBJETO: Comparecen por una parte CORPORACIÓN ANDINA HOLDING S.A. ('EL CLIENTE') y por otra parte DATACORE OUTSOURCING S.A. con RUC 1793019284001 ('EL PROVEEDOR'), para la prestación del servicio de hospedaje de bases de datos financieras por un canon anual de $48,000.00 USD más IVA.");

                    col.Item().Text("CLÁUSULA CUARTA.- VIGENCIA Y RENOVACIÓN AUTOMÁTICA FORZOSA: El presente contrato tendrá una duración inicial de doce (12) meses. Al vencimiento, el contrato se renovará automáticamente de manera forzosa e irrevocable por períodos sucesivos de treinta y seis (36) meses con un incremento tarifario automático del 18% anual, salvo que EL CLIENTE notifique su voluntad de no renovar mediante escritura pública notariada con al menos ciento ochenta (180) días calendario de anticipación.");

                    col.Item().Text("CLÁUSULA SÉPTIMA.- PENALIDAD UNILATERAL POR TERMINACIÓN ANTICIPADA: En caso de que EL CLIENTE decida dar por terminado el presente contrato antes del vencimiento del plazo inicial o de cualquiera de sus prórrogas, deberá pagar de forma inmediata y sin derecho a reclamo una multa indemnizatoria fija equivalente a $25,000.00 USD (veinticinco mil dólares), adicional al pago íntegro de todas las mensualidades restantes. Por el contrario, EL PROVEEDOR podrá suspender o terminar unilateralmente el servicio en cualquier momento con un aviso simple de 48 horas sin penalidad alguna a su cargo.");

                    col.Item().Text("CLÁUSULA DÉCIMA.- EXONERACIÓN ABSOLUTA DE RESPONSABILIDAD POR INCIDENTES DE SEGURIDAD: EL PROVEEDOR no asume responsabilidad civil, contractual ni regulatoria alguna frente a pérdidas de información contable, secuestro de datos (ransomware), filtración de datos confidenciales o interrupciones prolongadas del servicio, aun cuando dichos eventos deriven de negligencia operativa directa del personal de EL PROVEEDOR. La responsabilidad máxima acumulada de EL PROVEEDOR bajo cualquier concepto se limita a $100.00 USD.");

                    col.Item().Text("CLÁUSULA DUODÉCIMA.- CESIÓN LIBRE Y JURISDICCIÓN EXTRANJERA: EL PROVEEDOR queda facultado para subcontratar o ceder libremente la custodia de las bases de datos a terceros en cualquier jurisdicción sin notificar a EL CLIENTE. Cualquier controversia deberá someterse exclusivamente a arbitraje privado en el extranjero con renuncia expresa al domicilio del cliente en Ecuador.");

                    col.Item().PaddingTop(18).Text("Suscrito en Quito, a los 26 días del mes de noviembre de 2026.").Italic();
                });

                page.Footer().AlignCenter().Text("Contrato de prueba para auditoría legal IA — AuditAI Enterprise Platform")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        }).GeneratePdf(outputPath);
    }

    private sealed record SampleSpec(
        string FileName,
        string Title,
        string ScenarioDescription,
        string ExpectedOutcome);
}
