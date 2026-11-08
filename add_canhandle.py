path = r"C:\AIGRAV\Revit\AIGRAV.RevitBridge\AIGRAV.Skills.BimAudit\BimAuditSkill.cs"
with open(path, "r", encoding="utf-8") as f:
    text = f.read()

target = 'return toolName.Equals("aigrav_tag_wf400_endplate"'
repl = '''return toolName.Equals("aigrav_create_rcwf_588", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("create_rcwf_588", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("aigrav_tag_wf400_endplate"'''

if target in text:
    text = text.replace(target, repl, 1)
    with open(path, "w", encoding="utf-8") as f:
        f.write(text)
    print("Successfully added create_rcwf_588 to CanHandle")
else:
    print("target not found")
