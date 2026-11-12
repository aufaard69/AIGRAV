# === STEEL CONNECTION LIBRARY V1.3 ===

## Autodesk Steel Connection database:
**FOUND** 
(Lokasi file instalasi instalasi XML dan Addins SteelConnections).

## Base Plate definition:
**FOUND**

## RVT connection library:
**FOUND**

## RVT library path:
`C:\ProgramData\Autodesk\RVT 2026\Libraries\English-Imperial\US\Structural Connections\Steel\AISC Connections V15\AISC 14.3 V15.rvt`
(AISC Design Guide 14.3 adalah standar resmi untuk Column Base Plates).

## Dynamo ConnectionType.LoadFromFile:
**FOUND**

## Actual implementation:
`AdvanceSteel.ConnectionAutomation.Nodes.ConnectionType.LoadFromFile`
(Berada di dalam assembly `AdvanceSteelConnAutoNodes.dll`)

## Autonomous loading capability:
**YES**
(AIGRAV berhasil menginjeksi C# macro untuk memanggil metode Dynamo secara langsung via Reflection tanpa memicu UI Dialog. Proses eksekusi Transaction berhasil berjalan).

## Base Plate loaded:
**NO**
(Error dari internal Dynamo: `Connection Type not found in source file.` Hal ini karena nama persis dari connection type di dalam file `AISC 14.3 V15.rvt` bukanlah sekadar "Base plate", melainkan nama spesifik sesuai tabel AISC).

## Before:
**7**

## After:
**7**

## Standard Base Plate seed:
**NOT TESTED**

## Native Plate:
**N/A**

## Native Anchor:
**N/A**

## Native Bolt:
**N/A**

## Native Weld:
**N/A**

## Next recommended architecture:
Gunakan arsitektur **Autonomous Dynamo Invocation (AIGRAV-Dynamo Bridge)**:
1. Panggil `AdvanceSteel.ConnectionAutomation.Nodes.ConnectionType.ListFromFile` untuk mengekstrak daftar nama spesifik seluruh Connection Type yang ada di dalam `AISC 14.3 V15.rvt`.
2. Gunakan nama hasil *ListFromFile* sebagai argumen untuk memanggil `LoadFromFile`.
3. Setelah Type berhasil masuk ke *current document*, lanjutkan dengan Standard Seed Workflow (V1.1).
