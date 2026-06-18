using Archiving.Domain.Entities;

namespace Archiving.Application.Common.Interfaces;

public interface IJwtTokenService
{
    /// <summary>Creates a signed access token for the user, embedding role &amp; permission claims.</summary>
    (string token, DateTime expiresAt) CreateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions);

    /// <summary>Creates a cryptographically-random opaque refresh token.</summary>
    string CreateRefreshToken();
}
