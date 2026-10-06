using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.Entities;

namespace SistemPenjadwalanShiftGA.Services
{
    public class ScheduleService : IScheduleService
    {
        private readonly IUserRepository _userRepository;
        private readonly IShiftTypeRepository _shiftTypeRepository;
        private readonly ILeaveRequestRepository _leaveRequestRepository;
        private readonly IScheduleRepository _scheduleRepository;
        private readonly Random _random = new Random();

        public ScheduleService(
            IUserRepository userRepository,
            IShiftTypeRepository shiftTypeRepository,
            ILeaveRequestRepository leaveRequestRepository,
            IScheduleRepository scheduleRepository)
        {
            _userRepository = userRepository;
            _shiftTypeRepository = shiftTypeRepository;
            _leaveRequestRepository = leaveRequestRepository;
            _scheduleRepository = scheduleRepository;
        }

        public async Task<SchedulePreviewData> GenerateSchedulePreviewAsync(
            DateTime startDate, 
            string durationType, 
            int maxConsecutiveDays, 
            int maxOffDays, 
            bool preventNightToMorning, 
            int minSenior, 
            int? nightShiftId, 
            int createdBy,
            int? previousPeriodId = null)
        {
            // Support custom number of weeks (e.g. 1, 2, 3, 4, etc.)
            int weeks = 1;
            if (!string.IsNullOrWhiteSpace(durationType))
            {
                var digits = new string(durationType.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out int parsed) && parsed > 0)
                {
                    if (parsed >= 7 && parsed % 7 == 0) weeks = parsed / 7;
                    else weeks = parsed;
                }
            }

            int days = Math.Max(1, weeks) * 7;
            string normalizedDurationType = $"{weeks}_Weeks";
            DateTime endDate = startDate.AddDays(days - 1);
            string periodName = $"Jadwal {startDate:dd MMM} - {endDate:dd MMM yyyy}";

            if (maxOffDays <= 0) maxOffDays = weeks * 1;
            maxOffDays = Math.Min(days - 1, maxOffDays);

            var activeUsers = (await _userRepository.GetAllAsync())
                .Where(u => u.RoleId == 2 && u.IsActive).ToList();
            
            var shiftTypes = (await _shiftTypeRepository.GetAllAsync()).ToList();

            // Resolve NightShiftId: if not specified or invalid, look for shift code 'M' or name containing 'malam'
            int? resolvedNightShiftId = nightShiftId;
            if (!resolvedNightShiftId.HasValue || resolvedNightShiftId.Value <= 0 || !shiftTypes.Any(s => s.Id == resolvedNightShiftId.Value))
            {
                var detectedNight = shiftTypes.FirstOrDefault(s => s.ShiftCode.Equals("M", StringComparison.OrdinalIgnoreCase) || s.ShiftName.ToLower().Contains("malam"));
                resolvedNightShiftId = detectedNight?.Id;
            }

            bool IsNight(int? sId) => resolvedNightShiftId.HasValue && sId == resolvedNightShiftId.Value;
            
            var leaves = await _leaveRequestRepository.GetApprovedForDateRangeAsync(startDate, endDate);
            
            IEnumerable<ScheduleDetail> lastPeriodDetails;
            if (previousPeriodId.HasValue)
            {
                if (previousPeriodId.Value > 0)
                {
                    lastPeriodDetails = await _scheduleRepository.GetDetailsByPeriodIdAsync(previousPeriodId.Value);
                }
                else
                {
                    lastPeriodDetails = new List<ScheduleDetail>();
                }
            }
            else
            {
                lastPeriodDetails = await _scheduleRepository.GetLastPeriodDetailsAsync();
            }

            var dates = Enumerable.Range(0, days).Select(d => startDate.AddDays(d)).ToList();

            // GA Parameters
            int popSize = 50;
            int generations = 200;
            double mutationRate = 0.1;
            
            // Initialization with even shift distribution tracking and max off days cap
            var population = new List<Dictionary<(int, DateTime), int?>>();
            for (int i = 0; i < popSize; i++)
            {
                population.Add(InitializeChromosome(activeUsers, dates, shiftTypes, leaves, maxOffDays, resolvedNightShiftId, minSenior));
            }

            Dictionary<(int, DateTime), int?>? bestChromosome = null;
            double bestFitness = double.MaxValue;

