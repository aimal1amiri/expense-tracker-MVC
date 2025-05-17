using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Expense_Tracker.Models;
using System.Threading.Tasks;
using System.Linq;

namespace Expense_Tracker.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public SettingsController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(string name, string surname, string email)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction("Index");
            }
            user.Name = name;
            user.Surname = surname;
            user.Email = email;
            user.UserName = email;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Profile updated successfully.";
                return RedirectToAction("Index");
            }
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                TempData["Error"] = "New passwords do not match.";
                return RedirectToAction("Index");
            }
            var user = await _userManager.GetUserAsync(User);
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Password changed successfully.";
                return RedirectToAction("Index");
            }
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> ExportData()
        {
            // Placeholder for export logic
            // You can implement CSV/Excel export here
            return File(new byte[0], "text/csv", "export.csv");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAccount()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();
            // Delete user-related data (transactions, categories)
            var transactions = _context.Transactions.Where(t => t.UserId == user.Id);
            var categories = _context.Categories.Where(c => c.UserId == user.Id);
            _context.Transactions.RemoveRange(transactions);
            _context.Categories.RemoveRange(categories);
            await _context.SaveChangesAsync();
            await _userManager.DeleteAsync(user);
            await _signInManager.SignOutAsync();
            return RedirectToAction("Landing", "Home");
        }
    }
} 