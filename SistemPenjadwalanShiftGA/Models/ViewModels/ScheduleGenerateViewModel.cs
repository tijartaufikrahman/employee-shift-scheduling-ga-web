using System.ComponentModel.DataAnnotations;
using SistemPenjadwalanShiftGA.Interfaces;

namespace SistemPenjadwalanShiftGA.Models.ViewModels
{
    public class PreviousPeriodSummaryDto
    {
        public int Id { get; set; }
        public string ScheduleCode { get; set; } = string.Empty;
        private string _periodName = string.Empty;
        public string PeriodName
        {
            get => System.Text.RegularExpressions.Regex.Replace(_periodName ?? string.Empty, @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            set => _periodName = System.Text.RegularExpressions.Regex.Replace(value ?? string.Empty, @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime RecommendedNextStartDate => EndDate.AddDays(1);
        public bool IsPublished { get; set; }
        public int TotalEmployees { get; set; }
        public List<string> LastDayNightStaff { get; set; } = new();
        public List<string> LastDayMorningStaff { get; set; } = new();
        public List<string> LastDayAfternoonStaff { get; set; } = new();
    }

    public class ScheduleGenerateViewModel
    {
        [Required(ErrorMessage = "Tanggal mulai wajib diisi.")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Durasi jadwal wajib dipilih.")]
        public string DurationType { get; set; } = "1_Week"; // "1_Week" atau "2_Weeks"

        [Range(3, 7, ErrorMessage = "Maks hari kerja berturut-turut antara 3-7.")]
        [Display(Name = "Maks Hari Kerja Berturut-turut")]
        public int MaxConsecutiveDays { get; set; } = 5;

        [Range(1, 4, ErrorMessage = "Hari libur mingguan per staf harus antara 1-4 hari per minggu.")]
        [Display(Name = "Hari Libur Mingguan per Staf")]
        public int MaxOffDays { get; set; } = 1;

        [Display(Name = "Larangan Shift Malam ke Pagi")]
        public bool PreventNightToMorning { get; set; } = true;

        [Range(0, 5, ErrorMessage = "Minimal senior antara 0-5.")]
        [Display(Name = "Minimal Senior per Shift")]
        public int MinSenior { get; set; } = 1;

        [Display(Name = "Shift yang Ditentukan Sebagai Shift Malam")]
        public int? NightShiftId { get; set; }

        public PreviousPeriodSummaryDto? PreviousPeriod { get; set; }

        [Display(Name = "Acuan Riwayat Jadwal")]
        public int? PreviousPeriodId { get; set; }

        public List<SistemPenjadwalanShiftGA.Models.Entities.SchedulePeriod> AvailableHistoryPeriods { get; set; } = new();

        // Employee Readiness & Leaves Preview (Next 2 Weeks / Selected Period)
        public List<EmployeeReadinessDto> EmployeeReadinessList { get; set; } = new();
        public DateTime PreviewStartDate { get; set; }
        public DateTime PreviewEndDate { get; set; }
        public int TotalActiveEmployees => EmployeeReadinessList.Count;
        public int TotalEmployeesWithLeave => EmployeeReadinessList.Count(e => e.Leaves.Any());
        public int TotalEmployeesReady => TotalActiveEmployees - TotalEmployeesWithLeave;
    }

    public class EmployeeLeaveItemDto
    {
        public int Id { get; set; }
        public DateTime RequestDate { get; set; }
        public string LeaveType { get; set; } = string.Empty; // Cuti, Sakit, Izin
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Approved, Pending, Rejected
        public string FormattedDate => RequestDate.ToString("dd MMM yyyy");
    }

    public class EmployeeReadinessDto
    {
        public int UserId { get; set; }
        public string NamaLengkap { get; set; } = string.Empty;
        public string Level { get; set; } = "Junior";
        public List<EmployeeLeaveItemDto> Leaves { get; set; } = new();
        public int TotalLeaveDays => Leaves.Count;
        public bool HasApprovedLeave => Leaves.Any(l => l.Status == "Approved");
        public bool HasPendingLeave => Leaves.Any(l => l.Status == "Pending");
        public string ApprovedLeaveType
        {
            get
            {
                var types = Leaves.Where(l => l.Status == "Approved").Select(l => string.IsNullOrWhiteSpace(l.LeaveType) ? "Dispensasi" : l.LeaveType).Distinct().ToList();
                if (!types.Any()) return string.Empty;
                return string.Join(" / ", types);
            }
        }
        public string AvailabilityStatus
        {
            get
            {
                if (HasApprovedLeave)
                {
                    var type = ApprovedLeaveType;
                    return (string.IsNullOrEmpty(type) ? "Dispensasi" : type) + " Disetujui";
                }
                if (HasPendingLeave)
                {
                    var pendingTypes = Leaves.Where(l => l.Status == "Pending").Select(l => string.IsNullOrWhiteSpace(l.LeaveType) ? "Izin" : l.LeaveType).Distinct().ToList();
                    return string.Join(" / ", pendingTypes) + " Pending";
                }
                return "Siap Penuh";
            }
        }
    }

    public class GenerateScheduleViewModel : ScheduleGenerateViewModel { }

    public class EmergencyReplacementViewModel
    {
        public int PeriodId { get; set; }
        public int SchedulePeriodId { get => PeriodId; set => PeriodId = value; }

        public int DetailId { get; set; }
        public int TargetDetailId { get => DetailId; set => DetailId = value; }
        public int ScheduleDetailId { get => DetailId; set => DetailId = value; }

        public int SickUserId { get; set; }
        public string SickUserName { get; set; } = string.Empty;
        public string SickEmployeeName { get => SickUserName; set => SickUserName = value; }

        public DateTime SickDate { get; set; }
        public DateTime TargetDate { get => SickDate; set => SickDate = value; }

        public string ShiftName { get; set; } = string.Empty;
        public string TargetShiftName { get => ShiftName; set => ShiftName = value; }
        public string TargetShiftCode { get; set; } = string.Empty;

        public int ReplacementUserId { get; set; }
        public int ReplacementEmployeeId { get => ReplacementUserId; set => ReplacementUserId = value; }

        public List<EmergencyCandidate> Candidates { get; set; } = new();
    }

    public class SchedulePeriodInfo
    {
        public int Id { get; set; }
        public string ScheduleCode { get; set; } = string.Empty;
        private string _periodName = string.Empty;
        public string PeriodName
        {
            get => System.Text.RegularExpressions.Regex.Replace(_periodName ?? string.Empty, @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            set => _periodName = System.Text.RegularExpressions.Regex.Replace(value ?? string.Empty, @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsPublished { get; set; }
        public string DurationType { get; set; } = string.Empty;
    }

    public class ScheduleMatrixCell
    {
        public int Id { get; set; }
        public int EmployeeId { get; set; }
        public DateTime Date { get; set; }
        public string ShiftCode { get; set; } = "OFF";
        public bool IsSick { get; set; }
        public bool IsReplaced { get; set; }
        public bool IsReplacement { get; set; }
    }

    public class EmployeeInfo
    {
        public int Id { get; set; }
        public string NamaLengkap { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
    }

    public class ScheduleMatrixViewModel
    {
        public int PeriodId { get; set; }
        public string ScheduleCode { get; set; } = string.Empty;
        private string _periodName = string.Empty;
        public string PeriodName
        {
            get => System.Text.RegularExpressions.Regex.Replace(_periodName ?? string.Empty, @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            set => _periodName = System.Text.RegularExpressions.Regex.Replace(value ?? string.Empty, @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsPublished { get; set; }
        public List<DateTime> Dates { get; set; } = new();
        public List<EmployeeScheduleRow> Rows { get; set; } = new();

        public SchedulePeriodInfo Period => new SchedulePeriodInfo
        {
            Id = PeriodId,
            ScheduleCode = ScheduleCode,
            PeriodName = PeriodName,
            StartDate = StartDate,
            EndDate = EndDate,
            IsPublished = IsPublished,
            DurationType = Dates.Count.ToString()
        };

        public List<EmployeeInfo> Employees => Rows.Select(r => new EmployeeInfo
        {
            Id = r.UserId,
            NamaLengkap = r.NamaLengkap,
            Level = r.Level
        }).ToList();

        public List<ScheduleMatrixCell> Matrix
        {
            get
            {
                var list = new List<ScheduleMatrixCell>();
                foreach (var r in Rows)
                {
                    for (int i = 0; i < Dates.Count && i < r.Days.Count; i++)
                    {
                        var day = r.Days[i];
                        list.Add(new ScheduleMatrixCell
                        {
                            Id = day?.DetailId ?? 0,
                            EmployeeId = r.UserId,
                            Date = Dates[i],
                            ShiftCode = day?.ShiftCode ?? "OFF",
                            IsSick = day?.Status == "Sick",
                            IsReplaced = day?.Status == "Replaced",
                            IsReplacement = day?.IsReplacement ?? false
                        });
                    }
                }
                return list;
            }
        }
    }

    public class EmployeeScheduleRow
    {
        public int UserId { get; set; }
        public string NamaLengkap { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public List<DayAssignment> Days { get; set; } = new();
        public int TotalShifts { get; set; }
        public int TotalOffDays => Days.Count(d => d == null || d.ShiftCode == "OFF" || d.Status == "Off");
        public int NightShifts { get; set; }
    }

    public class DayAssignment
    {
        public int? DetailId { get; set; }
        public string? ShiftCode { get; set; } // P, S, M, or null for OFF
        public string? ShiftName { get; set; }
        public string Status { get; set; } = "Off"; // Assigned, Off, Sick, Replaced
        public bool IsReplacement { get; set; }
        public bool IsNightShift { get; set; }
    }
}
