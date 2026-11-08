---
name: install-aigrav
description: Installs, deploys, and configures AIGRAV on this computer. Triggers when the user prompts "install aigrav", "instalkan aigrav", "setup aigrav", "pasang aigrav", or asks to install/reinstall AIGRAV and its databases.
---

# AIGRAV One-Click Installer Skill

This skill executes the automated installation and configuration of **AIGRAV** (AI-Powered Revit BIM & Structural Automation Suite).

## Trigger Scenarios
Activate this skill when the user asks:
- "install aigrav"
- "instalkan aigrav"
- "setup aigrav sekarang"
- "pasang aigrav di pc ini"
- "install aigrav yang sudah ada database"

## Execution Procedure

When triggered, follow these exact steps:

1. **Locate the Installer Script**:
   Find `Install-AIGRAV.ps1` in the current workspace or in `C:\AIGRAV\Install-AIGRAV.ps1`.

2. **Execute the PowerShell Installer**:
   Run the following command via `run_command`:
   ```powershell
   powershell.exe -ExecutionPolicy Bypass -File "C:\AIGRAV\Install-AIGRAV.ps1"
   ```
   (If running from a portable installer folder, specify the relative path to `.\Install-AIGRAV.ps1`).

3. **Verify the Installation**:
   Verify that:
   - Revit add-in `AIGRAV.addin` is deployed in `%APPDATA%\Autodesk\Revit\Addins\<RevitVersion>\AIGRAV.addin`
   - Python virtual environment is ready at `C:\AIGRAV\MCP\TestServer\venv`
   - MCP Server is configured in `%USERPROFILE%\.gemini\config\mcp_config.json`
   - Databases exist:
     - `C:\AIGRAV\KnowledgeBase\SteelConnectionDatabase\AIGRAV_SteelConnectionDatabase_v1.json`
     - `C:\AIGRAV\Revit\AIGRAV.SteelDatabase.csv`
     - `C:\AIGRAV\ConnectionLibrary\*.rfa`
   - Plugin `aigrav-bim` is installed in `%USERPROFILE%\.gemini\config\plugins\aigrav-bim`

4. **Report Back to User**:
   Provide a concise and structured Markdown summary table displaying the status of each component.
