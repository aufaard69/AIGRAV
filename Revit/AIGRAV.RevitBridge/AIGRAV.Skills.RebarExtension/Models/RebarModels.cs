using System;
using System.Collections.Generic;

namespace AIGRAV.Skills.RebarExtension.Models;

public enum StructuralHostCategory
{
    Unknown = 0,
    Beam = 1,
    Column = 2,
    Foundation = 3,
    Floor = 4
}

public enum LapMode
{
    D40 = 0,
    D50 = 1,
    D60 = 2,
    CustomMm = 3,
    Coupler = 4
}

public enum BondCondition
{
    Good = 0,
    Poor = 1
}

public enum LengthRoundingMode
{
    Nearest1Mm = 1,
    Nearest25Mm = 25,
    Nearest50Mm = 50
}

public enum CutoffExtensionMode
{
    SniMaxDOr12D = 0,
    L20D = 1,
    Zero = 2
}

public enum ColumnSpliceStrategy
{
    AutoFollowModelCut = 0,
    ForceSplicePerLevel = 1,
    OptimizeFull12m = 2
}

public enum EndHookDirection
{
    None = 0,
    Bend90Inward = 1,
    Bend90Outward = 2,
    UHairpin = 3,
    Straight = 4
}

public enum FloorTopReinforcementMode
{
    SupportStripL4 = 0,
    FullDoubleMesh = 1
}

public enum FloorMeshLayoutMode
{
    Segmented12mMax = 0,
    FullExtent = 1
}

public enum EdgeTerminationMode
{
    Bend90 = 0,
    UBarLoop = 1,
    Straight = 2
}

public enum MarkScope
{
    PerHost = 0,
    PerLevel = 1,
    WholeProject = 2
}

public enum SplitMethod
{
    MaxStock = 0,
    Staggered = 1,
    CustomZones = 2
}

public enum StirrupZoneMode
{
    QuarterSpan_L4_L2_L4 = 0,
    SniSrpmkMax2hOrL4 = 1
}

public sealed record Point3DData(double X, double Y, double Z);

public sealed record RebarGlobalSettings
{
    public double MaxStockLengthMm { get; set; } = 12000.0;
    public LapMode LapMode { get; set; } = LapMode.D40;
    public double CustomLapMm { get; set; } = 800.0;
    public BondCondition BondCondition { get; set; } = BondCondition.Good;
    public LengthRoundingMode RoundingMode { get; set; } = LengthRoundingMode.Nearest25Mm;
    public bool StaggerSplices { get; set; } = true;
    public double StaggerShiftFactor { get; set; } = 1.3;
    public bool MomentSafeSplit { get; set; } = true;
    public bool UseRebarConstraints { get; set; } = true;
    public bool SetSolidInFine3D { get; set; } = true;
    public bool ApplyColorByDiameter { get; set; } = true;
    public double DefaultCoverMm { get; set; } = 40.0;
    public string SteelGrade { get; set; } = "BJTS 420B";
    public MarkScope MarkScope { get; set; } = MarkScope.WholeProject;
    public bool MarkMatchDiameter { get; set; } = true;
    public bool MarkMatchShapeCode { get; set; } = true;
    public bool MarkMatchSegmentDims { get; set; } = true;
    public bool MarkMatchHooks { get; set; } = true;
    public string DefaultLokasi { get; set; } = string.Empty;
}

public sealed record BeamConfig
{
    public double CoverMm { get; set; } = 40.0;
    public bool ManualSizeOverride { get; set; } = false;
    public double OverrideWidthMm { get; set; } = 250.0;
    public double OverrideDepthMm { get; set; } = 500.0;
    public double OverrideLengthMm { get; set; } = 6000.0;

    public int TopBarDiameterMm { get; set; } = 16;
    public int TopBarCount { get; set; } = 3;
    public int BottomBarDiameterMm { get; set; } = 16;
    public int BottomBarCount { get; set; } = 3;

    public EndHookDirection ExteriorJointHook { get; set; } = EndHookDirection.Bend90Inward;
    public string JointHookLegFormula { get; set; } = "h-2c"; // "h-2c" | "12D" | "15D"

    public bool EnableExtraTopSupport { get; set; } = true;
    public int ExtraTopSupportCount { get; set; } = 2;
    public int ExtraTopSupportDiaMm { get; set; } = 16;

    public bool EnableExtraBottomMidspan { get; set; } = true;
    public int ExtraBottomMidspanCount { get; set; } = 2;
    public int ExtraBottomMidspanDiaMm { get; set; } = 16;

    public CutoffExtensionMode CutoffMode { get; set; } = CutoffExtensionMode.SniMaxDOr12D;

    public bool EnableSkinBars { get; set; } = false;
    public int SkinBarDiaMm { get; set; } = 12;
    public int SkinBarPairs { get; set; } = 1;

