using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartOps.Application.Modules.Student.DTOs;
using SmartOps.Domain.Common.Enums;
using SmartOps.Domain.Modules.Student.Entities;
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

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAllStudents(
        [FromQuery] int pageIndex = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] string? searchTerm = null, 
        [FromQuery] string? sortColumn = null, 
        [FromQuery] string? sortDirection = null,
        [FromQuery] StudentFilter filter = StudentFilter.Active)
    {
        var result = await _studentRepository.GetAllStudentsAsync(pageIndex, pageSize, searchTerm, sortColumn, sortDirection, filter);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStudentById(Guid id)
    {
        var student = await _studentRepository.GetStudentByIdAsync(id);
        if (student == null) return NotFound();
        return Ok(student);
    }

    [HttpPut("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateStudent(Guid id, [FromBody] StudentEntity student)
    {
        if (id != student.Id) return BadRequest();
        await _studentRepository.UpdateStudentAsync(student);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> DeleteStudent(Guid id)
    {
        await _studentRepository.DeleteStudentAsync(id);
        return NoContent();
    }
}
