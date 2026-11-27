using AuditAI.Application.Common.Cqrs;
using AuditAI.Application.Common.Interfaces;
using AuditAI.Application.DTOs;
using AuditAI.Domain.Enums;

namespace AuditAI.Application.Features.Auth;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthResponseDto>;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator) : ICommandHandler<LoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> HandleAsync(LoginCommand command, CancellationToken ct = default)
    {
        var user = await userRepository.GetByEmailAsync(command.Email.Trim().ToLowerInvariant(), ct);
        if (user is null || !user.IsActive || !passwordHasher.Verify(command.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Credenciales inválidas o cuenta inactiva.");
        }

        var (token, expiresAt) = jwtTokenGenerator.GenerateToken(user);
        return new AuthResponseDto(
            token,
            expiresAt,
            new UserDto(user.Id, user.Email, user.FullName, user.Role.ToString()));
    }
}

public sealed record DemoLoginCommand(string Role) : ICommand<AuthResponseDto>;

public sealed class DemoLoginCommandHandler(
    IUserRepository userRepository,
    IJwtTokenGenerator jwtTokenGenerator) : ICommandHandler<DemoLoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> HandleAsync(DemoLoginCommand command, CancellationToken ct = default)
    {
        var parsedRole = string.Equals(command.Role?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase)
            ? UserRole.Admin
            : UserRole.Analyst;

        var user = await userRepository.GetFirstByRoleAsync(parsedRole, ct)
            ?? throw new InvalidOperationException($"No se encontró usuario semilla para el rol {parsedRole}.");

        var (token, expiresAt) = jwtTokenGenerator.GenerateToken(user);
        return new AuthResponseDto(
            token,
            expiresAt,
            new UserDto(user.Id, user.Email, user.FullName, user.Role.ToString()));
    }
}
