-- =======================================================================
-- BACKUP SQL SCRIPT: DATABASE SISTEM PENJADWALAN SHIFT KERJA
-- Database: SistemPenjadwalan
-- Engine: Microsoft SQL Server (LocalDB / Express / Standard)
-- =======================================================================

-- 1. Inisialisasi Database
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'SistemPenjadwalan')
BEGIN
    CREATE DATABASE SistemPenjadwalan;
END
GO

USE SistemPenjadwalan;
GO

-- 2. Hapus Tabel Lama Jika Ada (Reverse Dependency)
IF OBJECT_ID('dbo.ScheduleDetails', 'U') IS NOT NULL DROP TABLE dbo.ScheduleDetails;
IF OBJECT_ID('dbo.SchedulePeriods', 'U') IS NOT NULL DROP TABLE dbo.SchedulePeriods;
IF OBJECT_ID('dbo.LeaveRequestAttachments', 'U') IS NOT NULL DROP TABLE dbo.LeaveRequestAttachments;
IF OBJECT_ID('dbo.LeaveRequests', 'U') IS NOT NULL DROP TABLE dbo.LeaveRequests;
IF OBJECT_ID('dbo.ShiftTypes', 'U') IS NOT NULL DROP TABLE dbo.ShiftTypes;
IF OBJECT_ID('dbo.Announcements', 'U') IS NOT NULL DROP TABLE dbo.Announcements;
IF OBJECT_ID('dbo.MenuRoles', 'U') IS NOT NULL DROP TABLE dbo.MenuRoles;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.Roles', 'U') IS NOT NULL DROP TABLE dbo.Roles;
GO

-- -----------------------------------------------------------------------
-- 3. Tabel Roles (Peran Sistem: Manager & Karyawan)
-- -----------------------------------------------------------------------
CREATE TABLE dbo.Roles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NamaRole NVARCHAR(50) NOT NULL UNIQUE,
    Deskripsi NVARCHAR(200) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy INT NULL
);
GO

-- -----------------------------------------------------------------------
-- 4. Tabel Users (Akun Pengguna)
-- -----------------------------------------------------------------------
CREATE TABLE dbo.Users (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RoleId INT NOT NULL,
    NamaLengkap NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    PhoneNumber NVARCHAR(20) NULL,
    Level NVARCHAR(20) NOT NULL DEFAULT 'Junior', -- 'Senior' atau 'Junior'
    IsActive BIT NOT NULL DEFAULT 1,
    ResetPasswordToken NVARCHAR(255) NULL,
    TokenCreatedAt DATETIME2 NULL,
    TokenExpiredAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy INT NULL,
    CONSTRAINT FK_Users_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id) ON DELETE NO ACTION
);
GO

-- -----------------------------------------------------------------------
-- 5. Tabel MenuRoles (Navigasi Menu Berdasarkan Role)
-- -----------------------------------------------------------------------
CREATE TABLE dbo.MenuRoles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    RoleId INT NOT NULL,
    NamaMenu NVARCHAR(100) NOT NULL,
    Icon NVARCHAR(100) NULL,
    Url NVARCHAR(200) NOT NULL,
    Urutan INT NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy INT NULL,
    CONSTRAINT FK_MenuRoles_Roles FOREIGN KEY (RoleId) REFERENCES dbo.Roles(Id) ON DELETE CASCADE
);
GO

-- -----------------------------------------------------------------------
-- 6. Tabel Announcements (Pengumuman Manager ke Seluruh Karyawan)
-- -----------------------------------------------------------------------
CREATE TABLE dbo.Announcements (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    Title NVARCHAR(150) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    Priority NVARCHAR(20) NOT NULL DEFAULT 'Normal', -- 'Normal', 'Important', 'Urgent'
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy INT NULL,
    CONSTRAINT FK_Announcements_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
);
GO

-- -----------------------------------------------------------------------
-- 7. Tabel ShiftTypes (Master Shift & Kuota Minimal Dinamis)
-- -----------------------------------------------------------------------
CREATE TABLE dbo.ShiftTypes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    ShiftName NVARCHAR(50) NOT NULL,
    ShiftCode NVARCHAR(10) NOT NULL UNIQUE,
    StartTime TIME(0) NOT NULL,
    EndTime TIME(0) NOT NULL,
    MinStaff INT NOT NULL DEFAULT 3,      -- Kuota minimal staf per shift
    MinSenior INT NOT NULL DEFAULT 1,     -- Kuota minimal staf senior
    IsNightShift BIT NOT NULL DEFAULT 0,  -- 1 jika shift malam
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy INT NULL
);
GO

-- -----------------------------------------------------------------------
-- 8. Tabel LeaveRequests (Pengajuan Cuti / Izin / Sakit)
-- -----------------------------------------------------------------------
CREATE TABLE dbo.LeaveRequests (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT NOT NULL,
    LeaveType NVARCHAR(20) NOT NULL,      -- 'Cuti', 'Sakit', 'Izin'
    RequestDate DATE NOT NULL,
    Reason NVARCHAR(MAX) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending', -- 'Pending', 'Approved', 'Rejected'
    RejectionNote NVARCHAR(MAX) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy INT NULL,
    CONSTRAINT FK_LeaveRequests_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
);
GO

