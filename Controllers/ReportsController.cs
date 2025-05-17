using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Expense_Tracker.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Collections.Generic;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text.Json;

namespace Expense_Tracker.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var now = DateTime.Now;
            var startOfMonth = new DateTime(now.Year, now.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            var expenses = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Category.Type == "Expense" && t.Date >= startOfMonth && t.Date <= endOfMonth)
                .ToListAsync();

            var categorySummary = expenses
                .GroupBy(t => t.Category.Title)
                .Select(g => new
                {
                    Category = g.Key,
                    Total = g.Sum(t => t.Amount)
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            var totalExpenses = categorySummary.Sum(x => x.Total);

            ViewBag.PieLabels = categorySummary.Select(x => x.Category).ToArray();
            ViewBag.PieData = categorySummary.Select(x => x.Total).ToArray();
            ViewBag.CategorySummary = categorySummary.Select(x => new {
                x.Category,
                x.Total,
                Percent = totalExpenses > 0 ? (x.Total * 100.0 / totalExpenses) : 0
            }).ToList();
            ViewBag.TotalExpenses = totalExpenses;
            ViewBag.Month = now.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetReportData(DateTime start, DateTime end, string summaryType = "Monthly")
        {
            var userId = _userManager.GetUserId(User);
            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= start && t.Date <= end)
                .ToListAsync();

            // Summary cards
            var totalIncome = transactions.Where(t => t.Category.Type == "Income").Sum(t => t.Amount);
            var totalExpense = transactions.Where(t => t.Category.Type == "Expense").Sum(t => t.Amount);
            var net = totalIncome - totalExpense;

            // Pie chart (expenses by category)
            var pie = transactions
                .Where(t => t.Category.Type == "Expense")
                .GroupBy(t => t.Category.Title)
                .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.Total)
                .ToList();

            // Income vs Expense line chart
            var lineLabels = new List<string>();
            var incomeData = new List<int>();
            var expenseData = new List<int>();
            if (summaryType == "Monthly")
            {
                // Group by day
                var days = Enumerable.Range(0, (end - start).Days + 1)
                    .Select(i => start.AddDays(i)).ToList();
                foreach (var day in days)
                {
                    lineLabels.Add(day.ToString("MMM d"));
                    incomeData.Add(transactions.Where(t => t.Category.Type == "Income" && t.Date.Date == day.Date).Sum(t => t.Amount));
                    expenseData.Add(transactions.Where(t => t.Category.Type == "Expense" && t.Date.Date == day.Date).Sum(t => t.Amount));
                }
            }
            else if (summaryType == "Weekly")
            {
                // Group by week
                var weekGroups = transactions.GroupBy(t => CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(t.Date, CalendarWeekRule.FirstDay, DayOfWeek.Monday));
                foreach (var g in weekGroups.OrderBy(g => g.Key))
                {
                    lineLabels.Add($"Week {g.Key}");
                    incomeData.Add(g.Where(t => t.Category.Type == "Income").Sum(t => t.Amount));
                    expenseData.Add(g.Where(t => t.Category.Type == "Expense").Sum(t => t.Amount));
                }
            }
            else if (summaryType == "Yearly")
            {
                // Group by month
                var monthGroups = transactions.GroupBy(t => new { t.Date.Year, t.Date.Month });
                foreach (var g in monthGroups.OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month))
                {
                    lineLabels.Add($"{CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(g.Key.Month)} {g.Key.Year}");
                    incomeData.Add(g.Where(t => t.Category.Type == "Income").Sum(t => t.Amount));
                    expenseData.Add(g.Where(t => t.Category.Type == "Expense").Sum(t => t.Amount));
                }
            }

            // Top expenses
            var topExpenses = transactions
                .Where(t => t.Category.Type == "Expense")
                .OrderByDescending(t => t.Amount)
                .Take(5)
                .Select(t => new { t.Date, Category = t.Category.Title, t.Note, t.Amount })
                .ToList();

            // Budget comparison
            var budgets = await _context.Budgets.Where(b => b.UserId == userId).ToListAsync();
            var budgetComparison = pie.Select(x => new {
                Category = x.Category,
                Actual = x.Total,
                Budget = budgets.FirstOrDefault(b => b.CategoryId == _context.Categories.FirstOrDefault(c => c.Title == x.Category && c.UserId == userId).CategoryId)?.Amount ?? 0
            }).ToList();

            return Json(new
            {
                totalIncome,
                totalExpense,
                net,
                pieLabels = pie.Select(x => x.Category).ToArray(),
                pieData = pie.Select(x => x.Total).ToArray(),
                lineLabels,
                incomeData,
                expenseData,
                topExpenses,
                budgetComparison
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetBudgets()
        {
            var userId = _userManager.GetUserId(User);
            var budgets = await _context.Budgets.Include(b => b.CategoryId).Where(b => b.UserId == userId).ToListAsync();
            var categories = await _context.Categories.Where(c => c.UserId == userId).ToListAsync();
            return Json(categories.Select(c => new {
                c.CategoryId,
                c.Title,
                Budget = budgets.FirstOrDefault(b => b.CategoryId == c.CategoryId)?.Amount ?? 0
            }));
        }

        [HttpPost]
        public async Task<IActionResult> SetBudget(int categoryId, int amount)
        {
            var userId = _userManager.GetUserId(User);
            var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == categoryId);
            if (budget == null)
            {
                budget = new Budget { UserId = userId, CategoryId = categoryId, Amount = amount };
                _context.Budgets.Add(budget);
            }
            else
            {
                budget.Amount = amount;
                _context.Budgets.Update(budget);
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(DateTime start, DateTime end, string summaryType = "Monthly")
        {
            var userId = _userManager.GetUserId(User);
            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= start && t.Date <= end)
                .OrderBy(t => t.Date)
                .ToListAsync();
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Date,Type,Category,Note,Amount");
            foreach (var t in transactions)
            {
                csv.AppendLine($"{t.Date:yyyy-MM-dd},{t.Category.Type},{t.Category.Title},\"{(t.Note ?? "").Replace("\"", "\"\"")}\",{t.Amount}");
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"ExpenseReport_{start:yyyyMMdd}_{end:yyyyMMdd}.csv");
        }

        [HttpPost]
        public async Task<IActionResult> ExportPdf(DateTime start, DateTime end, [FromForm] string? pieChartBase64 = null, [FromForm] string? lineChartBase64 = null, [FromForm] string? pieLabels = null, [FromForm] string? pieColors = null)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);
            var transactions = await _context.Transactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId && t.Date >= start && t.Date <= end)
                .OrderBy(t => t.Date)
                .ToListAsync();
            var totalIncome = transactions.Where(t => t.Category.Type == "Income").Sum(t => t.Amount);
            var totalExpense = transactions.Where(t => t.Category.Type == "Expense").Sum(t => t.Amount);
            var net = totalIncome - totalExpense;

            // Parse pieLabels and pieColors from JSON
            List<string> legendLabels = new List<string>();
            List<string> legendColors = new List<string>();
            if (!string.IsNullOrEmpty(pieLabels) && !string.IsNullOrEmpty(pieColors))
            {
                legendLabels = JsonSerializer.Deserialize<List<string>>(pieLabels);
                legendColors = JsonSerializer.Deserialize<List<string>>(pieColors);
            }

            // Decode chart images if provided
            byte[]? pieBytes = null, lineBytes = null;
            if (!string.IsNullOrEmpty(pieChartBase64) && pieChartBase64.StartsWith("data:image"))
            {
                var base64 = pieChartBase64.Substring(pieChartBase64.IndexOf(",") + 1);
                pieBytes = Convert.FromBase64String(base64);
            }
            if (!string.IsNullOrEmpty(lineChartBase64) && lineChartBase64.StartsWith("data:image"))
            {
                var base64 = lineChartBase64.Substring(lineChartBase64.IndexOf(",") + 1);
                lineBytes = Convert.FromBase64String(base64);
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.Header().Column(col => {
                        col.Item().Text("Expense Tracker").SemiBold().FontSize(28).FontColor(Colors.Blue.Medium);
                        col.Item().Text($"Expense Report ({start:yyyy-MM-dd} to {end:yyyy-MM-dd})").FontSize(14).FontColor(Colors.Grey.Darken2);
                        if (user != null)
                            col.Item().Text($"User: {user.Name} {user.Surname}").FontSize(12).FontColor(Colors.Grey.Darken1);
                    });
                    page.Content().Column(col =>
                    {
                        // Summary section
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Padding(10).Background(Colors.Blue.Lighten4).Column(c =>
                            {
                                c.Item().Text("Total Income").FontSize(12);
                                c.Item().Text($"${totalIncome}").FontSize(16).Bold();
                            });
                            row.RelativeItem().Padding(10).Background(Colors.Red.Lighten4).Column(c =>
                            {
                                c.Item().Text("Total Expense").FontSize(12);
                                c.Item().Text($"${totalExpense}").FontSize(16).Bold();
                            });
                            row.RelativeItem().Padding(10).Background(Colors.Green.Lighten4).Column(c =>
                            {
                                c.Item().Text("Net Savings").FontSize(12);
                                c.Item().Text($"${net}").FontSize(16).Bold();
                            });
                        });
                        col.Item().PaddingVertical(10);
                        // Charts section (if provided)
                        if (lineBytes != null || pieBytes != null)
                        {
                            col.Item().Row(row =>
                            {
                                if (lineBytes != null)
                                    row.RelativeItem().Width(220).Height(140).Image(lineBytes).FitArea();
                                if (pieBytes != null)
                                    row.RelativeItem().Width(220).Height(140).Image(pieBytes).FitArea();
                            });
                            // Pie chart legend (from frontend)
                            if (legendLabels.Count > 0 && legendColors.Count == legendLabels.Count)
                            {
                                col.Item().PaddingTop(8).Row(legendRow =>
                                {
                                    for (int i = 0; i < legendLabels.Count; i++)
                                    {
                                        var color = legendColors[i];
                                        var label = legendLabels[i];
                                        legendRow.AutoItem().Row(itemRow =>
                                        {
                                            itemRow.AutoItem().Width(18).Height(12).Background(color);
                                            itemRow.AutoItem().PaddingLeft(4).Text(label).FontSize(10);
                                        });
                                        legendRow.AutoItem().PaddingRight(10);
                                    }
                                });
                            }
                            col.Item().PaddingVertical(10);
                        }
                        col.Item().Text("Transactions").Bold().FontSize(14);
                        col.Item().Height(400).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2); // Date
                                columns.RelativeColumn(1); // Type
                                columns.RelativeColumn(2); // Category
                                columns.RelativeColumn(3); // Note
                                columns.RelativeColumn(1); // Amount
                            });
                            table.Header(header =>
                            {
                                header.Cell().Text("Date").Bold();
                                header.Cell().Text("Type").Bold();
                                header.Cell().Text("Category").Bold();
                                header.Cell().Text("Note").Bold();
                                header.Cell().Text("Amount").Bold();
                            });
                            foreach (var t in transactions.Take(20)) // Limit for safety
                            {
                                table.Cell().Text(t.Date.ToString("yyyy-MM-dd"));
                                table.Cell().Text(t.Category.Type);
                                table.Cell().Text(t.Category.Title);
                                table.Cell().Text((t.Note ?? "").Length > 50 ? (t.Note.Substring(0, 50) + "...") : t.Note ?? "");
                                table.Cell().Text($"${t.Amount}");
                            }
                        });
                    });
                    page.Footer().AlignCenter().Text(x => x.Span("Generated by Expense Tracker").FontSize(10));
                });
            });
            var pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"ExpenseReport_{start:yyyyMMdd}_{end:yyyyMMdd}.pdf");
        }
    }
} 