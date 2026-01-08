using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.API.Data;
using PropertyManagement.Models;

namespace PropertyManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BuildingsController : ControllerBase
{
    private readonly PropertyManagementDbContext _context;
    private readonly ILogger<BuildingsController> _logger;

    public BuildingsController(PropertyManagementDbContext context, ILogger<BuildingsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Building>>> GetBuildings(
        [FromQuery] string? search = null,
        [FromQuery] PropertyType? propertyType = null,
        [FromQuery] bool? includeDeleted = false)
    {
        var query = _context.Buildings
            .Include(b => b.Units)
            .Include(b => b.OwnershipShares)
            .AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(b => 
                b.Name.Contains(search) || 
                b.Code.Contains(search) || 
                b.Address.City.Contains(search));
        }

        if (propertyType.HasValue)
        {
            query = query.Where(b => b.PropertyType == propertyType.Value);
        }

        if (includeDeleted != true)
        {
            query = query.Where(b => !b.IsDeleted);
        }

        return await query.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Building>> GetBuilding(int id)
    {
        var building = await _context.Buildings
            .Include(b => b.Units)
            .Include(b => b.OwnershipShares)
                .ThenInclude(s => s.Owner)
            .Include(b => b.Files)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (building == null)
        {
            return NotFound();
        }

        return building;
    }

    [HttpPost]
    public async Task<ActionResult<Building>> CreateBuilding(Building building)
    {
        // Validate unique BuildingCode per Organization
        if (await _context.Buildings.AnyAsync(b => 
            b.OrganizationId == building.OrganizationId && 
            b.Code == building.Code))
        {
            return BadRequest(new { error = "Building code already exists for this organization" });
        }

        building.CreatedAt = DateTime.UtcNow;
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetBuilding), new { id = building.Id }, building);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBuilding(int id, Building building)
    {
        if (id != building.Id)
        {
            return BadRequest();
        }

        // Validate unique BuildingCode per Organization
        if (await _context.Buildings.AnyAsync(b => 
            b.OrganizationId == building.OrganizationId && 
            b.Code == building.Code && 
            b.Id != id))
        {
            return BadRequest(new { error = "Building code already exists for this organization" });
        }

        building.UpdatedAt = DateTime.UtcNow;
        _context.Entry(building).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await BuildingExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    [HttpPut("{id}/address")]
    public async Task<IActionResult> UpdateBuildingAddress(int id, Address address)
    {
        var building = await _context.Buildings.FindAsync(id);
        if (building == null)
        {
            return NotFound();
        }

        building.Address = address;
        building.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBuilding(int id)
    {
        var building = await _context.Buildings.FindAsync(id);
        if (building == null)
        {
            return NotFound();
        }

        building.IsDeleted = true;
        building.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<bool> BuildingExists(int id)
    {
        return await _context.Buildings.AnyAsync(e => e.Id == id);
    }
}
