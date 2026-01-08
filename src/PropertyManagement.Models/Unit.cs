namespace PropertyManagement.Models;

public class Unit
{
    public int Id { get; set; }
    public required string UnitNumber { get; set; }
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;
    public int Floor { get; set; }
    public UnitType Type { get; set; }
    public FurnishingType Furnishing { get; set; }
    public UnitStatus Status { get; set; }
    public bool HasOwnershipOverride { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    public ICollection<UnitOwnershipShare> OwnershipShares { get; set; } = new List<UnitOwnershipShare>();
    public ICollection<UnitFile> Files { get; set; } = new List<UnitFile>();
}