-- -----------------------------------------------------------------------
-- 9. Tabel LeaveRequestAttachments (Multi-Lampiran Berkas Bukti)
-- -----------------------------------------------------------------------
CREATE TABLE dbo.LeaveRequestAttachments (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    LeaveRequestId INT NOT NULL,
    FileName NVARCHAR(255) NOT NULL,
    FilePath NVARCHAR(500) NOT NULL,
    FileType NVARCHAR(50) NOT NULL,
    FileSize INT NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy INT NULL,
    CONSTRAINT FK_LeaveRequestAttachments_LeaveRequests FOREIGN KEY (LeaveRequestId) REFERENCES dbo.LeaveRequests(Id) ON DELETE CASCADE
);
GO

-- -----------------------------------------------------------------------
-- 10. Tabel SchedulePeriods (Header Periode Jadwal & Verifikasi PDF/QR)
-- -----------------------------------------------------------------------
CREATE TABLE dbo.SchedulePeriods (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    PeriodName NVARCHAR(100) NOT NULL,
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    DurationType NVARCHAR(20) NOT NULL,   -- '1_Week' atau '2_Weeks'
    IsPublished BIT NOT NULL DEFAULT 0,
    PdfFilePath NVARCHAR(500) NULL,
    QrCodePath NVARCHAR(500) NULL,
    QrVerificationCode NVARCHAR(100) NULL UNIQUE,
    GeneratedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy INT NULL
);
GO

