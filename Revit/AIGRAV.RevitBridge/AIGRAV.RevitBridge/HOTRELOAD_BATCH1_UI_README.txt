AIGRAV Hot-Reload Batch 1 + Ribbon ON/OFF UI

Changes:
- AIGRAV initializes at Revit startup but starts OFF.
- No Welcome window appears at Revit startup.
- Ribbon AIGRAV button toggles AIGRAV ON/OFF.
- AIGRAV HTTP bridge starts only when AIGRAV is ON.
- Turning AIGRAV OFF stops the HTTP bridge and closes the window.
- Closing the AIGRAV window does not turn AIGRAV OFF.
- Ribbon uses dedicated 32px/16px AIGRAV logo resources.
- Hot-reload foundation files from Batch 1 are included.

Do not modify AIGRAVToolExecutor.cs in this batch.
