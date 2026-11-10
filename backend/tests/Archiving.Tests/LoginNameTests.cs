using Archiving.Application.Features.Auth;
using Archiving.Domain.Entities;
using Archiving.Infrastructure.Auth;
using Archiving.Infrastructure.Persistence;
using Archiving.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Archiving.Tests;

/// <summary>
/// Signing in by the short name.
///
/// Accounts are addressed by email, but nobody is told "admin@archiving.local" out loud — they are
/// told "admin". The sign-in box therefore takes the part before the @ as well, and the rules worth
/// pinning down are that it resolves to the right account, that the full address still works, and
/// that a short name shared by two accounts resolves to neither rather than to whichever the
/// database happened to return first.
/// </summary>
public class LoginNameTests
{
    private const string Password = "Admin@12345";

    private static (AuthService Auth, AppDbContext Db) NewService(params string[] emails)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        var hasher = new PasswordHasher();
        long id = 1;
        foreach (var email in emails)
        {
            db.Users.Add(new User
            {
                Id = id++,
                FullName = email,
                Email = email,
                PasswordHash = hasher.Hash(Password),
                IsActive = true,
            });
        }
        db.SaveChanges();

        var jwt = Options.Create(new JwtOptions { Key = new string('k', 48) });
        return (new AuthService(db, hasher, new JwtTokenService(jwt), jwt), db);
    }

    private static Task<Application.Common.Models.Result<AuthResponse>> Login(AuthService auth, string who)
        => auth.LoginAsync(new LoginRequest(who, Password), null);

    [Fact]
    public async Task Short_name_signs_in_the_account_that_owns_it()
    {
        var (auth, _) = NewService("admin@archiving.local", "hr.manager@archiving.local");

        var result = await Login(auth, "admin");

        Assert.True(result.Succeeded);
        Assert.Equal("admin@archiving.local", result.Value!.User.Email);
    }

    [Fact]
    public async Task Full_address_still_signs_in()
    {
        var (auth, _) = NewService("admin@archiving.local");

        var result = await Login(auth, "admin@archiving.local");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Short_name_is_case_and_space_insensitive()
    {
        var (auth, _) = NewService("admin@archiving.local");

        var result = await Login(auth, "  Admin ");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task An_ambiguous_short_name_signs_nobody_in()
    {
        // The same local part on two domains: picking one would hand whoever typed it an account
        // that is not theirs, and which one depends on row order.
        var (auth, _) = NewService("admin@archiving.local", "admin@other.gov");

        var result = await Login(auth, "admin");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task A_short_name_nobody_owns_is_refused()
    {
        var (auth, _) = NewService("admin@archiving.local");

        var result = await Login(auth, "root");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task The_wrong_password_is_still_refused_under_a_short_name()
    {
        var (auth, _) = NewService("admin@archiving.local");

        var result = await auth.LoginAsync(new LoginRequest("admin", "not-the-password"), null);

        Assert.False(result.Succeeded);
    }
}
