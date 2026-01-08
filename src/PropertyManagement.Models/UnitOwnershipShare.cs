namespace PropertyManagement.Models;

public class UnitOwnershipShare
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public int OwnerId { get; set; }
    public Owner Owner { get; set; } = null!;
    public decimal SharePercent { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
