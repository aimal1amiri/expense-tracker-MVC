using System.ComponentModel.DataAnnotations;

namespace Expense_Tracker.Models
{
    public class Budget
    {
        [Key]
        public int BudgetId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public int Amount { get; set; }
    }
} 