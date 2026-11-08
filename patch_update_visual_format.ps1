$path = "C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
$txt = [System.IO.File]::ReadAllText($path)

$targetMethodStart = 'private static (ViewSchedule? vs, string err) CreateStructuredSchedule('
$idxStart = $txt.IndexOf($targetMethodStart)
if ($idxStart -lt 0) {
    Write-Error "Could not find CreateStructuredSchedule"
    exit 1
}

$targetBatch1Start = 'public static string ExecuteBatch1_CartonCupsAndProductionOffice('
$idxEnd = $txt.IndexOf($targetBatch1Start)
if ($idxEnd -lt 0) {
    Write-Error "Could not find ExecuteBatch1_CartonCupsAndProductionOffice"
    exit 1
}

$newCreateStructuredSchedule = @'
private static (ViewSchedule? vs, string err) CreateStructuredSchedule(
        Document doc,
        ViewSchedule? srcTemplate,
        BuiltInCategory bic,
        string newName,
        string lokName,
        string itemKind,
        string profileFilter)
    {
        try
        {
            var old = FindSched(doc, s => s.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
            if (old != null) doc.Delete(old.Id);

            ViewSchedule vs;
            if (srcTemplate != null)
            {
                ElementId newId = srcTemplate.Duplicate(ViewDuplicateOption.Duplicate);
                vs = (ViewSchedule)doc.GetElement(newId);
                vs.Name = newName;
            }
            else
            {
                ElementId catId = new ElementId(bic);
                if (!ViewSchedule.IsValidCategoryForSchedule(catId))
                {
                    return (null, $"Kategori {bic} tidak valid untuk schedule");
                }
                vs = ViewSchedule.CreateSchedule(doc, catId);
                vs.Name = newName;
            }

            var def = vs.Definition;
            def.IsItemized = false;
            def.ClearFilters();

            // 1. Pastikan Field 'Lokasi' ada, TIDAK DI-HIDE (muncul sebagai penanda), dan terfilter
            ScheduleField? fLok = null;
            for (int i = 0; i < def.GetFieldCount(); i++)
            {
                var f = def.GetField(i);
                if (f.GetName().Equals("Lokasi", StringComparison.OrdinalIgnoreCase))
                {
                    fLok = f;
                    break;
                }
            }
            if (fLok == null)
            {
                var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Lokasi", StringComparison.OrdinalIgnoreCase));
                if (sf != null)
                {
                    fLok = def.AddField(sf);
                }
            }
            if (fLok != null)
            {
                fLok.IsHidden = false; // Munculkan kolom Lokasi sebagai penanda
                fLok.ColumnHeading = "Lokasi";
                def.AddFilter(new ScheduleFilter(fLok.FieldId, ScheduleFilterType.Equal, lokName));
            }

            // 2. Filter Spesifik Profil / Sambungan
            if (!string.IsNullOrWhiteSpace(profileFilter))
            {
                if (itemKind == "Kolom" || itemKind == "Balok")
                {
                    ScheduleField? fType = null;
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().Equals("Type", StringComparison.OrdinalIgnoreCase) || f.GetName().Equals("Family and Type", StringComparison.OrdinalIgnoreCase))
                        {
                            fType = f;
                            break;
                        }
                    }
                    if (fType == null)
                    {
                        var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Type", StringComparison.OrdinalIgnoreCase));
                        if (sf != null) fType = def.AddField(sf);
                    }
                    if (fType != null)
                    {
                        def.AddFilter(new ScheduleFilter(fType.FieldId, ScheduleFilterType.Contains, profileFilter));
                    }
                }
                else if (itemKind == "Connection")
                {
                    // A. Tipe Sambungan
                    ScheduleField? fTypeConn = null;
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().IndexOf("Type of Connection", StringComparison.OrdinalIgnoreCase) >= 0 || f.GetName().IndexOf("Tipe Sambungan", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            fTypeConn = f;
                            break;
                        }
                    }
                    if (fTypeConn == null)
                    {
                        var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).IndexOf("Type of Connection", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (sf != null) fTypeConn = def.AddField(sf);
                    }
                    if (fTypeConn != null)
                    {
                        fTypeConn.ColumnHeading = "Tipe Sambungan";
                        fTypeConn.IsHidden = false;
                        def.AddFilter(new ScheduleFilter(fTypeConn.FieldId, ScheduleFilterType.Contains, profileFilter));
                    }

                    // B. Tipe Detail Sambungan (Family and Type / Type)
                    ScheduleField? fTypeDetail = null;
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().Equals("Type", StringComparison.OrdinalIgnoreCase) || f.GetName().Equals("Family and Type", StringComparison.OrdinalIgnoreCase))
                        {
                            fTypeDetail = f;
                            break;
                        }
                    }
                    if (fTypeDetail == null)
                    {
                        var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Family and Type", StringComparison.OrdinalIgnoreCase) || x.GetName(doc).Equals("Type", StringComparison.OrdinalIgnoreCase));
                        if (sf != null) fTypeDetail = def.AddField(sf);
                    }
                    if (fTypeDetail != null)
                    {
                        fTypeDetail.ColumnHeading = "Tipe Detail Sambungan";
                        fTypeDetail.IsHidden = false;
                    }

                    // C. Comments
                    ScheduleField? fComm = null;
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase))
                        {
                            fComm = f;
                            break;
                        }
                    }
                    if (fComm == null)
                    {
                        var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Comments", StringComparison.OrdinalIgnoreCase));
                        if (sf != null) fComm = def.AddField(sf);
                    }
                    if (fComm != null)
                    {
                        fComm.ColumnHeading = "Comments";
                        fComm.IsHidden = false;
                    }

                    // D. Count (Qty)
                    ScheduleField? fCount = null;
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().Equals("Count", StringComparison.OrdinalIgnoreCase) || f.GetName().Equals("Qty", StringComparison.OrdinalIgnoreCase))
                        {
                            fCount = f;
                            break;
                        }
                    }
                    if (fCount == null)
                    {
                        var sf = def.GetSchedulableFields().FirstOrDefault(x => x.FieldType == ScheduleFieldType.Count || x.GetName(doc).Equals("Count", StringComparison.OrdinalIgnoreCase));
                        if (sf != null) fCount = def.AddField(sf);
                    }
                    if (fCount != null)
                    {
                        fCount.ColumnHeading = "Qty";
                        fCount.IsHidden = false;
                        fCount.DisplayType = ScheduleFieldDisplayType.Totals;
                    }

                    // E. Sorting / Grouping & Grand Total
                    while (def.GetSortGroupFieldCount() > 0) def.RemoveSortGroupField(0);
                    if (fTypeConn != null)
                    {
                        var sg1 = new ScheduleSortGroupField(fTypeConn.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = true };
                        def.AddSortGroupField(sg1);
                    }
                    if (fTypeDetail != null)
                    {
                        var sg2 = new ScheduleSortGroupField(fTypeDetail.FieldId, ScheduleSortOrder.Ascending) { ShowHeader = false, ShowFooter = false };
                        def.AddSortGroupField(sg2);
                    }

                    def.ShowGrandTotal = true;
                    def.ShowGrandTotalCount = true;
                    def.ShowGrandTotalTitle = true;
                    def.GrandTotalTitle = "Total";
                }
                else if (itemKind == "Baseplate")
                {
                    ScheduleField? fComm = null;
                    for (int i = 0; i < def.GetFieldCount(); i++)
                    {
                        var f = def.GetField(i);
                        if (f.GetName().Equals("Comments", StringComparison.OrdinalIgnoreCase))
                        {
                            fComm = f;
                            break;
                        }
                    }
                    if (fComm == null)
                    {
                        var sf = def.GetSchedulableFields().FirstOrDefault(x => x.GetName(doc).Equals("Comments", StringComparison.OrdinalIgnoreCase));
                        if (sf != null) fComm = def.AddField(sf);
                    }
                    if (fComm != null)
                    {
                        def.AddFilter(new ScheduleFilter(fComm.FieldId, ScheduleFilterType.Contains, profileFilter));
                    }
                }
            }

            return (vs, "");
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    
'@

