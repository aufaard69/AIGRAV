# === CONNECTION LIBRARY V1.2 ===

## Library Discovery:
**PASS**
**Installation:** `C:\ProgramData\Autodesk\Revit Steel Connections 2026\`
**Candidate directories:** `\en-US\` (dan folder bahasa lainnya)
**Candidate files:**
- `SteelConnectionsData.xml`
- `SteelConnections_en-US.zip`

## Base Plate Resource:
**FOUND**
- **Filename:** `SteelConnectionsData.xml` (berisi definisi XML `<Command>baseplate</Command>` dan metadata lainnya).
- **Path:** `C:\ProgramData\Autodesk\Revit Steel Connections 2026\en-US\SteelConnectionsData.xml`
- **Extension:** `.xml`

## Loading Mechanism:
**LIBRARY LOADING REQUIRES REVIT UI**

1. **UI command yang digunakan:** *Structural Connection Settings*
2. **Menu/location:** Tab *Steel* -> Panel *Connection* -> Klik ikon panah diagonal kecil di pojok kanan bawah panel (Connection Settings). Kemudian pindahkan "Base plate" dari *Available Connections* ke *Loaded Connections*.
3. **Apakah Revit API menyediakan equivalent:** **TIDAK**. Revit 2026 API tidak memiliki fungsi publik (seperti `Document.LoadFamily`) untuk memuat *Structural Connection Type* yang berasal dari database Advance Steel. Method `AddConnectionToMap` yang pernah ada di versi lama tidak ditemukan pada kelas `StructuralConnectionSettings` saat ini.
4. **Apakah operation dapat dipanggil melalui ExternalEvent:** **YA**, namun hanya sebatas membuka UI Dialog menggunakan `PostCommand` (`PostableCommand.StructuralConnectionSettings`). Pemanggilan ini akan nge-blok *thread* dan membutuhkan interaksi klik manual dari User.
5. **Apakah operation dapat direplikasi melalui API:** **TIDAK** secara standar. Dynamo memang memiliki *node* `ConnectionType.LoadOnDemand`, namun itu adalah wrapper yang memanggil fungsi internal unmanaged dari `SteelConnections` addin. Tanpa injeksi eksternal atau manipulasi unmanaged library, API publik C# tidak dapat melakukannya secara otonom.

## Before:
**7 types**

## After:
**7 types** (Tidak ada eksekusi loading karena membutuhkan UI)

## Base Plate:
**NOT FOUND** (Belum dimuat ke dalam *document*)

## Standard Seed:
**FAIL** (Blocked by unloaded library)

## Native Plate:
**N/A**

## Native Anchor:
**N/A**

## Native Bolt:
**N/A**

## Native Weld:
**N/A**

## Custom Authoring:
**NOT TESTED**
