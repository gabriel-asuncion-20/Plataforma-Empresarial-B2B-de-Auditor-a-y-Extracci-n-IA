namespace AuditAI.Domain.Enums;

public enum UserRole
{
    Admin = 1,
    Analyst = 2
}

public enum DocumentType
{
    Invoice = 1,
    Contract = 2
}

public enum AuditStatus
{
    Queued = 1,
    ExtractingText = 2,
    AnalyzingWithAi = 3,
    Reconciling = 4,
    ApprovedAuto = 5,
    FlaggedDiscrepancy = 6,
    ManuallyApproved = 7,
    Rejected = 8,
    Failed = 9
}

public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum FindingSeverity
{
    Info = 1,
    Warning = 2,
    Critical = 3
}