    public int StirrupDiaMm { get; set; } = 10;
    public double StirrupSupportSpacingMm { get; set; } = 100.0;
    public double StirrupMidspanSpacingMm { get; set; } = 150.0;
    public double StirrupSupportZoneRatio { get; set; } = 0.25; // 1/4 bentang tumpuan, 1/2 bentang lapangan
    public StirrupZoneMode StirrupZoneMode { get; set; } = StirrupZoneMode.QuarterSpan_L4_L2_L4;
    public int StirrupLegs { get; set; } = 2;
    public bool Use135SeismicHook { get; set; } = true;
}

public sealed record ColumnConfig
{
    public double CoverMm { get; set; } = 40.0;
    public bool ManualSizeOverride { get; set; } = false;
    public double OverrideWidthMm { get; set; } = 400.0;
    public double OverrideDepthMm { get; set; } = 400.0;
    public double OverrideHeightMm { get; set; } = 4000.0;

    public int MainBarDiaMm { get; set; } = 19;
    public int TotalMainBars { get; set; } = 8; // Must be >= 4, preferably even perimeter count
    public int TieDiaMm { get; set; } = 10;
    public double TieLoSpacingMm { get; set; } = 100.0;
    public double TieMidSpacingMm { get; set; } = 150.0;
    public double FoundationTieSpacingMm { get; set; } = 150.0;
    public int TieLegs { get; set; } = 4; // 2 (outer only) | 4 (outer + diamond/cross tie)

    public ColumnSpliceStrategy SpliceStrategy { get; set; } = ColumnSpliceStrategy.AutoFollowModelCut;
    public EndHookDirection BaseFootingBend { get; set; } = EndHookDirection.Bend90Outward;
    public double BaseFootingBendLegMm { get; set; } = 300.0; // max(12D, 250mm)
    public double FootingEmbedCoverClearanceMm { get; set; } = 100.0; // sits atop bottom pad mat
    public EndHookDirection RoofTopHook { get; set; } = EndHookDirection.Bend90Inward;
    public bool EnableCrankAtOffset { get; set; } = true; // 1:6 crank when column reduces at upper level
    public bool IncludeFoundationConfiningTies { get; set; } = true;
}

public sealed record FoundationConfig
{
    public double CoverMm { get; set; } = 75.0; // SNI soil exposure min 75 mm
    public bool ManualSizeOverride { get; set; } = false;
    public double OverrideLengthXMm { get; set; } = 2000.0;
    public double OverrideWidthYMm { get; set; } = 2000.0;
    public double OverrideThicknessMm { get; set; } = 500.0;

    public int BottomXDiaMm { get; set; } = 16;
    public double BottomXSpacingMm { get; set; } = 150.0;
    public int BottomYDiaMm { get; set; } = 16;
    public double BottomYSpacingMm { get; set; } = 150.0;

    public bool EnableTopMesh { get; set; } = true;
    public int TopXDiaMm { get; set; } = 13;
    public double TopXSpacingMm { get; set; } = 200.0;
    public int TopYDiaMm { get; set; } = 13;
    public double TopYSpacingMm { get; set; } = 200.0;

    public EdgeTerminationMode EdgeTermination { get; set; } = EdgeTerminationMode.Bend90;
    public double HookLegMm { get; set; } = 250.0;
    public bool IncludePerimeterBinder { get; set; } = false;
    public int PerimeterBinderDiaMm { get; set; } = 13;
    // IMPORTANT: Zero column starter dowels inside Foundation per SOFiSTiK & anti-double-counting rule!
}

public sealed record FloorConfig
{
    public double CoverMm { get; set; } = 20.0;
    public FloorMeshLayoutMode MeshLayoutMode { get; set; } = FloorMeshLayoutMode.Segmented12mMax;
    public FloorTopReinforcementMode TopMode { get; set; } = FloorTopReinforcementMode.SupportStripL4;

    public int BottomXDiaMm { get; set; } = 10;
    public double BottomXSpacingMm { get; set; } = 150.0;
    public int BottomYDiaMm { get; set; } = 10;
    public double BottomYSpacingMm { get; set; } = 150.0;

    public int TopXDiaMm { get; set; } = 10;
    public double TopXSpacingMm { get; set; } = 150.0;
    public int TopYDiaMm { get; set; } = 10;
    public double TopYSpacingMm { get; set; } = 150.0;

    public bool EdgeHook90 { get; set; } = true;
    public bool TrimAtVoidOpenings { get; set; } = true;
    public bool AddOpeningDiagonalTrimmers { get; set; } = true;
    public int OpeningTrimmerDiaMm { get; set; } = 13;
    public int OpeningTrimmerBarsPerCorner { get; set; } = 2;
}

public sealed record RebarPresetProfile
{
    public string Name { get; set; } = "SNI 2847-2019 (SRPMK)";
    public string Description { get; set; } = "Standar bangunan daktail penuh (Baja BJTS 420B, selimut standar, 135 deg kait gempa).";
    public RebarGlobalSettings Global { get; set; } = new();
    public BeamConfig Beam { get; set; } = new();
    public ColumnConfig Column { get; set; } = new();
    public FoundationConfig Foundation { get; set; } = new();
    public FloorConfig Floor { get; set; } = new();
}

