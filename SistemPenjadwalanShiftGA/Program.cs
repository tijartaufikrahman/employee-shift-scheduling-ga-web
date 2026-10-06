using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SistemPenjadwalanShiftGA.Data;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Repositories;
using SistemPenjadwalanShiftGA.Services;

var builder = WebApplication.CreateBuilder(args);

// Forwarded Headers for Reverse Proxy (Nginx / Linux VPS)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// SQL Server Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "SisJad.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization();

// Register Repositories (Interface/Repository Pattern)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
builder.Services.AddScoped<IShiftTypeRepository, ShiftTypeRepository>();
builder.Services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();

// Register Services
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IScheduleService, ScheduleService>();
builder.Services.AddScoped<IPdfExportService, PdfExportService>();

var app = builder.Build();

// Pastikan folder upload lampiran dan penjadwalan_pdf tersedia
var uploadPath = Path.Combine(app.Environment.WebRootPath, "upload");
if (!Directory.Exists(uploadPath))
    Directory.CreateDirectory(uploadPath);

var schedulePdfPath = Path.Combine(app.Environment.WebRootPath, "penjadwalan_pdf");
if (!Directory.Exists(schedulePdfPath))
    Directory.CreateDirectory(schedulePdfPath);

// Enable Forwarded Headers first for Reverse Proxy
app.UseForwardedHeaders();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "verifyPdf",
    pattern: "Verify/Pdf/{code}",
    defaults: new { controller = "Verify", action = "Pdf" });

app.MapControllerRoute(
    name: "verify",
    pattern: "Verify/{code}",
    defaults: new { controller = "Verify", action = "Index" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Sanitize historical PeriodName records if any contain (X Minggu)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dirtyPeriods = db.SchedulePeriods.Where(p => p.PeriodName.Contains("Minggu")).ToList();
        if (dirtyPeriods.Any())
        {
            foreach (var p in dirtyPeriods)
            {
                p.PeriodName = System.Text.RegularExpressions.Regex.Replace(p.PeriodName, @"\s*\(\d+\s*Minggu\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            }
            db.SaveChanges();
        }

        // Ensure all periods have unique QrVerificationCode
        var nullQrPeriods = db.SchedulePeriods.Where(p => p.QrVerificationCode == null || p.QrVerificationCode == "").ToList();
        if (nullQrPeriods.Any())
        {
            foreach (var p in nullQrPeriods)
            {
                p.QrVerificationCode = Guid.NewGuid().ToString("N");
            }
            db.SaveChanges();
        }
    }
    catch
    {
        // Safe fallback
    }
}

app.Run();
