namespace AIGRAV.Skills.QTO.Models;

public enum QTOQuantityKind
{
    Count,
    Length,
    Area,
    Volume,
    Weight
}

public enum QTOCalibrationMode
{
    None,
    Factor,
    ReferenceWeightPerMeter
}

public sealed class QTORecord
{
    public string Lokasi { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;

    public double? LengthMm { get; set; }
    public double? AreaM2 { get; set; }
    public double? SurfaceAreaM2 { get; set; }
    public double? VolumeM3 { get; set; }
    public double Count { get; set; }

    public double? RevitWeightKg { get; set; }
    public double? CalibrationFactor { get; set; }
    public double? ReferenceWeightKgPerMeter { get; set; }
    public double? CalibratedWeightKg { get; set; }

    public string SteelMatchStatus { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Source { get; set; } = "Revit";
    public string Notes { get; set; } = string.Empty;
}

public sealed class QTOColumnDefinition
{
    public string Key { get; set; } = string.Empty;
    public string Header { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Format { get; set; } = "0.00";
    public bool Visible { get; set; } = true;
}

public sealed class QTOOutputTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<QTOColumnDefinition> Columns { get; set; } = new();
    public List<string> GroupBy { get; set; } = new();
    public bool IncludeTotal { get; set; } = true;
}

public sealed class QTORequest
{
    public string Category { get; set; } = string.Empty;
    public string Template { get; set; } = "Generic";
    public string Quantity { get; set; } = "Count";
    public string Parameter { get; set; } = string.Empty;

    public List<string> GroupBy { get; set; } = new();
    public List<string> Fields { get; set; } = new();

    public QTOCalibrationMode CalibrationMode { get; set; } = QTOCalibrationMode.None;

    public double? CalibrationFactor { get; set; }
    public double? ReferenceWeightKgPerMeter { get; set; }

    public bool IncludeElementIds { get; set; }
    public int MaxItems { get; set; } = 100;
    public string? ScheduleName { get; set; }
    public string? LokasiFilter { get; set; }
    public string? CommentsFilter { get; set; }
}

public static class QTOTemplates
{
    public static QTOOutputTemplate Generic() =>
        new()
        {
            Name = "Generic",
            Description = "Generic quantity takeoff output with Lokasi in Column A.",
            GroupBy = new() { "Lokasi", "Comments", "Type" },
            Columns = new()
            {
                new() { Key = "Lokasi", Header = "Lokasi" },
                new() { Key = "Comments", Header = "Comments" },
                new() { Key = "Type", Header = "Type" },
                new() { Key = "Count", Header = "Qty", Format = "0" },
                new() { Key = "LengthMm", Header = "Length (mm)", Unit = "mm" },
                new() { Key = "AreaM2", Header = "Area (m²)", Unit = "m²" },
                new() { Key = "VolumeM3", Header = "Volume (m³)", Unit = "m³" }
            }
        };

    public static QTOOutputTemplate StructuralSteel() =>
        new()
        {
            Name = "StructuralSteel",
            Description = "Structural steel QTO using Lokasi, Comments, Type/Profile, Length, Qty, Calibrated Weight, and Painting Surface Area.",
            GroupBy = new() { "Lokasi", "Comments", "Profile" },
            Columns = new()
            {
                new() { Key = "Lokasi", Header = "Lokasi" },
                new() { Key = "Comments", Header = "Comments" },
                new() { Key = "Profile", Header = "Type / Profil" },
                new() { Key = "LengthMm", Header = "Length (mm)", Unit = "mm" },
                new() { Key = "Count", Header = "Qty", Format = "0" },
                new() { Key = "CalibratedWeightKg", Header = "Berat (kg)", Unit = "kg" },
                new() { Key = "SurfaceAreaM2", Header = "Luas Cat (m²)", Unit = "m²" }
            }
        };

    public static QTOOutputTemplate Concrete() =>
        new()
        {
            Name = "Concrete",
            Description = "Concrete QTO based on Lokasi, Comments, Type, Qty, and Volume (no steel weight).",
            GroupBy = new() { "Lokasi", "Comments", "Type" },
            Columns = new()
            {
                new() { Key = "Lokasi", Header = "Lokasi" },
                new() { Key = "Comments", Header = "Comments" },
                new() { Key = "Type", Header = "Type" },
                new() { Key = "Count", Header = "Qty", Format = "0" },
                new() { Key = "VolumeM3", Header = "Volume (m³)", Unit = "m³" }
            }
        };

    public static QTOOutputTemplate FinishingArea() =>
        new()
        {
            Name = "FinishingArea",
            Description = "Area-based QTO for architectural/finishing elements.",
            GroupBy = new() { "Lokasi", "Comments", "Type" },
            Columns = new()
            {
                new() { Key = "Lokasi", Header = "Lokasi" },
                new() { Key = "Comments", Header = "Comments" },
                new() { Key = "Type", Header = "Type" },
                new() { Key = "AreaM2", Header = "Area (m²)", Unit = "m²" }
            }
        };

    public static QTOOutputTemplate Resolve(string? name)
    {
        if (string.Equals(name, "StructuralSteel", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Steel", StringComparison.OrdinalIgnoreCase))
            return StructuralSteel();

        if (string.Equals(name, "Concrete", StringComparison.OrdinalIgnoreCase))
            return Concrete();

        if (string.Equals(name, "FinishingArea", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Area", StringComparison.OrdinalIgnoreCase))
            return FinishingArea();

        return Generic();
    }
}
