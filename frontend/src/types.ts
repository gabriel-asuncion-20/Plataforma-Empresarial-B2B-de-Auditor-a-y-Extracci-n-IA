export interface UserDto {
  id: string;
  email: string;
  fullName: string;
  role: 'Admin' | 'Analyst' | string;
}

export interface AuthResponseDto {
  accessToken: string;
  expiresAtUtc: string;
  user: UserDto;
}

export interface PurchaseOrderItemDto {
  id: string;
  sku: string;
  description: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface PurchaseOrderDto {
  id: string;
  orderNumber: string;
  supplierName: string;
  supplierRuc: string;
  currency: string;
  subtotal: number;
  taxRate: number;
  taxAmount: number;
  totalAmount: number;
  issuedAtUtc: string;
  expectedDeliveryUtc: string;
  status: string;
  items: PurchaseOrderItemDto[];
}

export interface AuditFindingDto {
  id: string;
  code: string;
  category: string;
  severity: 'Info' | 'Warning' | 'Critical' | string;
  title: string;
  description: string;
  expectedValue?: string | null;
  actualValue?: string | null;
  financialImpactUsd?: number | null;
}

export interface AuditDocumentSummaryDto {
  id: string;
  originalFileName: string;
  fileSizeBytes: number;
  pageCount: number;
  documentType: 'Invoice' | 'Contract' | string;
  status: string;
  riskLevel: 'Low' | 'Medium' | 'High' | 'Critical' | string;
  extractedOrderNumber?: string | null;
  extractedInvoiceNumber?: string | null;
  extractedSupplierName?: string | null;
  extractedSupplierRuc?: string | null;
  extractedSubtotal?: number | null;
  extractedTaxAmount?: number | null;
  extractedTotalAmount?: number | null;
  detectedVarianceAmount?: number | null;
  aiExecutiveSummary?: string | null;
  aiModelUsed?: string | null;
  processingTimeMs?: number | null;
  uploadedByName: string;
  reviewedByName?: string | null;
  reviewNotes?: string | null;
  createdAtUtc: string;
  processedAtUtc?: string | null;
  reviewedAtUtc?: string | null;
  findingsCount: number;
  criticalFindingsCount: number;
}

export interface AuditDocumentDetailDto extends Omit<AuditDocumentSummaryDto, 'findingsCount' | 'criticalFindingsCount'> {
  matchedPurchaseOrderId?: string | null;
  matchedPurchaseOrder?: PurchaseOrderDto | null;
  extractedTaxRate?: number | null;
  structuredExtractionJson?: string | null;
  extractedRawTextPreview?: string | null;
  findings: AuditFindingDto[];
}

export interface DashboardMetricsDto {
  totalDocuments: number;
  inProgressCount: number;
  approvedCount: number;
  flaggedCount: number;
  rejectedCount: number;
  highRiskCount: number;
  totalAuditedAmountUsd: number;
  totalDetectedVarianceUsd: number;
  averageProcessingTimeMs: number;
}

export interface SamplePdfInfoDto {
  fileName: string;
  title: string;
  scenarioDescription: string;
  expectedOutcome: string;
  fileSizeBytes: number;
}

export interface RealtimeAuditUpdateDto {
  documentId: string;
  fileName: string;
  status: string;
  riskLevel: string;
  stageMessage: string;
  detectedVarianceAmount?: number | null;
  findingsCount: number;
  timestampUtc: string;
  document: AuditDocumentSummaryDto;
}
