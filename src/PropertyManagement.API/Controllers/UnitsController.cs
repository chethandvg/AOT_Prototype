using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.API.Data;
using PropertyManagement.Models;

namespace PropertyManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UnitsController : ControllerBase
{
    private readonly PropertyManagementDbContext _context;
    private readonly ILogger<UnitsController> _logger;

    public UnitsController(PropertyManagementDbContext context, ILogger<UnitsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Unit>>> GetUnits([FromQuery] int? buildingId = null)
    {
        var query = _context.Units
            .Include(u => u.Building)
            .Include(u => u.OwnershipShares)
            .AsQueryable();

        if (buildingId.HasValue)
        {
            query = query.Where(u => u.BuildingId == buildingId.Value);
        }

        return await query.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Unit>> GetUnit(int id)
    {
        var unit = await _context.Units
            .Include(u => u.Building)
            .Include(u => u.OwnershipShares)
                .ThenInclude(s => s.Owner)
            .Include(u => u.Files)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (unit == null)
        {
            return NotFound();
        }

        return unit;
    }

    [HttpPost]
    public async Task<ActionResult<Unit>> CreateUnit(Unit unit)
    {
        // Validate unique UnitNumber per Building
        if (await _context.Units.AnyAsync(u => 
            u.BuildingId == unit.BuildingId && 
            u.UnitNumber == unit.UnitNumber))
        {
            return BadRequest(new { error = "Unit number already exists for this building" });
        }

        unit.CreatedAt = DateTime.UtcNow;
        _context.Units.Add(unit);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUnit), new { id = unit.Id }, unit);
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<IEnumerable<Unit>>> CreateUnitsInBulk([FromBody] BulkUnitRequest request)
    {
        var units = new List<Unit>();
        
        for (int i = request.Start; i <= request.End; i++)
        {
            var unitNumber = $"{request.Prefix}{i}";
            
            // Check if unit already exists
            if (await _context.Units.AnyAsync(u => 
                u.BuildingId == request.BuildingId && 
                u.UnitNumber == unitNumber))
            {
                return BadRequest(new { error = $"Unit {unitNumber} already exists for this building" });
            }

            units.Add(new Unit
            {
                UnitNumber = unitNumber,
                BuildingId = request.BuildingId,
                Floor = request.Floor,
                Type = request.Type,
                Furnishing = request.Furnishing,
                Status = UnitStatus.Available,
                CreatedAt = DateTime.UtcNow
            });
        }

        _context.Units.AddRange(units);
        await _context.SaveChangesAsync();

        return Ok(units);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUnit(int id, Unit unit)
    {
        if (id != unit.Id)
        {
            return BadRequest();
        }

        // Validate unique UnitNumber per Building
        if (await _context.Units.AnyAsync(u => 
            u.BuildingId == unit.BuildingId && 
            u.UnitNumber == unit.UnitNumber && 
            u.Id != id))
        {
            return BadRequest(new { error = "Unit number already exists for this building" });
        }

        unit.UpdatedAt = DateTime.UtcNow;
        _context.Entry(unit).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await UnitExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUnit(int id)
    {
        var unit = await _context.Units.FindAsync(id);
        if (unit == null)
        {
            return NotFound();
        }

        _context.Units.Remove(unit);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<bool> UnitExists(int id)
    {
        return await _context.Units.AnyAsync(e => e.Id == id);
    }
}

public class BulkUnitRequest
{
    public int BuildingId { get; set; }
    public required string Prefix { get; set; }
    public int Start { get; set; }
    public int End { get; set; }
    public int Floor { get; set; }
    public UnitType Type { get; set; }
    public FurnishingType Furnishing { get; set; }
}
