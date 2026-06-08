using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Notice;
using SmartOps.Application.Modules.Notice.Interfaces;
using SmartOps.Domain.Common.Constants;
using SmartOps.Domain.Modules.Notice;

namespace SmartOps.Api.Modules.Notice.Controllers;

[ApiController]
[Route("api/notices")]
[Authorize]
public sealed class NoticesController : ControllerBase
{
    private readonly INoticeService _service;

    public NoticesController(INoticeService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = MenuPolicies.Notices.View)]
    public async Task<IActionResult> GetList(CancellationToken ct)
    {
        var result = await _service.GetListAsync(ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Notices.View)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error?.Contains("not found", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = MenuPolicies.Notices.Add)]
    public async Task<IActionResult> Create([FromBody] CreateNoticeRequestDto request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Notices.Edit)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNoticeRequestDto request, CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/publish")]
    [Authorize(Policy = MenuPolicies.Notices.Edit)]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var result = await _service.PublishAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpGet("{id:guid}/responses")]
    [Authorize(Policy = MenuPolicies.Notices.View)]
    public async Task<IActionResult> GetResponses(Guid id, CancellationToken ct)
    {
        var result = await _service.GetResponsesAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("{id:guid}/respond")]
    [Authorize(Policy = MenuPolicies.MyActions.Edit)]
    public async Task<IActionResult> Respond(Guid id, [FromBody] RespondToNoticeRequestDto request, CancellationToken ct)
    {
        var result = await _service.RespondAsync(id, request, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = MenuPolicies.Notices.Edit)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpGet("audience-preview")]
    [Authorize(Policy = MenuPolicies.Notices.View)]
    public async Task<IActionResult> GetAudiencePreview(
        [FromQuery] NoticeTargetType targetType,
        [FromQuery] Guid? targetRefId,
        [FromQuery] IList<Guid>? targetRefIds,
        CancellationToken ct)
    {
        var result = await _service.GetAudiencePreviewAsync(targetType, targetRefId, targetRefIds, ct).ConfigureAwait(false);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }
}
