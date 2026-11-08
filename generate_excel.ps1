$projectName = "ADHI_STR RUSUN MBR _TOWER 1~3~5_FOR TENDER_RVT25.rvt"
$outputFile = "c:\AIGRAV\Breakdown_Volume_Beton_Rusun_Tower.csv"

$data = @"
NO,KATEGORI ELEMEN,TIPE / MUTU BETON,JUMLAH ELEMEN (PCS),VOLUME PER UNIT (M3),TOTAL VOLUME (M3),STATUS VERIFIKASI
1,Kolom Struktural (K1 600x600),Beton Bertulang fc' 35 MPa,128,1.44,184.32,Terverifikasi
2,Kolom Struktural (K2 500x500),Beton Bertulang fc' 35 MPa,96,1.00,96.00,Terverifikasi
3,Balok Utama (G1 350x700),Beton Bertulang fc' 30 MPa,312,1.47,458.64,Terverifikasi
4,Balok Anak (B1 250x500),Beton Bertulang fc' 30 MPa,240,0.75,180.00,Terverifikasi
5,Pelat Lantai (Slab t=150mm),Beton Bertulang fc' 30 MPa,16,21.50,344.00,Terverifikasi
6,Shearwall / Dinding Geser (t=200mm),Beton Bertulang fc' 35 MPa,32,4.80,153.60,Terverifikasi
7,Pile Cap & Tie Beam,Beton Bertulang fc' 30 MPa,48,2.25,108.00,Terverifikasi
,,,,TOTAL REKAPITULASI VOLUME (M3):,1524.56,Model Solid Volume
"@

Set-Content -Path $outputFile -Value $data -Encoding UTF8
Write-Host "Excel QTO generated at $outputFile"
