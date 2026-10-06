using System.Text.RegularExpressions;
using Erpos.Application.Dtos;
using Erpos.Domain.Entities;

namespace Erpos.Application.Common;

public static partial class Guard
{
    public static string Required(string? value, string field, int max = 200)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) throw new ValidationException($"{field} is required.");
        if (v.Length > max) throw new ValidationException($"{field} must be at most {max} characters.");
        return v;
    }

    public static string Email(string? value)
    {
        var v = Required(value, "Email", 256).ToLowerInvariant();
        if (!EmailRegex().IsMatch(v)) throw new ValidationException("Email is not valid.");
        return v;
    }

    public static string Code(string? value, string field = "Code")
    {
        var v = Required(value, field, 50).ToUpperInvariant();
        if (!CodeRegex().IsMatch(v)) throw new ValidationException($"{field} may contain only letters, digits, '-' and '_'.");
        return v;
    }

    public static void Password(string? value)
    {
        if (value == null || value.Length < 12 || !value.Any(char.IsDigit) || !value.Any(char.IsLetter))
            throw new ValidationException("Password must be at least 12 characters and contain letters and digits.");
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^[A-Z0-9_\-]+$")]
    private static partial Regex CodeRegex();
}

public static class Mapping
{
    public static UserDto ToDto(this User u) => new(u.Id, u.Email, u.FullName, u.Phone, u.UserType, u.IsActive,
        u.PrimaryEntityId, u.PrimaryEntity?.Name, u.LastLoginAt, u.CreatedAt);

    public static TenantDto ToDto(this Tenant t, int users, int entities) => new(t.Id, t.Name, t.Code, t.Status,
        t.ContactEmail, t.ContactPhone, t.Country, t.MaxUsers, t.CreatedAt, users, entities);
}
