using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using SistemPenjadwalanShiftGA.Interfaces;
using SistemPenjadwalanShiftGA.Models.ViewModels;
using System.Security.Claims;

namespace SistemPenjadwalanShiftGA.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IAuthService authService, ILogger<AccountController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToUserHome();

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
                return View(model);

            // model.Email digunakan sebagai identifier untuk login
            var user = await _authService.LoginAsync(model.Email, model.Password);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Email atau password salah. Silakan coba lagi.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.NamaLengkap),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role?.NamaRole ?? "Guest"),
                new Claim("RoleId", user.RoleId.ToString())
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var authProps = new AuthenticationProperties
            {
                IsPersistent = model.RememberMe,
                ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(30) : DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProps);
            _logger.LogInformation("User {Email} login berhasil", model.Email);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToUserHome();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToUserHome();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var resetBaseUrl = Url.Action("ResetPassword", "Account", null, Request.Scheme) ?? "";
            await _authService.ForgotPasswordAsync(model.Email, resetBaseUrl);

            TempData["ForgotPasswordSuccess"] = "Jika email terdaftar di sistem, link reset password telah dikirim.";
            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string? token = null)
        {
            if (string.IsNullOrEmpty(token))
                return RedirectToAction(nameof(Login));

            var isValid = await _authService.ValidateResetTokenAsync(token);
            if (!isValid)
            {
                TempData["ResetError"] = "Link reset password tidak valid atau telah kedaluwarsa. Silakan minta link baru.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var model = new ResetPasswordViewModel { Token = token };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _authService.ResetPasswordAsync(model.Token, model.NewPassword);
            if (!result)
            {
                ModelState.AddModelError(string.Empty, "Link reset password tidak valid atau telah kedaluwarsa.");
                return View(model);
            }

            TempData["ResetSuccess"] = "Password berhasil diubah! Silakan login dengan password baru Anda.";
            return RedirectToAction(nameof(Login));
        }

        private IActionResult RedirectToUserHome()
        {
            var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Karyawan";
            if (role == "Manager" || role == "Admin")
                return RedirectToAction("Dashboard", "Admin");
            return RedirectToAction("Index", "Home");
        }
    }
}