            for (int gen = 0; gen < generations; gen++)
            {
                var fitnessScores = population.Select(c => new 
                    { 
                        Chromosome = c, 
                        Fitness = CalculateFitness(c, activeUsers, dates, shiftTypes, leaves, maxConsecutiveDays, maxOffDays, preventNightToMorning, minSenior, resolvedNightShiftId, lastPeriodDetails, startDate) 
                    })
                    .OrderBy(x => x.Fitness).ToList();

                if (fitnessScores.First().Fitness < bestFitness)
                {
                    bestFitness = fitnessScores.First().Fitness;
                    bestChromosome = fitnessScores.First().Chromosome;
                }

                // Elitism
                var newPopulation = new List<Dictionary<(int, DateTime), int?>>
                {
                    CloneChromosome(fitnessScores[0].Chromosome),
                    CloneChromosome(fitnessScores[1].Chromosome)
                };

                while (newPopulation.Count < popSize)
                {
                    var parent1 = TournamentSelection(population, fitnessScores.Select(f => f.Fitness).ToList());
                    var parent2 = TournamentSelection(population, fitnessScores.Select(f => f.Fitness).ToList());

                    var child = Crossover(parent1, parent2, activeUsers, dates);

                    if (_random.NextDouble() < mutationRate)
                    {
                        Mutate(child, activeUsers, dates, shiftTypes, leaves, resolvedNightShiftId);
                    }

                    newPopulation.Add(child);
                }

                population = newPopulation;
            }

            // Post-GA Equalizer Pass to strictly balance total shifts and off days per employee
            if (bestChromosome != null)
            {
                EqualizeEmployeeShifts(bestChromosome, activeUsers, dates, shiftTypes, leaves, maxConsecutiveDays, maxOffDays, preventNightToMorning, minSenior, resolvedNightShiftId);

                // Hard guarantee: Ensure no employee on approved leave (Izin, Cuti, Sakit) is EVER assigned to work
                foreach (var leave in leaves)
                {
                    var key = (leave.UserId, leave.RequestDate.Date);
                    if (bestChromosome.ContainsKey(key) && bestChromosome[key] != null)
                    {
                        var shiftId = bestChromosome[key];
                        bestChromosome[key] = null;
                        var replacement = activeUsers.FirstOrDefault(u => 
                            u.Id != leave.UserId && 
                            bestChromosome.ContainsKey((u.Id, leave.RequestDate.Date)) && 
                            bestChromosome[(u.Id, leave.RequestDate.Date)] == null &&
                            !leaves.Any(l => l.UserId == u.Id && l.RequestDate.Date == leave.RequestDate.Date));
                        if (replacement != null)
                        {
                            bestChromosome[(replacement.Id, leave.RequestDate.Date)] = shiftId;
                        }
                    }
                }
            }

            // Generate distinct official Schedule Code (e.g. SCH-202609-001)
            var allExistingPeriods = await _scheduleRepository.GetAllPeriodsAsync();
            var yearMonth = startDate.ToString("yyyyMM");
            var monthCount = allExistingPeriods.Count(p => p.StartDate.ToString("yyyyMM") == yearMonth || (!string.IsNullOrEmpty(p.ScheduleCode) && p.ScheduleCode.StartsWith($"SCH-{yearMonth}")));
            var sequence = monthCount + 1;
            string scheduleCode = $"SCH-{yearMonth}-{sequence:D3}";
            while (allExistingPeriods.Any(p => p.ScheduleCode == scheduleCode))
            {
                sequence++;
                scheduleCode = $"SCH-{yearMonth}-{sequence:D3}";
            }

            // Map best chromosome to preview DTO (Do NOT save to database yet)
            var previewData = new SchedulePreviewData
            {
                Token = Guid.NewGuid().ToString("N"),
                ScheduleCode = scheduleCode,
                PeriodName = periodName,
                StartDate = startDate,
                EndDate = endDate,
                DurationType = normalizedDurationType,
                TotalDays = days,
                MaxConsecutiveDays = maxConsecutiveDays,
                MaxOffDays = maxOffDays,
                PreventNightToMorning = preventNightToMorning,
                MinSenior = minSenior,
                NightShiftId = resolvedNightShiftId,
                PreviousPeriodId = previousPeriodId,
                CreatedBy = createdBy,
                GeneratedAt = DateTime.UtcNow.AddHours(7)
            };

            if (bestChromosome != null)
            {
                foreach (var kvp in bestChromosome)
                {
                    var userId = kvp.Key.Item1;
                    var date = kvp.Key.Item2;
                    var shiftId = kvp.Value;
                    var user = activeUsers.FirstOrDefault(u => u.Id == userId);
                    var shift = shiftTypes.FirstOrDefault(s => s.Id == shiftId);
                    
                    string status = shiftId == null ? "Off" : "Assigned";

                    previewData.Details.Add(new ScheduleDetailPreviewItem
                    {
                        UserId = userId,
                        UserName = user?.NamaLengkap ?? "Karyawan",
                        UserLevel = user?.Level ?? "Junior",
                        ShiftTypeId = status == "Off" ? null : shiftId,
                        ShiftCode = shift?.ShiftCode ?? (status == "Off" ? "OFF" : status),
                        ShiftName = shift?.ShiftName ?? (status == "Off" ? "Libur" : "Shift"),
                        IsNightShift = IsNight(shiftId),
                        WorkDate = date,
                        Status = status,
                        IsReplacement = false
                    });
                }
            }

