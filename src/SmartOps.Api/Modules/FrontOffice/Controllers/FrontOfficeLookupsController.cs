using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.FrontOffice.Interfaces;
using SmartOps.Domain.Common.Constants;

namespace SmartOps.Api.Modules.FrontOffice.Controllers;

[ApiController]
[Route("api/front-office/lookups")]
[Authorize]
public sealed class FrontOfficeLookupsController : ControllerBase
{
    private readonly IFrontOfficeService _service;

    public FrontOfficeLookupsController(IFrontOfficeService service) => _service = service;

    [HttpGet("employees")]
    [Authorize(Policy = MenuPolicies.FrontOfficeEmployeeLookup)]
    public async Task<IActionResult> GetEmployees(CancellationToken ct)
    {
        var result = await _service.GetActiveEmployeesAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
