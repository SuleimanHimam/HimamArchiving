using Archiving.Domain.Enums;

namespace Archiving.Application.Common.Interfaces;

/// <summary>Ambient information about the authenticated caller, resolved per request.</summary>
public interface ICurrentUser
{
    long? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    ConfidentialityLevel Clearance { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}