            return previewData;
        }

        public async Task<SchedulePeriod> SavePreviewAsync(SchedulePreviewData previewData)
        {
            string scheduleCode = previewData.ScheduleCode;
            if (string.IsNullOrWhiteSpace(scheduleCode))
            {
                var allExistingPeriods = await _scheduleRepository.GetAllPeriodsAsync();
                var yearMonth = previewData.StartDate.ToString("yyyyMM");
                var monthCount = allExistingPeriods.Count(p => p.StartDate.ToString("yyyyMM") == yearMonth || (!string.IsNullOrEmpty(p.ScheduleCode) && p.ScheduleCode.StartsWith($"SCH-{yearMonth}")));
                var sequence = monthCount + 1;
                scheduleCode = $"SCH-{yearMonth}-{sequence:D3}";
                while (allExistingPeriods.Any(p => p.ScheduleCode == scheduleCode))
                {
                    sequence++;
                    scheduleCode = $"SCH-{yearMonth}-{sequence:D3}";
                }
            }

            var period = new SchedulePeriod
            {
                ScheduleCode = scheduleCode,
                PeriodName = System.Text.RegularExpressions.Regex.Replace(previewData.PeriodName ?? "", @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim(),
                StartDate = previewData.StartDate,
                EndDate = previewData.EndDate,
                DurationType = previewData.DurationType,
                IsPublished = true,
                MaxConsecutiveDays = previewData.MaxConsecutiveDays,
                MaxOffDays = previewData.MaxOffDays,
                PreventNightToMorning = previewData.PreventNightToMorning,
                MinSenior = previewData.MinSenior,
                NightShiftId = previewData.NightShiftId,
                GeneratedAt = previewData.GeneratedAt,
                CreatedBy = previewData.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                QrVerificationCode = Guid.NewGuid().ToString("N")
            };

            await _scheduleRepository.AddPeriodAsync(period);
            await _scheduleRepository.SaveChangesAsync();

            var details = previewData.Details.Select(d => new ScheduleDetail
            {
                SchedulePeriodId = period.Id,
                UserId = d.UserId,
                ShiftTypeId = d.ShiftTypeId,
                WorkDate = d.WorkDate,
                Status = d.Status,
                IsReplacement = d.IsReplacement,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = previewData.CreatedBy
            }).ToList();

            await _scheduleRepository.AddDetailsRangeAsync(details);
            await _scheduleRepository.SaveChangesAsync();

            return period;
        }

        public async Task<SchedulePeriod> GenerateScheduleAsync(
            DateTime startDate, 
            string durationType, 
            int maxConsecutiveDays, 
            int maxOffDays, 
            bool preventNightToMorning, 
            int minSenior, 
            int? nightShiftId, 
            int createdBy,
            int? previousPeriodId = null)
        {
            var preview = await GenerateSchedulePreviewAsync(startDate, durationType, maxConsecutiveDays, maxOffDays, preventNightToMorning, minSenior, nightShiftId, createdBy, previousPeriodId);
            return await SavePreviewAsync(preview);
        }

        public async Task<List<EmergencyCandidate>> FindEmergencyReplacementsAsync(int periodId, int sickUserId, DateTime sickDate)
        {
            var period = await _scheduleRepository.GetPeriodByIdAsync(periodId);
            var details = await _scheduleRepository.GetDetailsByPeriodIdAsync(periodId);
            var sickDetail = details.FirstOrDefault(d => d.UserId == sickUserId && d.WorkDate.Date == sickDate.Date);

            if (sickDetail == null) return new List<EmergencyCandidate>();

            var users = await _userRepository.GetAllAsync();
            var activeKaryawan = users.Where(u => u.RoleId == 2 && u.IsActive).ToList();
            var leavesOnDate = await _leaveRequestRepository.GetApprovedForDateRangeAsync(sickDate.Date, sickDate.Date);

            var offUsersOnDate = activeKaryawan.Where(u => 
                !details.Any(d => d.UserId == u.Id && d.WorkDate.Date == sickDate.Date && d.Status == "Assigned") &&
                !leavesOnDate.Any(l => l.UserId == u.Id)
            ).ToList();

            var candidates = new List<EmergencyCandidate>();
            int? nightShiftId = period?.NightShiftId;
            bool preventNightToMorning = period?.PreventNightToMorning ?? true;
            bool IsNight(int? sId) => nightShiftId.HasValue && sId == nightShiftId.Value;

            foreach (var u in offUsersOnDate)
            {
                // Check night-to-morning violation
                if (preventNightToMorning)
                {
                    var prevDayDetail = details.FirstOrDefault(d => d.UserId == u.Id && d.WorkDate.Date == sickDate.AddDays(-1).Date && d.Status == "Assigned");
                    if (prevDayDetail != null && IsNight(prevDayDetail.ShiftTypeId) && !IsNight(sickDetail.ShiftTypeId))
                    {
                        // Skip
                        continue;
                    }
                }

                var totalShifts = details.Count(d => d.UserId == u.Id && d.Status == "Assigned");
                
                candidates.Add(new EmergencyCandidate
                {
                    UserId = u.Id,
                    NamaLengkap = u.NamaLengkap,
                    Level = u.Level,
                    TotalShiftsInPeriod = totalShifts
                });
            }

            return candidates.OrderBy(c => c.TotalShiftsInPeriod)
                .ThenByDescending(c => c.Level) // Senior first
                .ToList();
        }

        private Dictionary<(int, DateTime), int?> InitializeChromosome(
            List<User> users, 
            List<DateTime> dates, 
            List<ShiftType> shifts, 
            IEnumerable<LeaveRequest> leaves,
            int maxOffDays,
            int? nightShiftId,
            int minSenior = 0)
        {
            var chromosome = new Dictionary<(int, DateTime), int?>();
            bool IsNight(int? sId) => nightShiftId.HasValue && sId == nightShiftId.Value;
            var daytimeShifts = shifts.Where(s => !IsNight(s.Id)).ToList();
            if (!daytimeShifts.Any()) daytimeShifts = shifts;

            // Default all user-dates to null (OFF)
            foreach (var u in users)
            {
                foreach (var date in dates)
                {
                    chromosome[(u.Id, date)] = null;
                }
            }

            int numWeeks = Math.Max(1, dates.Count / 7);
            var userTotalShifts = users.ToDictionary(u => u.Id, u => 0);

            // Plan week by week to strictly enforce weekly off-days distribution
            for (int w = 0; w < numWeeks; w++)
            {
                var weekDates = dates.Skip(w * 7).Take(7).ToList();
                int targetOffThisWeek = maxOffDays / numWeeks;
                if (w < (maxOffDays % numWeeks)) targetOffThisWeek++;
                if (targetOffThisWeek <= 0) targetOffThisWeek = 1;

                // Track how many users take OFF on each day of weekDates to evenly distribute days off
                var dailyOffCount = weekDates.ToDictionary(d => d, d => 0);
                var userWorkDaysInWeek = users.ToDictionary(u => u.Id, u => new HashSet<DateTime>());

                // 1. Determine OFF days and WORK days for each user in this week
                foreach (var user in users.OrderBy(x => _random.Next()))
                {
                    // Approved leaves are mandatory OFF days
                    var approvedLeaveDays = weekDates.Where(d => 
                        leaves.Any(l => l.UserId == user.Id && d.Date >= l.RequestDate.Date && d.Date <= l.RequestDate.Date)).ToList();

                    foreach (var d in approvedLeaveDays)
                    {
                        dailyOffCount[d]++;
                    }

                    int neededOff = targetOffThisWeek - approvedLeaveDays.Count;
                    var candidateOffDays = weekDates.Except(approvedLeaveDays).ToList();

                    var chosenOffDays = new List<DateTime>(approvedLeaveDays);
                    if (neededOff > 0 && candidateOffDays.Any())
                    {
                        // Distribute off days smoothly across days with fewer off staff
                        var selected = candidateOffDays
                            .OrderBy(d => dailyOffCount[d])
                            .ThenBy(x => _random.Next())
                            .Take(neededOff)
                            .ToList();

                        foreach (var d in selected)
                        {
                            dailyOffCount[d]++;
                            chosenOffDays.Add(d);
                        }
                    }

                    // All days in weekDates not in chosenOffDays are working days for this user
                    foreach (var d in weekDates)
                    {
                        if (!chosenOffDays.Contains(d))
                        {
                            userWorkDaysInWeek[user.Id].Add(d);
                        }
                    }
                }

                // 2. Assign shifts for each day of the week
                foreach (var d in weekDates)
                {
                    var availableWorkers = users.Where(u => userWorkDaysInWeek[u.Id].Contains(d)).ToList();

                    // Ensure total available workers is at least MinStaff sum
                    int totalRequired = shifts.Sum(s => s.MinStaff);
                    while (availableWorkers.Count < totalRequired)
                    {
                        // Emergency draw: pick an off user who has no approved leave on day d
                        var candidate = users.Except(availableWorkers)
                            .Where(u => !leaves.Any(l => l.UserId == u.Id && d.Date >= l.RequestDate.Date && d.Date <= l.RequestDate.Date))
                            .OrderByDescending(u => weekDates.Count(day => !userWorkDaysInWeek[u.Id].Contains(day)))
                            .FirstOrDefault();

                        if (candidate == null) break;
                        availableWorkers.Add(candidate);
                        userWorkDaysInWeek[candidate.Id].Add(d);
                    }

                    var remainingWorkers = new List<User>(availableWorkers.OrderBy(u => userTotalShifts[u.Id]).ThenBy(x => _random.Next()));

                    // Mandatory MinStaff per shift
                    foreach (var shift in shifts)
                    {
                        for (int i = 0; i < shift.MinStaff; i++)
                        {
                            User? chosenWorker = null;
                            if (minSenior > 0 && i < minSenior)
                            {
                                chosenWorker = remainingWorkers.FirstOrDefault(u => u.Level == "Senior");
                            }
                            if (chosenWorker == null)
                            {
                                chosenWorker = remainingWorkers.FirstOrDefault();
                            }

                            if (chosenWorker != null)
                            {
                                chromosome[(chosenWorker.Id, d)] = shift.Id;
                                userTotalShifts[chosenWorker.Id]++;
                                remainingWorkers.Remove(chosenWorker);
                            }
                        }
                    }

                    // Assign surplus workers to daytime shifts
                    while (remainingWorkers.Any())
                    {
                        var worker = remainingWorkers.First();
                        var targetShift = daytimeShifts
                            .OrderBy(s => users.Count(u => chromosome[(u.Id, d)] == s.Id))
                            .First();

                        chromosome[(worker.Id, d)] = targetShift.Id;
                        userTotalShifts[worker.Id]++;
                        remainingWorkers.Remove(worker);
                    }
                }
            }

            return chromosome;
        }

        private double CalculateFitness(
            Dictionary<(int, DateTime), int?> chromosome, 
            List<User> users, 
            List<DateTime> dates, 
            List<ShiftType> shifts, 
            IEnumerable<LeaveRequest> leaves, 
            int maxConsecutiveDays, 
            int maxOffDays,
            bool preventNightToMorning,
            int minSenior,
            int? nightShiftId,
            IEnumerable<ScheduleDetail> lastPeriod, 
            DateTime startDate)
        {
            double penalty = 0;
            bool IsNight(int? sId) => nightShiftId.HasValue && sId == nightShiftId.Value;

            // 1. MinStaff and MinSenior constraint per shift per day
            foreach (var date in dates)
            {
                foreach (var shift in shifts)
                {
                    var assignedToShift = users.Where(u => chromosome[(u.Id, date)] == shift.Id).ToList();
                    
                    // MinStaff constraint
                    if (assignedToShift.Count < shift.MinStaff)
                    {
                        penalty += 1000 * (shift.MinStaff - assignedToShift.Count);
                    }

                    // Dynamic MinSenior constraint
                    if (minSenior > 0)
                    {
                        int seniorCount = assignedToShift.Count(u => u.Level == "Senior");
                        if (seniorCount < minSenior)
                        {
                            penalty += 50 * (minSenior - seniorCount);
                        }
                    }
                }
            }

            // 2. Weekly Off-Days Distribution Check (CRITICAL: Every week MUST have days off!)
            int numWeeks = Math.Max(1, dates.Count / 7);
            for (int w = 0; w < numWeeks; w++)
            {
                var weekDates = dates.Skip(w * 7).Take(7).ToList();
                int targetOffThisWeek = maxOffDays / numWeeks;
                if (w < (maxOffDays % numWeeks)) targetOffThisWeek++;
                if (targetOffThisWeek <= 0) targetOffThisWeek = 1;

                foreach (var user in users)
                {
                    int offInWeek = weekDates.Count(d => chromosome[(user.Id, d)] == null);

                    // HEAVY HARD PENALTY: Employee has 0 days off in a 7-day week!
                    if (offInWeek == 0)
                    {
                        penalty += 5000;
                    }
                    else
                    {
                        // Penalty for deviation from weekly target (prevents clumping in one week and starving another)
                        int diff = Math.Abs(offInWeek - targetOffThisWeek);
                        penalty += diff * 1200;
                    }
                }
            }

            // 3. Consecutive days, night-to-morning, leaves, and total shifts
            var shiftsPerUser = new Dictionary<int, int>();
            var nightsPerUser = new Dictionary<int, int>();

            foreach (var user in users)
            {
                shiftsPerUser[user.Id] = 0;
                nightsPerUser[user.Id] = 0;
                int consecutive = 0;
                int consecutiveOff = 0;
                bool wasNight = false;

                // Check last day of previous period
                if (lastPeriod != null)
                {
                    var lastDayDetail = lastPeriod.FirstOrDefault(d => d.UserId == user.Id && d.WorkDate.Date == startDate.AddDays(-1).Date && d.Status == "Assigned");
                    if (lastDayDetail != null)
                    {
                        consecutive++;
                        if (IsNight(lastDayDetail.ShiftTypeId)) wasNight = true;
                    }
                }

                foreach (var date in dates)
                {
                    var shiftId = chromosome[(user.Id, date)];
                    
                    if (leaves.Any(l => l.UserId == user.Id && date >= l.RequestDate.Date && date <= l.RequestDate.Date) && shiftId != null)
                    {
                        penalty += 10000;
                    }

                    if (shiftId != null)
                    {
                        var shift = shifts.First(s => s.Id == shiftId);
                        shiftsPerUser[user.Id]++;
                        if (IsNight(shift.Id)) nightsPerUser[user.Id]++;

                        consecutive++;
                        if (consecutive > maxConsecutiveDays) penalty += 1000 * (consecutive - maxConsecutiveDays);

                        if (preventNightToMorning && wasNight && !IsNight(shift.Id)) penalty += 1000;
                        wasNight = IsNight(shift.Id);

                        if (consecutiveOff > 2)
                        {
                            penalty += 300 * (consecutiveOff - 2);
                        }
                        consecutiveOff = 0;
                    }
                    else
                    {
                        consecutive = 0;
                        wasNight = false;
                        consecutiveOff++;
                    }
                }

                if (consecutiveOff > 2)
                {
                    penalty += 300 * (consecutiveOff - 2);
                }

                // Strict penalty if total off days exceeds maxOffDays
                int offCount = dates.Count - shiftsPerUser[user.Id];
                if (offCount > maxOffDays)
                {
                    penalty += 2000 * (offCount - maxOffDays);
                }
            }

            if (shiftsPerUser.Any())
            {
                int targetShiftsPerUser = Math.Max(1, dates.Count - maxOffDays);
                foreach (var count in shiftsPerUser.Values)
                {
                    penalty += Math.Abs(count - targetShiftsPerUser) * 150;
                }
                penalty += (shiftsPerUser.Values.Max() - shiftsPerUser.Values.Min()) * 300;
                penalty += (nightsPerUser.Values.Max() - nightsPerUser.Values.Min()) * 100;
            }

            return penalty;
        }

        private void EnforceWeeklyOffDays(
            Dictionary<(int, DateTime), int?> chromosome,
            List<User> users,
            List<DateTime> dates,
            List<ShiftType> shifts,
            IEnumerable<LeaveRequest> leaves,
            int maxOffDays,
            bool preventNightToMorning,
            int? nightShiftId)
        {
            int numWeeks = Math.Max(1, dates.Count / 7);

            for (int w = 0; w < numWeeks; w++)
            {
                var weekDates = dates.Skip(w * 7).Take(7).ToList();
                int targetOffThisWeek = maxOffDays / numWeeks;
                if (w < (maxOffDays % numWeeks)) targetOffThisWeek++;
                if (targetOffThisWeek <= 0) targetOffThisWeek = 1;

                // 1. Identify users with fewer off days than targetOffThisWeek (especially 0 off days!)
                foreach (var user in users)
                {
                    int currentOff = weekDates.Count(d => chromosome[(user.Id, d)] == null);
                    while (currentOff < targetOffThisWeek)
                    {
                        bool resolved = false;

                        // Strategy A: Find a day where total staff on duty > MinStaff, user can take OFF directly
                        foreach (var d in weekDates.OrderBy(x => _random.Next()))
                        {
                            var shiftId = chromosome[(user.Id, d)];
                            if (shiftId == null) continue;
                            var shift = shifts.FirstOrDefault(s => s.Id == shiftId);
                            if (shift == null) continue;

                            int countOnShift = users.Count(u => chromosome[(u.Id, d)] == shift.Id);
                            if (countOnShift > shift.MinStaff)
                            {
                                chromosome[(user.Id, d)] = null;
                                currentOff++;
                                resolved = true;
                                break;
                            }
                        }

                        if (resolved) continue;

                        // Strategy B: Find another user in this same week who has surplus off days
                        var peerWithSurplus = users
                            .Where(u => u.Id != user.Id && weekDates.Count(d => chromosome[(u.Id, d)] == null) > targetOffThisWeek)
                            .FirstOrDefault();

                        if (peerWithSurplus != null)
                        {
                            var candidateDate = weekDates.FirstOrDefault(d => 
                                chromosome[(user.Id, d)] != null && 
                                chromosome[(peerWithSurplus.Id, d)] == null &&
                                !leaves.Any(l => l.UserId == peerWithSurplus.Id && d.Date >= l.RequestDate.Date && d.Date <= l.RequestDate.Date));

                            if (candidateDate != default)
                            {
                                var shiftId = chromosome[(user.Id, candidateDate)];
                                chromosome[(user.Id, candidateDate)] = null;
                                chromosome[(peerWithSurplus.Id, candidateDate)] = shiftId;
                                currentOff++;
                                resolved = true;
                                continue;
                            }
                        }

                        // Strategy C: Swap with an off peer on day d
                        foreach (var d in weekDates)
                        {
                            var shiftId = chromosome[(user.Id, d)];
                            if (shiftId == null) continue;

                            var offPeer = users.FirstOrDefault(u => 
                                u.Id != user.Id && 
                                chromosome[(u.Id, d)] == null &&
                                !leaves.Any(l => l.UserId == u.Id && d.Date >= l.RequestDate.Date && d.Date <= l.RequestDate.Date));

                            if (offPeer != null)
                            {
                                chromosome[(user.Id, d)] = null;
                                chromosome[(offPeer.Id, d)] = shiftId;
                                currentOff++;
                                resolved = true;
                                break;
                            }
                        }

                        if (!resolved) break;
                    }
                }

                // 2. Normalize users with surplus off days in this week (if any user has more off days than target and no approved leave)
                foreach (var user in users)
                {
                    int currentOff = weekDates.Count(d => chromosome[(user.Id, d)] == null);
                    while (currentOff > targetOffThisWeek)
                    {
                        var candidateOffDays = weekDates.Where(d => 
                            chromosome[(user.Id, d)] == null && 
                            !leaves.Any(l => l.UserId == user.Id && d.Date >= l.RequestDate.Date && d.Date <= l.RequestDate.Date)).ToList();

                        if (!candidateOffDays.Any()) break;

                        var peerInNeed = users.FirstOrDefault(u => u.Id != user.Id && weekDates.Count(d => chromosome[(u.Id, d)] == null) < targetOffThisWeek);
                        if (peerInNeed != null)
                        {
                            var swapDate = candidateOffDays.FirstOrDefault(d => chromosome[(peerInNeed.Id, d)] != null);
                            if (swapDate != default)
                            {
                                var shiftId = chromosome[(peerInNeed.Id, swapDate)];
                                chromosome[(peerInNeed.Id, swapDate)] = null;
                                chromosome[(user.Id, swapDate)] = shiftId;
                                currentOff--;
                                continue;
                            }
                        }
                        break;
                    }
                }
            }
        }

        private void EqualizeEmployeeShifts(
            Dictionary<(int, DateTime), int?> chromosome,
            List<User> users,
            List<DateTime> dates,
            List<ShiftType> shifts,
            IEnumerable<LeaveRequest> leaves,
            int maxConsecutiveDays,
            int maxOffDays,
            bool preventNightToMorning,
            int minSenior,
            int? nightShiftId)
        {
            bool IsNight(int? sId) => nightShiftId.HasValue && sId == nightShiftId.Value;
            var daytimeShifts = shifts.Where(s => !IsNight(s.Id)).ToList();
            if (!daytimeShifts.Any()) daytimeShifts = shifts;

            // Pass 1: Enforce weekly off days strictly first
            EnforceWeeklyOffDays(chromosome, users, dates, shifts, leaves, maxOffDays, preventNightToMorning, nightShiftId);

            // Pass 2: Balanced swap between employees across the period without breaking weekly off rule (minimum 1 off per week)
            for (int iter = 0; iter < 100; iter++)
            {
                var shiftCounts = users.ToDictionary(u => u.Id, u => dates.Count(d => chromosome[(u.Id, d)] != null));
                int maxCount = shiftCounts.Values.Max();
                int minCount = shiftCounts.Values.Min();

                if (maxCount - minCount <= 1) break; // Balanced

                var maxUser = users.First(u => shiftCounts[u.Id] == maxCount);
                var minUser = users.First(u => shiftCounts[u.Id] == minCount);

                bool swapped = false;
                foreach (var date in dates)
                {
                    var maxShiftId = chromosome[(maxUser.Id, date)];
                    var minShiftId = chromosome[(minUser.Id, date)];

                    if (maxShiftId != null && minShiftId == null)
                    {
                        // Check if minUser has approved leave on this date
                        if (leaves.Any(l => l.UserId == minUser.Id && date >= l.RequestDate.Date && date <= l.RequestDate.Date))
                            continue;

                        // Check weekly constraint: minUser must not end up with 0 off days in their week!
                        int weekIdx = dates.IndexOf(date) / 7;
                        var weekDates = dates.Skip(weekIdx * 7).Take(7).ToList();
                        int minUserOffInWeek = weekDates.Count(d => chromosome[(minUser.Id, d)] == null);
                        if (minUserOffInWeek <= 1)
                        {
                            // If minUser only has 1 off day in this week, taking away this off day would leave them with 0 off days!
                            // Strictly do not allow 0 off days in any week!
                            continue;
                        }

                        var shift = shifts.FirstOrDefault(s => s.Id == maxShiftId);
                        if (shift == null) continue;

                        // Check dynamic minSenior constraint
                        if (maxUser.Level == "Senior" && minUser.Level != "Senior" && minSenior > 0)
                        {
                            int otherSeniors = users.Count(u => u.Id != maxUser.Id && u.Level == "Senior" && chromosome[(u.Id, date)] == shift.Id);
                            if (otherSeniors < minSenior) continue;
                        }

                        // Check night-to-morning rule for minUser
                        if (preventNightToMorning)
                        {
                            if (date > dates.First())
                            {
                                var prevDate = date.AddDays(-1);
                                var prevShiftId = chromosome[(minUser.Id, prevDate)];
                                if (prevShiftId != null && IsNight(prevShiftId) && !IsNight(shift.Id)) continue;
                            }
                            if (date < dates.Last())
                            {
                                var nextDate = date.AddDays(1);
                                var nextShiftId = chromosome[(minUser.Id, nextDate)];
                                if (nextShiftId != null && IsNight(shift.Id) && !IsNight(nextShiftId)) continue;
                            }
                        }

                        // Execute balanced swap
                        chromosome[(maxUser.Id, date)] = null;
                        chromosome[(minUser.Id, date)] = maxShiftId;
                        swapped = true;
                        break;
                    }
                }

                if (!swapped) break;
            }

            // Final pass: Re-verify weekly off-days guarantee
            EnforceWeeklyOffDays(chromosome, users, dates, shifts, leaves, maxOffDays, preventNightToMorning, nightShiftId);
        }

        private Dictionary<(int, DateTime), int?> CloneChromosome(Dictionary<(int, DateTime), int?> c)
        {
            return c.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private Dictionary<(int, DateTime), int?> TournamentSelection(List<Dictionary<(int, DateTime), int?>> pop, List<double> fitnesses)
        {
            int bestIdx = _random.Next(pop.Count);
            for (int i = 1; i < 5; i++)
            {
                int idx = _random.Next(pop.Count);
                if (fitnesses[idx] < fitnesses[bestIdx]) bestIdx = idx;
            }
            return CloneChromosome(pop[bestIdx]);
        }

        private Dictionary<(int, DateTime), int?> Crossover(
            Dictionary<(int, DateTime), int?> p1, 
            Dictionary<(int, DateTime), int?> p2,
            List<User> users,
            List<DateTime> dates)
        {
            var child = new Dictionary<(int, DateTime), int?>();
            int numWeeks = Math.Max(1, dates.Count / 7);

            foreach (var user in users)
            {
                for (int w = 0; w < numWeeks; w++)
                {
                    var weekDates = dates.Skip(w * 7).Take(7).ToList();
                    bool useP1 = _random.NextDouble() < 0.5;
                    foreach (var d in weekDates)
                    {
                        child[(user.Id, d)] = useP1 ? p1[(user.Id, d)] : p2[(user.Id, d)];
                    }
                }
            }

            for (int i = numWeeks * 7; i < dates.Count; i++)
            {
                var d = dates[i];
                foreach (var user in users)
                {
                    child[(user.Id, d)] = _random.NextDouble() < 0.5 ? p1[(user.Id, d)] : p2[(user.Id, d)];
                }
            }

            return child;
        }

        private void Mutate(
            Dictionary<(int, DateTime), int?> child, 
            List<User> users, 
            List<DateTime> dates, 
            List<ShiftType> shifts, 
            IEnumerable<LeaveRequest> leaves,
            int? nightShiftId)
        {
            int numWeeks = Math.Max(1, dates.Count / 7);
            double roll = _random.NextDouble();

            if (roll < 0.5 && numWeeks > 0)
            {
                // Intra-week off-day swap: swaps an off day with a work day within the same week
                int w = _random.Next(numWeeks);
                var weekDates = dates.Skip(w * 7).Take(7).ToList();
                var user = users[_random.Next(users.Count)];

                var offDays = weekDates.Where(d => child[(user.Id, d)] == null && !leaves.Any(l => l.UserId == user.Id && d.Date >= l.RequestDate.Date && d.Date <= l.RequestDate.Date)).ToList();
                var workDays = weekDates.Where(d => child[(user.Id, d)] != null).ToList();

                if (offDays.Any() && workDays.Any())
                {
                    var offD = offDays[_random.Next(offDays.Count)];
                    var workD = workDays[_random.Next(workDays.Count)];
                    var shiftId = child[(user.Id, workD)];

                    var shift = shifts.FirstOrDefault(s => s.Id == shiftId);
                    int workingStaff = users.Count(u => child[(u.Id, workD)] == shiftId);
                    if (shift == null || workingStaff > shift.MinStaff)
                    {
                        child[(user.Id, offD)] = shiftId;
                        child[(user.Id, workD)] = null;
                    }
                }
            }
            else
            {
                // Swap shifts between two working users on the same date
                var date = dates[_random.Next(dates.Count)];
                var workingUsers = users.Where(u => child[(u.Id, date)] != null).ToList();
                if (workingUsers.Count >= 2)
                {
                    var u1 = workingUsers[_random.Next(workingUsers.Count)];
                    var u2 = workingUsers[_random.Next(workingUsers.Count)];
                    if (u1.Id != u2.Id)
                    {
                        var temp = child[(u1.Id, date)];
                        child[(u1.Id, date)] = child[(u2.Id, date)];
                        child[(u2.Id, date)] = temp;
                    }
                }
            }
        }
    }
}
