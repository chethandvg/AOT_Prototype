namespace PropertyManagement.Models;

public class BuildingFile
{
    public int Id { get; set; }
    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;
    public required string FileName { get; set; }
    public required string FilePath { get; set; }
    public FileType FileType { get; set; }
    public long FileSize { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime UploadedAt { get; set; }
}
