using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Expense_Tracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Expense_Tracker.Controllers
{
    [Authorize]
    public class CategoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CategoryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Category
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            return _context.Categories != null ?
                        View(await _context.Categories.Where(c => c.UserId == userId).ToListAsync()) :
                        Problem("Entity set 'ApplicationDbContext.Categories'  is null.");
        }

        // GET: Category/AddOrEdit
        public IActionResult AddOrEdit(int id = 0)
        {
            var userId = _userManager.GetUserId(User);
            if (id == 0)
                return View(new Category());
            else
            {
                var category = _context.Categories.FirstOrDefault(c => c.CategoryId == id && c.UserId == userId);
                if (category == null) return NotFound();
                return View(category);
            }
        }

        // POST: Category/AddOrEdit
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrEdit([Bind("CategoryId,Title,Icon,Type")] Category category)
        {
            var userId = _userManager.GetUserId(User);
            category.UserId = userId;
            if (ModelState.IsValid)
            {
                if (category.CategoryId == 0)
                    _context.Add(category);
                else
                {
                    var dbCategory = _context.Categories.AsNoTracking().FirstOrDefault(c => c.CategoryId == category.CategoryId && c.UserId == userId);
                    if (dbCategory == null) return NotFound();
                    _context.Update(category);
                }
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }

        // POST: Category/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = _userManager.GetUserId(User);
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == id && c.UserId == userId);
            if (category != null)
            {
                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
