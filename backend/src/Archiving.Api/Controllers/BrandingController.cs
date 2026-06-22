using Archiving.Api.Authorization;
using Archiving.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Archiving.Api.Controllers;

public sealed record BrandingDto(
    string  NameAr,
    string? NameEn,
    string? Code,
    string? Address,
    string? Phone,
    string? Email,
    string? LogoBase64,
    string? ColorPrimary,
    string? ColorAccent,
    string? ColorSeal,
    string? ColorBg);

public sealed record UpdateBrandingRequest(
    string  NameAr,
    string? NameEn,
    string? Code,
    string? Address,
    string? Phone,
    string? Email,
    string? LogoBase64,
    string? ColorPrimary,
    string? ColorAccent,
    string? ColorSeal,
    string? ColorBg);

[ApiController]
[Route("api/branding")]
public sealed class BrandingController(AppDbContext db) : ControllerBase
{
    private static BrandingDto ToDto(Archiving.Domain.Entities.Institution inst) => new(
        inst.Name, inst.NameEn, inst.Code, inst.Address, inst.Phone, inst.Email,
        inst.LogoBase64, inst.ColorPrimary, inst.ColorAccent, inst.ColorSeal, inst.ColorBg);

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var inst = await db.Institutions.FirstOrDefaultAsync(ct);
        if (inst is null)
            return Ok(new BrandingDto("", null, null, null, null, null, null, null, null, null, null));

        return Ok(ToDto(inst));
    }

    [HttpPut]
    [Authorize]
    [HasPermission("Organization.Edit")]
    public async Task<IActionResult> Update([FromBody] UpdateBrandingRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NameAr))
            return BadRequest(new { error = "اسم المؤسسة مطلوب" });

        if (!string.IsNullOrWhiteSpace(req.Email) && !req.Email.Contains('@'))
            return BadRequest(new { error = "البريد الإلكتروني غير صالح" });

        var inst = await db.Institutions.FirstOrDefaultAsync(ct);
        if (inst is null)
            return NotFound(new { error = "لا توجد مؤسسة مسجّلة في النظام" });

        inst.Name         = req.NameAr.Trim();
        inst.NameEn       = req.NameEn?.Trim();
        inst.Code         = req.Code?.Trim();
        inst.Address      = req.Address?.Trim();
        inst.Phone        = req.Phone?.Trim();
        inst.Email        = req.Email?.Trim();
        inst.LogoBase64   = req.LogoBase64;
        inst.ColorPrimary = req.ColorPrimary;
        inst.ColorAccent  = req.ColorAccent;
        inst.ColorSeal    = req.ColorSeal;
        inst.ColorBg      = req.ColorBg;
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(inst));
    }
}