public sealed record RebarGenerationRequest
{
    public List<long> SelectedHostElementIds { get; set; } = new();
    public RebarGlobalSettings Global { get; set; } = new();
    public BeamConfig Beam { get; set; } = new();
    public ColumnConfig Column { get; set; } = new();
    public FoundationConfig Foundation { get; set; } = new();
    public FloorConfig Floor { get; set; } = new();
}

public sealed class SelectedHostSummary
{
    public long ElementId { get; set; }
    public StructuralHostCategory Category { get; set; }
    public string CategoryDisplay { get; set; } = string.Empty;
    public string FamilyAndType { get; set; } = string.Empty;
    public string HostMark { get; set; } = string.Empty;
    public string LevelName { get; set; } = string.Empty;
    public bool IsIncluded { get; set; } = true;
    public double SpanOrHeightMm { get; set; }
    public double WidthMm { get; set; }
    public double DepthOrThicknessMm { get; set; }
    public double ConcreteVolumeM3 { get; set; }
    public int ExistingRebarCount { get; set; }
    public string ExistingAigravTag { get; set; } = string.Empty;
    public string HostGeometryHash { get; set; } = string.Empty;
    public bool IsModifiedSinceGenerate { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public sealed class ConcreteMeshPreviewDto
{
    public long ElementId { get; set; }
    public StructuralHostCategory Category { get; set; }
    public string Label { get; set; } = string.Empty;
    public List<Point3DData> VerticesMm { get; set; } = new();
    public List<int> TriangleIndices { get; set; } = new();
    public List<List<Point3DData>> WireframeLoopsMm { get; set; } = new();
    public Point3DData MinMm { get; set; } = new(0, 0, 0);
    public Point3DData MaxMm { get; set; } = new(0, 0, 0);
}

public sealed class RebarBarPreviewDto
{
    public long HostElementId { get; set; }
    public string GroupTag { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ShapeCode { get; set; } = "00";
    public int DiameterMm { get; set; }
    public string ColorHex { get; set; } = "#38BDF8";
    public bool IsStirrup { get; set; }
    public bool IsSpliceOverlap { get; set; }
    public List<List<Point3DData>> PolylinesMm { get; set; } = new();
}

public sealed class RebarZoneLabelDto
{
    public string Text { get; set; } = string.Empty;
    public double XMm { get; set; }
    public double YMm { get; set; }
    public double ZMm { get; set; }
    public string AccentHex { get; set; } = "#54A8FF";
}

public sealed class BbsPreviewRowDto
{
    public string Mark { get; set; } = string.Empty;
    public string HostTag { get; set; } = string.Empty;
    public string HostCategory { get; set; } = string.Empty;
    public string Lokasi { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string ShapeCode { get; set; } = "00";
    public int DiameterMm { get; set; }
    public double CutLengthMm { get; set; }
    public int Qty { get; set; }
    public double TotalLengthM => Math.Round(CutLengthMm / 1000.0 * Qty, 2);
    public double UnitWeightKgPerM => Math.Round(DiameterMm * DiameterMm * 0.0061654, 3);
    public double TotalWeightKg => Math.Round(TotalLengthM * UnitWeightKgPerM, 2);
    public string Notes { get; set; } = string.Empty;
}

public sealed class RebarPreviewPackage
{
    public List<SelectedHostSummary> Hosts { get; set; } = new();
    public List<ConcreteMeshPreviewDto> ConcreteMeshes { get; set; } = new();
    public List<RebarBarPreviewDto> RebarBars { get; set; } = new();
    public List<RebarZoneLabelDto> ZoneLabels { get; set; } = new();
    public List<BbsPreviewRowDto> BbsRows { get; set; } = new();
    public Point3DData SceneCenterMm { get; set; } = new(0, 0, 0);
    public double SceneRadiusMm { get; set; } = 3000.0;
    public double TotalConcreteVolumeM3 { get; set; }
    public double TotalSteelWeightKg { get; set; }
    public double SteelDensityKgPerM3 => TotalConcreteVolumeM3 > 1e-6 ? Math.Round(TotalSteelWeightKg / TotalConcreteVolumeM3, 1) : 0.0;
    public double MaxSingleBarLengthMm { get; set; }
    public int TotalOverlapCount { get; set; }
    public int TotalBarPieces { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public sealed class RebarExecutionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<long> CreatedRebarIds { get; set; } = new();
    public List<long> DeletedRebarIds { get; set; } = new();
    public int TotalCreatedSets { get; set; }
    public int TotalBarPieces { get; set; }
    public double TotalWeightKg { get; set; }
    public List<string> FailedItems { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class RebarBrowserNodeDto
{
    public string LevelName { get; set; } = "Unassigned";
    public long HostElementId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public StructuralHostCategory Category { get; set; }
    public string RebarTag { get; set; } = string.Empty;
    public string MarkSummary { get; set; } = string.Empty;
    public int BarCount { get; set; }
    public double WeightKg { get; set; }
    public bool IsHostModified { get; set; }
}
