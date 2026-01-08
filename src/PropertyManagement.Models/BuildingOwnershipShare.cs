namespace PropertyManagement.Models;

public class BuildingOwnershipShare
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;
    public int OwnerId { get; set; }
    public Owner Owner { get; set; } = null!;
    public decimal SharePercent { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
