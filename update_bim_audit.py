import os

path = r"C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
with open(path, "r", encoding="utf-8") as f:
    code = f.read()

# 1. Update TagWF400EndplateElements to call RunComprehensiveProjectAudit
target1 = """    private static string TagWF400EndplateElements(Document doc)
    {
        return InspectMoreSteelRows(doc);
    }"""
repl1 = """    private static string TagWF400EndplateElements(Document doc)
    {
        return RunComprehensiveProjectAudit(doc);
    }"""

if target1 in code:
    code = code.replace(target1, repl1, 1)
    print("Replaced TagWF400EndplateElements")
else:
    # check CRLF
    target1_crlf = target1.replace("\n", "\r\n")
    repl1_crlf = repl1.replace("\n", "\r\n")
    if target1_crlf in code:
        code = code.replace(target1_crlf, repl1_crlf, 1)
        print("Replaced TagWF400EndplateElements (CRLF)")
    else:
        print("target1 NOT found")

# 2. Optimize row loop
target2 = """                for (int r = 0; r < body.NumberOfRows; r++)
                {
                    for (int c = 0; c < body.NumberOfColumns; c++)
                    {
                        string txt = s.GetCellText(SectionType.Body, r, c).Trim();
                        if (string.IsNullOrEmpty(txt)) continue;
                        if (txt.Contains("WF") || txt.Contains("H 2") || txt.Contains("H 3") || txt.Contains("2L") || txt.Contains("CNP") || txt.Contains("PL"))
                        {
                            if (string.IsNullOrEmpty(profileText)) profileText = txt;
                        }
                        if (txt.Contains("kg") && string.IsNullOrEmpty(weightText)) weightText = txt;
                    }
                }"""

repl2 = """                for (int r = 0; r < Math.Min(body.NumberOfRows, 5); r++)
                {
                    for (int c = 0; c < body.NumberOfColumns; c++)
                    {
                        string txt = s.GetCellText(SectionType.Body, r, c).Trim();
                        if (string.IsNullOrEmpty(txt)) continue;
                        if (txt.Contains("WF") || txt.Contains("H 2") || txt.Contains("H 3") || txt.Contains("2L") || txt.Contains("CNP") || txt.Contains("PL"))
                        {
                            if (string.IsNullOrEmpty(profileText)) profileText = txt;
                        }
                        if (txt.Contains("kg") && string.IsNullOrEmpty(weightText)) weightText = txt;
                    }
                    if (!string.IsNullOrEmpty(profileText) && !string.IsNullOrEmpty(weightText)) break;
                }"""

if target2 in code:
    code = code.replace(target2, repl2, 1)
    print("Replaced loop")
else:
    target2_crlf = target2.replace("\n", "\r\n")
    repl2_crlf = repl2.replace("\n", "\r\n")
    if target2_crlf in code:
        code = code.replace(target2_crlf, repl2_crlf, 1)
        print("Replaced loop (CRLF)")
    else:
        print("target2 NOT found")

with open(path, "w", encoding="utf-8") as f:
    f.write(code)
print("Finished writing BimAuditSkill.cs")
