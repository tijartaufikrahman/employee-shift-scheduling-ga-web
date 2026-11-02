<p align="center">
  <img src="https://raw.githubusercontent.com/devicons/devicon/master/icons/dot-net/dot-net-original.svg" alt="ASP.NET Core MVC" width="70" height="70" />
</p>

<h1 align="center">Sistem Penjadwalan Shift Karyawan - Algoritma Genetika (SisJad)</h1>

<p align="center">
  <b>Enterprise-grade ASP.NET Core 8 MVC web application for automated employee shift scheduling using Genetic Algorithm (GA), featuring operational constraint rules, leave & sick permit workflows, PDF schedule export with QR code verification, and role-based access control.</b>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet&logoColor=white" alt=".NET 8" />
  <img src="https://img.shields.io/badge/ASP.NET_Core-MVC-512BD4?style=flat&logo=dotnet&logoColor=white" alt="ASP.NET Core MVC" />
  <img src="https://img.shields.io/badge/Algorithm-Genetic_Algorithm-0d9488?style=flat&logoColor=white" alt="Genetic Algorithm" />
  <img src="https://img.shields.io/badge/Database-SQL_Server-CC292B?style=flat&logo=microsoftsqlserver&logoColor=white" alt="SQL Server" />
  <img src="https://img.shields.io/badge/Bootstrap-5.3-7952B3?style=flat&logo=bootstrap&logoColor=white" alt="Bootstrap 5" />
  <img src="https://img.shields.io/badge/Document-QuestPDF-2024.3-blue?style=flat" alt="QuestPDF" />
  <img src="https://img.shields.io/badge/Security-BCrypt-critical?style=flat" alt="BCrypt" />
</p>

---

## 📌 Overview

**SisJad (Sistem Penjadwalan Shift Karyawan)** adalah platform berbasis web enterprise yang dirancang untuk mengatasi kompleksitas penjadwalan rotasi kerja (shift) karyawan secara otomatis, adil, dan optimal. 

Penyusunan jadwal kerja manual kerap memakan waktu berhari-hari dan rawan kesalahan manusia (*human error*), seperti penumpukan hari libur di akhir pekan, kekurangan kuota staf operasional, pelanggaran batas kerja harian, serta bentrok dengan pengajuan cuti yang telah disetujui. 

Sistem ini mengimplementasikan **Algoritma Genetika (Genetic Algorithm)** sebagai mesin optimasi (*heuristic search engine*) berkemampuan tinggi yang mampu memproses ratusan kombinasi permutasi penugasan shift dan menghasilkan jadwal terbaik dalam hitungan detik dengan mematuhi seluruh batasan (*hard & soft constraints*) operasional perusahaan.

---

## 🧬 Genetic Algorithm Engine Architecture

