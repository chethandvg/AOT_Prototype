using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.API.Data;
using PropertyManagement.Models;

namespace PropertyManagement.API.Controllers;

[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly PropertyManagementDbContext _context;
    private readonly ILogger<FilesController> _logger;
    private readonly string _uploadPath;

    public FilesController(PropertyManagementDbContext context, ILogger<FilesController> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _uploadPath = configuration["FileUploadPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        
        if (!Directory.Exists(_uploadPath))
        {
            Directory.CreateDirectory(_uploadPath);
        }
    }

    [HttpPost("buildings/{buildingId}")]
    public async Task<ActionResult<BuildingFile>> UploadBuildingFile(int buildingId, IFormFile file, [FromForm] FileType fileType = FileType.Other)
    {
        var building = await _context.Buildings.FindAsync(buildingId);
        if (building == null)
        {
            return NotFound();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded" });
        }

        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(_uploadPath, "buildings", buildingId.ToString(), fileName);
        var directory = Path.GetDirectoryName(filePath);
        
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var buildingFile = new BuildingFile
        {
            BuildingId = buildingId,
            FileName = file.FileName,
            FilePath = filePath,
            FileType = fileType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow
        };

        _context.BuildingFiles.Add(buildingFile);
        await _context.SaveChangesAsync();

        return Ok(buildingFile);
    }

    [HttpPost("units/{unitId}")]
    public async Task<ActionResult<UnitFile>> UploadUnitFile(int unitId, IFormFile file, [FromForm] FileType fileType = FileType.Other)
    {
        var unit = await _context.Units.FindAsync(unitId);
        if (unit == null)
        {
            return NotFound();
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded" });
        }

        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(_uploadPath, "units", unitId.ToString(), fileName);
        var directory = Path.GetDirectoryName(filePath);
        
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var unitFile = new UnitFile
        {
            UnitId = unitId,
            FileName = file.FileName,
            FilePath = filePath,
            FileType = fileType,
            FileSize = file.Length,
            UploadedAt = DateTime.UtcNow
        };

        _context.UnitFiles.Add(unitFile);
        await _context.SaveChangesAsync();

        return Ok(unitFile);
    }

    [HttpDelete("buildings/{fileId}")]
    public async Task<IActionResult> DeleteBuildingFile(int fileId)
    {
        var file = await _context.BuildingFiles.FindAsync(fileId);
        if (file == null)
        {
            return NotFound();
        }

        file.IsDeleted = true;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("units/{fileId}")]
    public async Task<IActionResult> DeleteUnitFile(int fileId)
    {
        var file = await _context.UnitFiles.FindAsync(fileId);
        if (file == null)
        {
            return NotFound();
        }

        file.IsDeleted = true;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("buildings/{buildingId}")]
    public async Task<ActionResult<IEnumerable<BuildingFile>>> GetBuildingFiles(int buildingId)
    {
        var files = await _context.BuildingFiles
            .Where(f => f.BuildingId == buildingId && !f.IsDeleted)
            .ToListAsync();

        return Ok(files);
    }

    [HttpGet("units/{unitId}")]
    public async Task<ActionResult<IEnumerable<UnitFile>>> GetUnitFiles(int unitId)
    {
        var files = await _context.UnitFiles
            .Where(f => f.UnitId == unitId && !f.IsDeleted)
            .ToListAsync();

        return Ok(files);
    }
}
