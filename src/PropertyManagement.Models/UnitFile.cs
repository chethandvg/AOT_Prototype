namespace PropertyManagement.Models;

public class UnitFile
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public Unit Unit { get; set; } = null!;
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public FileType FileType { get; set; }
    public long FileSize { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime UploadedAt { get; set; }
}