Mesin optimasi diimplementasikan pada [`ScheduleService.cs`](file:///C:/Programing/Github%20Project/SistemPenjadwalan/SistemPenjadwalanShiftAlgoritmaGenetika/SistemPenjadwalanShiftGA/Services/ScheduleService.cs) dengan mekanisme:

### 1. Representasi Kromosom (*Chromosome Representation*)
Setiap individu kromosom merepresentasikan satu konfigurasi jadwal lengkap:
$$\text{Gene} = (\text{UserId}, \text{Date}) \rightarrow \text{ShiftTypeId} \lor \text{NULL (Libur/Off)}$$

### 2. Batasan Operasional (*Operational Constraints*)
- **Shift Quota Compliance:** Setiap jenis shift (Pagi, Siang, Malam) wajib memenuhi target kuota minimum kebutuhan staf per hari.
- **Strict Night-to-Morning Prohibition:** Melarang perputaran shift dari shift Malam ke shift Pagi pada keesokan harinya untuk menjamin waktu istirahat karyawan yang sehat dan manusiawi.
- **Seniority Distribution:** Memastikan ketersediaan minimal staf berstatus **Senior** pada tiap shift agar operasional berjalan stabil dan terkendali.
- **Consecutive Working Days Limit:** Mencegah karyawan ditugaskan bekerja melebihi batas hari berturut-turut yang telah dikonfigurasi (misal maks. 5 atau 6 hari).
- **Weekly Off-Day Guarantees:** Menjamin alokasi hari libur mingguan terdistribusi secara proporsional dan merata tanpa penumpukan libur massal.
- **Leave & Sick Synchronization:** Mengintegrasikan data pengajuan cuti dan sakit yang sah secara otomatis ke dalam pembentukan jadwal kerja sehingga karyawan yang bersangkutan tidak ditugaskan.
- **Post-Evolution Equalization:** Mengoptimalkan distribusi total jam/shift antar karyawan agar beban kerja terbagi seadil mungkin (*workload fairness*).

### 3. Operator Genetika (*Genetic Operators*)
- **Inisialisasi Populasi:** Membangun populasi awal acak yang sudah memenuhi batasan dasar absensi dan cuti.
- **Fitness Evaluation:** Menghitung skor penalti (*penalty cost*) berdasarkan pelanggaran batasan kuota, rotasi malam-ke-pagi, jatah libur, dan ketersediaan staf senior.
- **Tournament Selection:** Memilih pasangan kromosom induk terbaik melalui metode turnamen.
- **Crossover (Pindah Silang):** Mengombinasikan gen-gen induk untuk mewariskan pola penugasan shift terbaik ke generasi anak.
- **Mutation (Mutasi Terarah):** Melakukan pertukaran (*swap*) atau penyesuaian shift acak dengan probabilitas tertentu guna mencegah jebakan optimum lokal (*local optima*).
- **Elitisme:** Menjaga kromosom dengan nilai fitness terbaik langsung lolos ke generasi berikutnya tanpa degradasi.

---

## 🚀 Key Features

### 📅 Modul Penjadwalan Cerdas (*Smart Scheduling Suite*)
- **Generator Jadwal Multi-Minggu:**
  - Konfigurasi tanggal mulai, durasi jadwal (1, 2, 3, hingga 4+ minggu).
  - Parameter fleksibel: Maksimal kerja berturut-turut, batas jatah libur mingguan, proteksi rotasi shift malam-pagi, dan kuota minimal karyawan senior.
- **Interactive Live Preview Matrix:**
  - Tampilan visual matriks jadwal sebelum disimpan permanen ke basis data.
  - Ringkasan statistik per karyawan (total shift pagi, siang, malam, libur, dan cuti).
- **Ekspor Dokumen Resmi PDF Berstandar Tinggi:**
  - Integrasi dengan **QuestPDF** untuk menghasilkan dokumen cetak tabel jadwal lanskap resolusi tinggi.
  - Dilengkapi tanda tangan pimpinan dan stempel digital.
- **Digital QR Code Anti-Pemalsuan:**
  - Kode QR dinamis dihasilkan menggunakan **QRCoder**.
  - Menautkan dokumen langsung ke portal verifikasi online publik.

### 🔍 Portal Verifikasi Jadwal Publik (`/Verify/{code}`)
- Siapapun (karyawan, manajemen, atau pihak berwenang) dapat memindai QR Code pada lembar cetak jadwal untuk memvalidasi keaslian dokumen secara *real-time* tanpa perlu login.
- Menyajikan status keaslian, periode jadwal, tanggal terbit, dan pengesahan resmi.

### 📝 Manajemen Cuti & Izin Karyawan (`LeaveRequest`)
- Pengajuan cuti tahunan, izin darurat, atau izin sakit secara mandiri melalui web.
- Fitur unggah lampiran berkas bukti otentik (misal: surat keterangan dokter dari klinik/rumah sakit).
- Alur persetujuan (*approval workflow*) dua arah oleh Manajer/Admin.
- Pengajuan yang disetujui secara otomatis dieksklusikan dari mesin algoritma genetika.

### 👥 Master Data & Manajemen Karyawan
- Direktori data personel: NIK, Nama Lengkap, Email, Nomor Telepon, Jabatan, Level Keahlian (*Senior / Junior*), dan status aktif.
- Konfigurasi tipe shift: Jam mulai, jam selesai, dan kuota minimal kebutuhan per shift.

### 📢 Papan Pengumuman Operasional (`Announcement`)
- Sarana komunikasi satu arah dari manajemen untuk menyiarkan informasi operasional penting, SOP terbaru, atau perubahan kebijakan kerja ke seluruh karyawan.

### 🛡️ Keamanan & Akses Terenkripsi
- Enkripsi password tingkat tinggi menggunakan algoritma **BCrypt**.
- Sistem autentikasi berbasis Cookie yang aman dengan proteksi `SameAsRequest` dan `HttpOnly`.
- Fitur **Lupa Password** mandiri melalui token reset terenkripsi via email SMTP (**MailKit**).
- Pembagian hak akses berbasis peran (*Role-Based Access Control* / RBAC):
  - **Admin / Manager:** Akses penuh konfigurasi master data, eksekusi algoritma genetika, persetujuan cuti, dan publikasi pengumuman.
  - **Karyawan:** Melihat jadwal kerja pribadi & tim, melihat pengumuman, mengajukan cuti/sakit, dan mengunduh berkas jadwal.
- Siap di-deploy di belakang Reverse Proxy (Nginx / Linux VPS) dengan konfigurasi `ForwardedHeaders`.

---

## 🛠️ Technology Stack

| Komponen | Teknologi yang Digunakan |
|---|---|
| **Framework & Runtime** | ASP.NET Core 8.0 (.NET 8.0 SDK) |
| **Pola Arsitektur** | MVC (Model-View-Controller) + Repository & Service Pattern |
| **Mesin Optimasi** | Genetic Algorithm (Custom Optimization Engine in C#) |
| **Bahasa Pemrograman** | C# 12 |
| **Basis Data** | Microsoft SQL Server (MSSQL) 2019/2022 |
| **Object-Relational Mapping** | Entity Framework Core 8.0 (Code First / DbContext) |
| **Keamanan & Autentikasi** | Cookie Authentication, BCrypt.Net-Next, Claims-based RBAC |
| **Email Notifikasi** | MailKit 4.8.0 / MimeKit (SMTP Service) |
| **Generator Dokumen PDF** | QuestPDF 2024.3.0 |
| **Generator QR Code** | QRCoder 1.8.0 |
| **Antarmuka (Frontend)** | Razor Views (.cshtml), Bootstrap 5.3, Plus Jakarta Sans |
| **Interaktivitas Klien** | JavaScript (ES6+), jQuery 3.7.1, FontAwesome 6 |
| **Infrastruktur & Cloud** | Linux VPS (Contabo), Reverse Proxy Nginx / Kestrel |

---

## 📂 Struktur Direktori Proyek

```plaintext
SistemPenjadwalanShiftAlgoritmaGenetika/
├── SistemPenjadwalanShiftGA.sln           # Visual Studio Solution File
├── SistemPenjadwalan_Script.sql           # Skrip Database MSSQL DDL & Data Awal
├── schema.dbml                            # Dokumentasi Skema Database DBML
├── note.txt                               # Catatan Teknis Portofolio Proyek
├── p4/                                    # Berkas Dokumentasi & Tangkapan Layar Aplikasi
│   └── img/                               # Screenshot antarmuka sistem (Dashboard, Matriks, dsb)
│
└── SistemPenjadwalanShiftGA/              # Proyek Utama ASP.NET Core 8 MVC
    ├── Controllers/                       # MVC Controllers
    │   ├── AccountController.cs           # Autentikasi, Login, Logout, Lupa Password
    │   ├── AdminController.cs             # Dashboard Admin & Statistik Operasional
    │   ├── AnnouncementController.cs      # Manajemen Pengumuman Tim
    │   ├── EmployeeController.cs          # Manajemen Master Data Karyawan
    │   ├── HomeController.cs              # Dashboard Karyawan & Beranda
    │   ├── LeaveRequestController.cs      # Alur Pengajuan & Persetujuan Cuti/Sakit
    │   ├── ScheduleController.cs          # UI Penjadwalan & Live Preview Matriks
    │   ├── ShiftTypeController.cs         # Master Konfigurasi Jam & Kuota Shift
    │   └── VerifyController.cs            # Verifikasi Keabsahan Dokumen Jadwal via QR Code
    ├── Data/
    │   └── AppDbContext.cs                # EF Core Database Context & Relasi Entitas
    ├── Interfaces/                        # Service & Repository Contract Interfaces
    │   ├── IAuthService.cs
    │   ├── IEmailService.cs
    │   ├── IPdfExportService.cs
    │   └── IScheduleService.cs            # Interface Mesin Algoritma Genetika
    ├── Models/
    │   ├── Entities/                      # Domain Model Entities (User, Role, Schedule, Shift, dsb)
    │   └── ViewModels/                    # DTOs & View Presentation Models
    ├── Repositories/                      # Implementasi Akses Data SQL Server
    ├── Services/                          # Logika Bisnis & Mesin Komputasi
    │   ├── AuthService.cs                 # Enkripsi BCrypt & Pengelolaan Sesi
    │   ├── EmailService.cs                # Pengiriman Notifikasi & Token Reset Password
    │   ├── PdfExportService.cs            # Tata Letak Dokumen PDF QuestPDF
    │   └── ScheduleService.cs             # Core Mesin Optimasi Algoritma Genetika
    ├── Views/                             # Razor View Templates (Bootstrap 5 UI)
    ├── wwwroot/                           # Static Assets (CSS, JS, Icons, Uploads)
    │   ├── penjadwalan_pdf/               # Direktori Arsip Berkas PDF Jadwal
    │   └── upload/                        # Direktori Berkas Lampiran Surat Dokter/Cuti
    ├── appsettings.json                   # Konfigurasi Database Connection & SMTP
    └── Program.cs                         # Pipeline Middleware, Dependency Injection, & Routing
```

---

## 🗄️ Skema Basis Data

Sistem menggunakan basis data relasional **Microsoft SQL Server** dengan tabel-tabel utama:
- `Users`: Data akun karyawan dan admin (NIK, Password Hash BCrypt, Level, RoleId).
- `Roles`: Data pembagian hak akses pengguna (*Admin*, *Karyawan*).
- `ShiftTypes`: Konfigurasi jenis shift (*Pagi*, *Siang*, *Malam*) dan kuota staf harian.
- `SchedulePeriods`: Periode jadwal kerja (Tanggal Mulai, Tanggal Selesai, Kode Unik Verifikasi, Status).
- `ScheduleAssignments`: Hasil penetapan shift karyawan per tanggal hasil eksekusi Algoritma Genetika.
- `LeaveRequests`: Pengajuan cuti dan sakit beserta berkas lampiran dan status persetujuan.
- `Announcements`: Pengumuman resmi dari pimpinan untuk seluruh staf.

---

## ⚙️ Petunjuk Instalasi & Menjalankan Proyek

### Prasyarat Sistem:
1. [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. [Microsoft SQL Server](https://www.microsoft.com/sql-server/) (Express, Developer, atau Enterprise) atau Azure SQL
3. [Visual Studio 2022](https://visualstudio.microsoft.com/) / Visual Studio Code / JetBrains Rider

### Langkah-Langkah:

1. **Clone Repositori:**
   ```bash
   git clone https://github.com/tijartaufikrahman/employee-shift-scheduling-ga-web.git
   cd employee-shift-scheduling-ga-web
   ```

2. **Inisialisasi Basis Data:**
   - Buka SQL Server Management Studio (SSMS) atau Azure Data Studio.
   - Eksekusi berkas skrip `SistemPenjadwalan_Script.sql` untuk membuat struktur tabel dan data pengguna awal.

3. **Konfigurasi `appsettings.json`:**
   Sesuaikan connection string dan kredensial email SMTP pada `SistemPenjadwalanShiftGA/appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=localhost;Database=SistemPenjadwalan;Trusted_Connection=True;TrustServerCertificate=True;"
     },
     "Smtp": {
       "Host": "smtp.gmail.com",
       "Port": "587",
       "Username": "your-email@gmail.com",
       "Password": "your-app-password",
       "FromEmail": "your-email@gmail.com",
       "FromName": "Sistem Penjadwalan Shift GA"
     }
   }
   ```

4. **Jalankan Aplikasi:**
   ```bash
   cd SistemPenjadwalanShiftGA
   dotnet restore
   dotnet run
   ```
   Buka peramban di `http://localhost:5000` atau URL yang ditampilkan di terminal.

---

## 🌐 Live Deployment & Pengujian

Aplikasi telah berhasil dideploy dan diuji pada server Cloud VPS Linux:
- **Demo URL:** [http://62.171.169.192:9008/](http://62.171.169.192:9008/)
- **Fitur Live:** Eksplorasi dashboard admin, pembuatan jadwal cerdas via Algoritma Genetika, simulasi cetak PDF jadwal resmi, dan pengujian portal verifikasi online.

---

<p align="center">
  Developed by <b>Tijar Taufik Rahman</b><br>
  Software Engineer & Full Stack Developer
</p>
