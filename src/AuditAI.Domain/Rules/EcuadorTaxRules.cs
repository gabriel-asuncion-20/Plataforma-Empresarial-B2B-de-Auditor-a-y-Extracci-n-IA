namespace AuditAI.Domain.Rules;

public static class EcuadorTaxRules
{
    public const decimal StandardIvaRate = 0.15m;
    public const decimal RoundingToleranceUsd = 0.05m;

    /// <summary>
    /// Valida el formato estructural de un RUC ecuatoriano (13 dígitos numéricos,
    /// código de provincia 01-24 o 30, y sufijo de establecimiento >= 001).
    /// </summary>
    public static bool IsValidRuc(string? ruc, out string reason)
    {
        if (string.IsNullOrWhiteSpace(ruc))
        {
            reason = "El documento no especifica un RUC de proveedor.";
            return false;
        }

        var normalized = new string(ruc.Where(char.IsDigit).ToArray());
        if (normalized.Length != 13)
        {
            reason = $"El RUC '{ruc}' debe contener exactamente 13 dígitos numéricos (encontrados: {normalized.Length}).";
            return false;
        }

        if (!int.TryParse(normalized[..2], out var province) || ((province < 1 || province > 24) && province != 30))
        {
            reason = $"El código de provincia '{normalized[..2]}' en el RUC '{normalized}' no corresponde a una provincia válida del Ecuador.";
            return false;
        }

        if (!int.TryParse(normalized[10..13], out var establishment) || establishment < 1)
        {
            reason = $"Los últimos tres dígitos ('{normalized[10..13]}') del RUC deben ser un establecimiento activo (>= 001).";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public static string NormalizeRuc(string? ruc)
    {
        if (string.IsNullOrWhiteSpace(ruc))
        {
            return string.Empty;
        }

        return new string(ruc.Where(char.IsDigit).ToArray());
    }

    public static decimal CalculateExpectedIva(decimal subtotal, decimal rate = StandardIvaRate)
        => Math.Round(subtotal * rate, 2, MidpointRounding.AwayFromZero);
}
