namespace PropertyManagement.Models;

public class Building
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string OrganizationId { get; set; }
    public PropertyType PropertyType { get; set; }
    public Address Address { get; set; } = new();
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
    public ICollection<BuildingOwnershipShare> OwnershipShares { get; set; } = new List<BuildingOwnershipShare>();
    public ICollection<BuildingFile> Files { get; set; } = new List<BuildingFile>();
}
