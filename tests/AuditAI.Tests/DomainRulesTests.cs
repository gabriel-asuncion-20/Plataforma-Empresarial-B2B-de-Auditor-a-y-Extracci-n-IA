using AuditAI.Domain.Entities;
using AuditAI.Domain.Enums;
using AuditAI.Domain.Rules;

namespace AuditAI.Tests;

public sealed class DomainRulesTests
{
    [Theory]
    [InlineData("0992847561001")] // Guayas sociedad
    [InlineData("1792458312001")] // Pichincha sociedad
    [InlineData("0190482915001")] // Azuay sociedad
    [InlineData("3091234567001")] // Código especial 30
    public void IsValidRuc_ShouldReturnTrue_ForValidEcuadorianRucs(string ruc)
    {
        var isValid = EcuadorTaxRules.IsValidRuc(ruc, out var reason);

        Assert.True(isValid);
        Assert.Equal(string.Empty, reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("9912345670001")] // Provincia 99 inválida
    [InlineData("0012345670001")] // Provincia 00 inválida
    [InlineData("0992847561000")] // Establecimiento 000 inválido
    public void IsValidRuc_ShouldReturnFalse_ForInvalidRucs(string invalidRuc)
    {
        var isValid = EcuadorTaxRules.IsValidRuc(invalidRuc, out var reason);

        Assert.False(isValid);
        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Theory]
    [InlineData(1000.00, 150.00)]
    [InlineData(11000.00, 1650.00)]
    [InlineData(3800.00, 570.00)]
    [InlineData(99.95, 14.99)]
    public void CalculateExpectedIva_ShouldApply15PercentWithStandardRounding(decimal subtotal, decimal expectedIva)
    {
        var iva = EcuadorTaxRules.CalculateExpectedIva(subtotal);

        Assert.Equal(expectedIva, iva);
    }

    [Fact]
    public void AuditDocument_ApproveManually_ShouldTransitionStateAndStoreAuditMetadata()
    {
        var doc = new AuditDocument { Status = AuditStatus.FlaggedDiscrepancy };
        var adminId = Guid.NewGuid();

        doc.ApproveManually(adminId, "Valeria Mendoza", "Excepción autorizada por gerencia financiera.");

        Assert.Equal(AuditStatus.ManuallyApproved, doc.Status);
        Assert.Equal(adminId, doc.ReviewedByUserId);
        Assert.Equal("Valeria Mendoza", doc.ReviewedByName);
        Assert.Equal("Excepción autorizada por gerencia financiera.", doc.ReviewNotes);
        Assert.NotNull(doc.ReviewedAtUtc);
    }

    [Fact]
    public void AuditDocument_Reject_ShouldThrow_WhenReasonIsEmpty()
    {
        var doc = new AuditDocument { Status = AuditStatus.FlaggedDiscrepancy };

        Assert.Throws<ArgumentException>(() => doc.Reject(Guid.NewGuid(), "Valeria Mendoza", "   "));
    }

    [Fact]
    public void AuditDocument_Review_ShouldThrow_WhileStillInAsyncQueue()
    {
        var doc = new AuditDocument { Status = AuditStatus.AnalyzingWithAi };

        Assert.Throws<InvalidOperationException>(() => doc.ApproveManually(Guid.NewGuid(), "Admin", "OK"));
        Assert.Throws<InvalidOperationException>(() => doc.Reject(Guid.NewGuid(), "Admin", "Motivo"));
    }
}
