namespace PropertyManagement.Models;

public enum PropertyType
{
    Residential,
    Commercial,
    Industrial,
    Mixed
}

public enum UnitType
{
    Studio,
    OneBedroom,
    TwoBedroom,
    ThreeBedroom,
    FourBedroom,
    Penthouse,
    Office,
    Retail,
    Warehouse,
    Other
}

public enum FurnishingType
{
    Unfurnished,
    SemiFurnished,
    FullyFurnished
}

public enum UnitStatus
{
    Available,
    Occupied,
    UnderMaintenance,
    Reserved
}

public enum FileType
{
    Photo,
    Agreement,
    Other
}
