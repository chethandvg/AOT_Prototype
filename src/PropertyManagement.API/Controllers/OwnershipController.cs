using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.API.Data;
using PropertyManagement.Models;

namespace PropertyManagement.API.Controllers;

[ApiController]
[Route("api/ownership")]
public class OwnershipController : ControllerBase
{
    private readonly PropertyManagementDbContext _context;
    private readonly ILogger<OwnershipController> _logger;
    private const decimal EPSILON = 0.01m;

    public OwnershipController(PropertyManagementDbContext context, ILogger<OwnershipController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPut("buildings/{buildingId}")]
    public async Task<IActionResult> UpdateBuildingOwnership(int buildingId, [FromBody] List<OwnershipShareRequest> shares)
    {
        var building = await _context.Buildings.FindAsync(buildingId);
        if (building == null)
        {
            return NotFound();
        }

        // Validate ownership shares
        var validation = ValidateOwnershipShares(shares);
        if (!validation.IsValid)
        {
            return BadRequest(new { error = validation.Error });
        }

        // Remove existing shares
        var existingShares = await _context.BuildingOwnershipShares
            .Where(s => s.BuildingId == buildingId)
            .ToListAsync();
        _context.BuildingOwnershipShares.RemoveRange(existingShares);

        // Add new shares
        var newShares = shares.Select(s => new BuildingOwnershipShare
        {
            BuildingId = buildingId,
            OwnerId = s.OwnerId,
            SharePercent = s.SharePercent,
            EffectiveDate = s.EffectiveDate ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.BuildingOwnershipShares.AddRange(newShares);
        await _context.SaveChangesAsync();

        return Ok(newShares);
    }

    [HttpPut("units/{unitId}")]
    public async Task<IActionResult> UpdateUnitOwnership(int unitId, [FromBody] List<OwnershipShareRequest> shares)
    {
        var unit = await _context.Units.FindAsync(unitId);
        if (unit == null)
        {
            return NotFound();
        }

        // Validate ownership shares
        var validation = ValidateOwnershipShares(shares);
        if (!validation.IsValid)
        {
            return BadRequest(new { error = validation.Error });
        }

        // Remove existing shares
        var existingShares = await _context.UnitOwnershipShares
            .Where(s => s.UnitId == unitId)
            .ToListAsync();
        _context.UnitOwnershipShares.RemoveRange(existingShares);

        // Add new shares
        var newShares = shares.Select(s => new UnitOwnershipShare
        {
            UnitId = unitId,
            OwnerId = s.OwnerId,
            SharePercent = s.SharePercent,
            EffectiveDate = s.EffectiveDate ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.UnitOwnershipShares.AddRange(newShares);
        
        // Update HasOwnershipOverride flag
        unit.HasOwnershipOverride = true;
        await _context.SaveChangesAsync();

        return Ok(newShares);
    }

    [HttpDelete("units/{unitId}/override")]
    public async Task<IActionResult> RemoveUnitOwnershipOverride(int unitId)
    {
        var unit = await _context.Units.FindAsync(unitId);
        if (unit == null)
        {
            return NotFound();
        }

        // Remove all unit ownership shares
        var existingShares = await _context.UnitOwnershipShares
            .Where(s => s.UnitId == unitId)
            .ToListAsync();
        _context.UnitOwnershipShares.RemoveRange(existingShares);

        // Update HasOwnershipOverride flag
        unit.HasOwnershipOverride = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("buildings/{buildingId}")]
    public async Task<ActionResult<IEnumerable<BuildingOwnershipShare>>> GetBuildingOwnership(int buildingId)
    {
        var shares = await _context.BuildingOwnershipShares
            .Include(s => s.Owner)
            .Where(s => s.BuildingId == buildingId)
            .ToListAsync();

        return Ok(shares);
    }

    [HttpGet("units/{unitId}")]
    public async Task<ActionResult<IEnumerable<UnitOwnershipShare>>> GetUnitOwnership(int unitId)
    {
        var shares = await _context.UnitOwnershipShares
            .Include(s => s.Owner)
            .Where(s => s.UnitId == unitId)
            .ToListAsync();

        return Ok(shares);
    }

    private (bool IsValid, string? Error) ValidateOwnershipShares(List<OwnershipShareRequest> shares)
    {
        if (shares == null || shares.Count == 0)
        {
            return (false, "At least one ownership share is required");
        }

        // Check for duplicate owners
        var ownerIds = shares.Select(s => s.OwnerId).ToList();
        if (ownerIds.Count != ownerIds.Distinct().Count())
        {
            return (false, "Each owner can only appear once in ownership shares");
        }

        // Check each share is greater than 0
        if (shares.Any(s => s.SharePercent <= 0))
        {
            return (false, "Share percent must be greater than 0");
        }

        // Check total equals 100 (with epsilon tolerance)
        var total = shares.Sum(s => s.SharePercent);
        if (Math.Abs(total - 100.00m) > EPSILON)
        {
            return (false, $"Ownership shares must sum to 100.00 (current: {total:F2})");
        }

        return (true, null);
    }
}

public class OwnershipShareRequest
{
    public int OwnerId { get; set; }
    public decimal SharePercent { get; set; }
    public DateTime? EffectiveDate { get; set; }
}
