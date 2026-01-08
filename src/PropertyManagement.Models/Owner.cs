namespace PropertyManagement.Models;

public class Owner
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? Phone { get; set; }
    public required string OrganizationId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<BuildingOwnershipShare> BuildingShares { get; set; } = new List<BuildingOwnershipShare>();
    public ICollection<UnitOwnershipShare> UnitShares { get; set; } = new List<UnitOwnershipShare>();
}
