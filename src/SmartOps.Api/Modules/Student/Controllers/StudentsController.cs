using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Student.DTOs;
using SmartOps.Domain.Modules.Student.Interfaces;

namespace SmartOps.Api.Modules.Student.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly IStudentRepository _studentRepository;

    public StudentsController(IStudentRepository studentRepository)
    {
        _studentRepository = studentRepository;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateStudent([FromBody] CreateStudentDto request)
    {
        if (request == null)
        {
            return BadRequest("Student data is required.");
        }

        var entity = request.ToEntity();
        
        var studentId = await _studentRepository.CreateStudentAsync(entity);

        return Ok(new { Message = "Student created successfully", StudentId = studentId });
    }
}
