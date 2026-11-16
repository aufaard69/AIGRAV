import subprocess

tr = r'"C:\Program Files\Autodesk\Revit 2026\Revit.exe" "C:\Users\sapuser\Documents\Project1.rvt"'
cmd = ['schtasks', '/create', '/tn', 'OpenRevitInteractive', '/tr', tr, '/sc', 'once', '/st', '23:59', '/f', '/it']
res = subprocess.run(cmd, capture_output=True, text=True)
print("Create:", res.stdout, res.stderr)