$newTxt = $txt.Substring(0, $idxStart) + $newCreateStructuredSchedule + $txt.Substring($idxEnd)

# Update QaAuditAllLocationSchedules to also audit Table Format
$oldQaAuditHeader = 'sb.AppendLine(string.Format("{0,-65} | {1,-10} | Qty:{2,-4} Rows:{3,-3} | {4}", s.Name, catName, elemCount, rowCount, filterDesc));'
$newQaAuditHeader = @'
            var td = s.GetTableData();
            var sd = td?.GetSectionData(SectionType.Body);
            int colCount = sd?.NumberOfColumns ?? 0;
            bool isLokasiVisible = false;
            for (int i = 0; i < def.GetFieldCount(); i++)
            {
                var f = def.GetField(i);
                if (f.GetName().Equals("Lokasi", StringComparison.OrdinalIgnoreCase) && !f.IsHidden)
                {
                    isLokasiVisible = true;
                    break;
                }
            }
            string formatStatus = (colCount >= 4 && isLokasiVisible) ? "FORMAT: OK" : $"FORMAT: DEFECT (Cols:{colCount}, LokVis:{isLokasiVisible})";
            sb.AppendLine(string.Format("{0,-65} | {1,-10} | Qty:{2,-4} Rows:{3,-3} Cols:{4,-2} LokVis:{5,-5} | {6}", s.Name, catName, elemCount, rowCount, colCount, isLokasiVisible, filterDesc));
'@

if ($newTxt.Contains($oldQaAuditHeader)) {
    $newTxt = $newTxt.Replace($oldQaAuditHeader, $newQaAuditHeader)
}

[System.IO.File]::WriteAllText($path, $newTxt, [System.Text.Encoding]::UTF8)
Write-Output "Successfully patched CreateStructuredSchedule & QaAuditAllLocationSchedules in BimAuditSkill.cs"
