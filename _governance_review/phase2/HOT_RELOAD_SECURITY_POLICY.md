# AIGRAV Skill Hot-Reload Security & Trust Boundary Policy

**Status:** `DRAFT — STAGING ONLY — NOT ACTIVE`  
**Keputusan Arsitektur:** `APPROVED — DESIGN DECISION (Opsi 4A)` | `NOT YET IMPLEMENTED`  
**Tujuan Dokumen:** Menetapkan batas kepercayaan (*trust boundaries*), protokol keamanan eksekusi, serta tata kelola pembaruan biner (*DLL hot-reload*) pada modul `AIGRAVSkillLoader.cs` guna mencegah eksekusi kode berbahaya di dalam ruang proses utama Autodesk Revit.

---

## 1. Analisis Arsitektur & Fakta Call Site (`AIGRAVSkillLoader.cs`)

Berdasarkan pemeriksaan kode sumber C#:
- **Mekanisme Pemuatan Saat Ini**: `AIGRAVSkillLoader.cs` memindai direktori biner, membaca *assembly* `.dll` yang mengimplementasikan *interface* skill AIGRAV, menyalin berkas ke *shadow-copy cache*, dan memuatnya secara dinamis ke dalam *AppDomain* Revit.
- **Tingkat Akses Eksekusi (*Process Privilege*)**: Kode yang dimuat berjalan di dalam konteks proses `Revit.exe` pada hak akses pengguna aktif (*Windows user privileges*). Kode ini memiliki akses penuh ke Revit API, sistem berkas lokal, soket jaringan, dan registri Windows.
- **Risiko Keamanan Nyata**: Pemuatan biner tanpa validasi integritas (*unauthenticated binary dynamic loading*) membuka celah *Arbitrary Code Execution (ACE)* dan *DLL Hijacking* jika direktori target dapat ditulis oleh pihak ketiga atau proses lokal tanpa pengawasan.

---

## 2. Kebijakan Keamanan Terpilih (Approved Opsi 4A)

Sesuai persetujuan pengguna (**Opsi 4A**), tata kelola hot-reload ditetapkan secara ketat:

```
[LINGKUNGAN PRODUKSI (PRODUCTION MODE)]
       │
       ├── DLL Hot-Reload DINONAKTIFKAN SECARA PERMANEN.
       ├── Endpoint pemanggilan dinamis (aigrav_reload_skills) diblokir (ERR_HOT_RELOAD_DISABLED_IN_PROD).
       └── Seluruh pembaruan biner DLL produksi WAJIB dilakukan melalui RESTART AUTODESK REVIT.

[LINGKUNGAN PENGEMBANGAN (DEVELOPMENT MODE)]
       │
       ├── DLL Hot-Reload DIPERBOLEHKAN secara terbatas untuk iterasi cepat developer.
       ├── Wajib Prompt Persetujuan Interaktif Pengguna (User Consent Prompt di chat UI).
       ├── Wajib Verifikasi Checksum SHA-256 sebelum pemuatan.
       └── Wajib Isolasi Direktori & Pembatasan Izin Akses (NTFS ACL).
```

---

## 3. Protokol Keamanan Mode Pengembangan (*Development Mode Controls*)

Jika hot-reload diaktifkan dalam lingkungan pengembangan:

### 3.1. Pengamanan Akses Direktori (*Directory Access Control Lists - ACLs*)
- Direktori biner skill (`C:\AIGRAV\Skills\` dan `C:\AIGRAV\Revit\`) wajib memiliki izin NTFS:
  - `BUILTIN\Administrators`: Full Control.
  - `Authenticated Users`: Read & Execute saja (Dilarang Write / Modify).

### 3.2. Verifikasi Hash & Jejak Asal (*SHA-256 Provenance Tracking*)
- Setiap pemanggilan muat ulang pada sesi developer wajib menghitung dan mencatat hash SHA-256 dari biner ke log telemetri:
  ```csharp
  using (var sha256 = SHA256.Create())
  {
      byte[] fileBytes = File.ReadAllBytes(dllPath);
      byte[] hashBytes = sha256.ComputeHash(fileBytes);
      string computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
      AIGRAVLog.Info($"[HOT-RELOAD DEV AUDIT] Assembly: {Path.GetFileName(dllPath)} | SHA-256: {computedHash}");
  }
  ```

### 3.3. Persetujuan Interaktif Pengguna (*User Approval Prompt*)
- Pemanggilan `aigrav_reload_skills` pada mode dev tidak boleh terjadi secara senyap (*silent reload*). Sistem wajib memunculkan prompt konfirmasi yang menampilkan nama berkas, path, dan nilai hash SHA-256 kepada pengguna sebelum biner dimuat ke memori aktif.
