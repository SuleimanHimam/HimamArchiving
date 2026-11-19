using Archiving.Api.Authorization;
using Archiving.Application.Common.Models;
using Archiving.Application.Features.Hr;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Archiving.Api.Controllers.Hr;

// Endpoints only: permission, call, result. Row scope and business rules live in the services.

internal static class HrResults
{
    public static IActionResult From<T>(this ControllerBase c, Result<T> r) =>
        r.Succeeded ? c.Ok(r.Value) : c.BadRequest(new { error = r.Error });

    public static IActionResult Deleted(this ControllerBase c, Result<bool> r) =>
        r.Succeeded ? c.NoContent() : c.BadRequest(new { error = r.Error });
}

[ApiController]
[Route("api/hr/settings/action-types")]
[Authorize]
public sealed class HrActionTypesController(IActionTypeService types) : ControllerBase
{
    [HttpGet]
    [HasPermission("HrActions.View")]
    public async Task<IActionResult> List([FromQuery] bool activeOnly, CancellationToken ct) => Ok(await types.ListAsync(activeOnly, ct));

    [HttpPost]
    [HasPermission("HrSettings.Edit")]
    public async Task<IActionResult> Create([FromBody] SaveActionTypeRequest request, CancellationToken ct) =>
        this.From(await types.CreateAsync(request, ct));

    [HttpPut("{id:long}")]
    [HasPermission("HrSettings.Edit")]
    public async Task<IActionResult> Update(long id, [FromBody] SaveActionTypeRequest request, CancellationToken ct) =>
        this.From(await types.UpdateAsync(id, request, ct));
}

[ApiController]
[Route("api/hr/employees/{employeeId:long}")]
[Authorize]
public sealed class HrEmployeeFileController(IEmployeeFileService files) : ControllerBase
{
    [HttpGet("file")]
    [HasPermission("HrProfile.View")]
    public async Task<IActionResult> Get(long employeeId, CancellationToken ct) => this.From(await files.GetAsync(employeeId, ct));

    [HttpPut("profile")]
    [HasPermission("HrProfile.Edit")]
    public async Task<IActionResult> SaveProfile(long employeeId, [FromBody] SaveProfileRequest request, CancellationToken ct) =>
        this.From(await files.SaveProfileAsync(employeeId, request, ct));

    [HttpPost("reveal")]
    [HasPermission("HrSensitive.View")]
    public async Task<IActionResult> Reveal(long employeeId, [FromBody] RevealRequest request, CancellationToken ct) =>
        this.From(await files.RevealAsync(employeeId, request, ct));

    [HttpPost("family")]
    [HasPermission("HrProfile.Edit")]
    public async Task<IActionResult> AddFamily(long employeeId, [FromBody] SaveFamilyMemberRequest request, CancellationToken ct) =>
        this.From(await files.AddFamilyMemberAsync(employeeId, request, ct));

    [HttpPut("family/{id:long}")]
    [HasPermission("HrProfile.Edit")]
    public async Task<IActionResult> UpdateFamily(long employeeId, long id, [FromBody] SaveFamilyMemberRequest request, CancellationToken ct) =>
        this.From(await files.UpdateFamilyMemberAsync(employeeId, id, request, ct));

    [HttpDelete("family/{id:long}")]
    [HasPermission("HrProfile.Edit")]
    public async Task<IActionResult> DeleteFamily(long employeeId, long id, CancellationToken ct) =>
        this.Deleted(await files.DeleteFamilyMemberAsync(employeeId, id, ct));

    [HttpPost("qualifications")]
    [HasPermission("HrProfile.Edit")]
    public async Task<IActionResult> AddQualification(long employeeId, [FromBody] SaveQualificationRequest request, CancellationToken ct) =>
        this.From(await files.AddQualificationAsync(employeeId, request, ct));

    [HttpPut("qualifications/{id:long}")]
    [HasPermission("HrProfile.Edit")]
    public async Task<IActionResult> UpdateQualification(long employeeId, long id, [FromBody] SaveQualificationRequest request, CancellationToken ct) =>
        this.From(await files.UpdateQualificationAsync(employeeId, id, request, ct));

    [HttpDelete("qualifications/{id:long}")]
    [HasPermission("HrProfile.Edit")]
    public async Task<IActionResult> DeleteQualification(long employeeId, long id, CancellationToken ct) =>
        this.Deleted(await files.DeleteQualificationAsync(employeeId, id, ct));

    [HttpPost("bank-accounts")]
    [HasPermission("HrProfile.Edit")]
    public async Task<IActionResult> AddBank(long employeeId, [FromBody] SaveBankAccountRequest request, CancellationToken ct) =>
        this.From(await files.AddBankAccountAsync(employeeId, request, ct));

    [HttpPut("bank-accounts/{id:long}")]
    [HasPermission("HrProfile.Edit")]
    public async Task<IActionResult> UpdateBank(long employeeId, long id, [FromBody] SaveBankAccountRequest request, CancellationToken ct) =>
        this.From(await files.UpdateBankAccountAsync(employeeId, id, request, ct));

    [HttpDelete("bank-accounts/{id:long}")]
    [HasPermission("HrProfile.Edit")]
    public async Task<IActionResult> DeleteBank(long employeeId, long id, CancellationToken ct) =>
        this.Deleted(await files.DeleteBankAccountAsync(employeeId, id, ct));
}

[ApiController]
[Route("api/hr/actions")]
[Authorize]
public sealed class HrPersonnelActionsController(IPersonnelActionService actions) : ControllerBase
{
    [HttpGet]
    [HasPermission("HrActions.View")]
    public async Task<IActionResult> List([FromQuery] PersonnelActionListQuery query, CancellationToken ct) =>
        Ok(await actions.ListAsync(query, ct));

    [HttpGet("{id:long}")]
    [HasPermission("HrActions.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => this.From(await actions.GetAsync(id, ct));

    [HttpPost]
    [HasPermission("HrActions.Create")]
    public async Task<IActionResult> Create([FromBody] SavePersonnelActionRequest request, CancellationToken ct) =>
        this.From(await actions.CreateAsync(request, ct));

    [HttpPut("{id:long}")]
    [HasPermission("HrActions.Create")]
    public async Task<IActionResult> Update(long id, [FromBody] SavePersonnelActionRequest request, CancellationToken ct) =>
        this.From(await actions.UpdateAsync(id, request, ct));

    [HttpPost("{id:long}/submit")]
    [HasPermission("HrActions.Create")]
    public async Task<IActionResult> Submit(long id, CancellationToken ct) => this.From(await actions.SubmitAsync(id, ct));

    [HttpPost("{id:long}/cancel")]
    [HasPermission("HrActions.Create")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct) => this.From(await actions.CancelAsync(id, ct));
}

[ApiController]
[Route("api/hr/clearances")]
[Authorize]
public sealed class HrClearancesController(IClearanceService clearances) : ControllerBase
{
    [HttpGet]
    [HasPermission("HrClearance.View")]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken ct) => Ok(await clearances.ListAsync(status, ct));

    [HttpGet("{id:long}")]
    [HasPermission("HrClearance.View")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) => this.From(await clearances.GetAsync(id, ct));

    [HttpPost("{id:long}/items/{itemId:long}/clear")]
    [HasPermission("HrClearance.Edit")]
    public async Task<IActionResult> Clear(long id, long itemId, [FromBody] ClearItemRequest request, CancellationToken ct) =>
        this.From(await clearances.ClearItemAsync(id, itemId, request, ct));
}