-- -----------------------------------------------------------------------
-- 11. Tabel ScheduleDetails (Transaksi Penempatan Shift Staf Harian)
-- -----------------------------------------------------------------------
CREATE TABLE dbo.ScheduleDetails (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    SchedulePeriodId INT NOT NULL,
    UserId INT NOT NULL,
    ShiftTypeId INT NULL,                 -- NULL jika OFF / Libur
    WorkDate DATE NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Assigned', -- 'Assigned', 'Off', 'Sick', 'Replaced'
    IsReplacement BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedBy INT NULL,
    UpdatedAt DATETIME2 NULL,
    UpdatedBy INT NULL,
    CONSTRAINT FK_ScheduleDetails_SchedulePeriods FOREIGN KEY (SchedulePeriodId) REFERENCES dbo.SchedulePeriods(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ScheduleDetails_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_ScheduleDetails_ShiftTypes FOREIGN KEY (ShiftTypeId) REFERENCES dbo.ShiftTypes(Id) ON DELETE SET NULL
);
GO

-- Indexing untuk Performa Query
CREATE INDEX IX_Users_RoleId ON dbo.Users(RoleId);
CREATE INDEX IX_MenuRoles_RoleId ON dbo.MenuRoles(RoleId);
CREATE INDEX IX_Announcements_UserId ON dbo.Announcements(UserId);
CREATE INDEX IX_LeaveRequests_UserId ON dbo.LeaveRequests(UserId);
CREATE INDEX IX_LeaveRequestAttachments_LeaveRequestId ON dbo.LeaveRequestAttachments(LeaveRequestId);
CREATE INDEX IX_ScheduleDetails_SchedulePeriodId ON dbo.ScheduleDetails(SchedulePeriodId);
CREATE INDEX IX_ScheduleDetails_UserId ON dbo.ScheduleDetails(UserId);
CREATE INDEX IX_ScheduleDetails_WorkDate ON dbo.ScheduleDetails(WorkDate);
GO

-- -----------------------------------------------------------------------
-- 12. SEED DATA AWAL (2 ROLE: Manager & Karyawan)
-- -----------------------------------------------------------------------

-- A. Insert Roles (Manager & Karyawan)
SET IDENTITY_INSERT dbo.Roles ON;
INSERT INTO dbo.Roles (Id, NamaRole, Deskripsi) VALUES
(1, N'Manager', N'Manajer / Supervisor dengan akses penuh pengelolaan jadwal dan staf'),
(2, N'Karyawan', N'Staf operasional dengan akses jadwal pribadi dan pengajuan izin');
SET IDENTITY_INSERT dbo.Roles OFF;
GO

-- B. Insert Menu Navigasi
SET IDENTITY_INSERT dbo.MenuRoles ON;
INSERT INTO dbo.MenuRoles (Id, RoleId, NamaMenu, Icon, Url, Urutan) VALUES
-- Menu Manager
(1, 1, N'Dashboard', N'fas fa-tachometer-alt', N'/Admin/Dashboard', 1),
(2, 1, N'Kelola Jadwal Shift', N'fas fa-calendar-alt', N'/Schedule/Index', 2),
(3, 1, N'Master Shift & Kuota', N'fas fa-clock', N'/ShiftType/Index', 3),
(4, 1, N'Data Karyawan', N'fas fa-users', N'/Employee/Index', 4),
(5, 1, N'Persetujuan Cuti/Sakit', N'fas fa-calendar-check', N'/LeaveRequest/Index', 5),
(6, 1, N'Pengumuman', N'fas fa-bullhorn', N'/Announcement/Index', 6),

-- Menu Karyawan
(7, 2, N'Dashboard', N'fas fa-home', N'/EmployeePortal/Dashboard', 1),
(8, 2, N'Jadwal Shift Saya', N'fas fa-calendar-day', N'/EmployeePortal/MySchedule', 2),
(9, 2, N'Pengajuan Cuti/Sakit', N'fas fa-file-medical', N'/EmployeePortal/RequestLeave', 3),
(10, 2, N'Pengumuman', N'fas fa-bullhorn', N'/EmployeePortal/Announcements', 4);
SET IDENTITY_INSERT dbo.MenuRoles OFF;
GO

-- C. Insert Master Shift Types Default
SET IDENTITY_INSERT dbo.ShiftTypes ON;
INSERT INTO dbo.ShiftTypes (Id, ShiftName, ShiftCode, StartTime, EndTime, MinStaff, MinSenior, IsNightShift) VALUES
(1, N'Shift Pagi', N'P', '07:00:00', '15:00:00', 3, 1, 0),
(2, N'Shift Sore', N'S', '15:00:00', '23:00:00', 3, 1, 0),
(3, N'Shift Malam', N'M', '23:00:00', '07:00:00', 2, 1, 1);
SET IDENTITY_INSERT dbo.ShiftTypes OFF;
GO

-- D. Insert Users Default (Password default: 'password123' bcrypt hash)
-- Hash: $2a$11$93mOPK/u6I2lupLLDEggA.2nr.JLS0iDY0kGbUBdTGhzoJu8vMtaq (Password: password123)
SET IDENTITY_INSERT dbo.Users ON;
INSERT INTO dbo.Users (Id, RoleId, NamaLengkap, Email, PasswordHash, PhoneNumber, Level, IsActive) VALUES
-- Manager (Role 1)
(1, 1, N'Budi Santoso (Manager)', N'manager@sistem.com', N'$2a$11$93mOPK/u6I2lupLLDEggA.2nr.JLS0iDY0kGbUBdTGhzoJu8vMtaq', N'081234567890', N'Senior', 1),

-- Karyawan Senior (Role 2)
(2, 2, N'Rian Pratama', N'rian@sistem.com', N'$2a$11$93mOPK/u6I2lupLLDEggA.2nr.JLS0iDY0kGbUBdTGhzoJu8vMtaq', N'081234567891', N'Senior', 1),
(3, 2, N'Siti Nurhaliza', N'siti@sistem.com', N'$2a$11$93mOPK/u6I2lupLLDEggA.2nr.JLS0iDY0kGbUBdTGhzoJu8vMtaq', N'081234567892', N'Senior', 1),

-- Karyawan Junior (Role 2)
(4, 2, N'Ahmad Fauzi', N'ahmad@sistem.com', N'$2a$11$93mOPK/u6I2lupLLDEggA.2nr.JLS0iDY0kGbUBdTGhzoJu8vMtaq', N'081234567893', N'Junior', 1),
(5, 2, N'Dewi Lestari', N'dewi@sistem.com', N'$2a$11$93mOPK/u6I2lupLLDEggA.2nr.JLS0iDY0kGbUBdTGhzoJu8vMtaq', N'081234567894', N'Junior', 1),
(6, 2, N'Eko Prasetyo', N'eko@sistem.com', N'$2a$11$93mOPK/u6I2lupLLDEggA.2nr.JLS0iDY0kGbUBdTGhzoJu8vMtaq', N'081234567895', N'Junior', 1),
(7, 2, N'Fani Rahmawati', N'fani@sistem.com', N'$2a$11$93mOPK/u6I2lupLLDEggA.2nr.JLS0iDY0kGbUBdTGhzoJu8vMtaq', N'081234567896', N'Junior', 1),
(8, 2, N'Gilang Ramadhan', N'gilang@sistem.com', N'$2a$11$93mOPK/u6I2lupLLDEggA.2nr.JLS0iDY0kGbUBdTGhzoJu8vMtaq', N'081234567897', N'Junior', 1),
(9, 2, N'Hani Handayani', N'hani@sistem.com', N'$2a$11$93mOPK/u6I2lupLLDEggA.2nr.JLS0iDY0kGbUBdTGhzoJu8vMtaq', N'081234567898', N'Junior', 1),
(10, 2, N'Indra Kusuma', N'indra@sistem.com', N'$2a$11$93mOPK/u6I2lupLLDEggA.2nr.JLS0iDY0kGbUBdTGhzoJu8vMtaq', N'081234567899', N'Junior', 1);
SET IDENTITY_INSERT dbo.Users OFF;
GO

-- E. Insert Pengumuman Sambutan Default
INSERT INTO dbo.Announcements (UserId, Title, Content, Priority) VALUES
(1, N'Selamat Datang di Sistem Penjadwalan Shift', N'Sistem telah aktif untuk mendukung penjadwalan shift otomatis, penanganan darurat staf sakit, dan permohonan izin terintegrasi.', N'Important');
GO
