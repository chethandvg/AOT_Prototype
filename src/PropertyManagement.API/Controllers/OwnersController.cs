using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PropertyManagement.API.Data;
using PropertyManagement.Models;

namespace PropertyManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OwnersController : ControllerBase
{
    private readonly PropertyManagementDbContext _context;
    private readonly ILogger<OwnersController> _logger;

    public OwnersController(PropertyManagementDbContext context, ILogger<OwnersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Owner>>> GetOwners([FromQuery] string? organizationId = null)
    {
        var query = _context.Owners.AsQueryable();

        if (!string.IsNullOrEmpty(organizationId))
        {
            query = query.Where(o => o.OrganizationId == organizationId);
        }

        return await query.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Owner>> GetOwner(int id)
    {
        var owner = await _context.Owners
            .Include(o => o.BuildingShares)
                .ThenInclude(s => s.Building)
            .Include(o => o.UnitShares)
                .ThenInclude(s => s.Unit)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (owner == null)
        {
            return NotFound();
        }

        return owner;
    }

    [HttpPost]
    public async Task<ActionResult<Owner>> CreateOwner(Owner owner)
    {
        owner.CreatedAt = DateTime.UtcNow;
        _context.Owners.Add(owner);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOwner), new { id = owner.Id }, owner);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateOwner(int id, Owner owner)
    {
        if (id != owner.Id)
        {
            return BadRequest();
        }

        owner.UpdatedAt = DateTime.UtcNow;
        _context.Entry(owner).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await OwnerExists(id))
            {
                return NotFound();
            }
            throw;
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteOwner(int id)
    {
        var owner = await _context.Owners.FindAsync(id);
        if (owner == null)
        {
            return NotFound();
        }

        _context.Owners.Remove(owner);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private async Task<bool> OwnerExists(int id)
    {
        return await _context.Owners.AnyAsync(e => e.Id == id);
    }
}
