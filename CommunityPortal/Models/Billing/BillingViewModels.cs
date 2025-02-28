using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CommunityPortal.Models.Billing
{
    public class BillDetailsViewModel
    {
        public Bill Bill { get; set; }
        public List<BillItem> BillItems { get; set; }
        public List<Payment> Payments { get; set; }
        public Homeowner Homeowner { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal RemainingBalance { get; set; }
        public string BillingPeriod { get; set; }
    }

    public class BillListViewModel
    {
        public List<Bill> Bills { get; set; }
        public PaginationInfo PaginationInfo { get; set; }
        public string SearchTerm { get; set; }
        public string SortBy { get; set; }
        public string SortDirection { get; set; }
        public string StatusFilter { get; set; }
        public string DateFilter { get; set; }
    }

    public class HomeownerBillingDashboardViewModel
    {
        public List<Bill> RecentBills { get; set; }
        public Bill CurrentBill { get; set; }
        public decimal TotalDue { get; set; }
        public decimal TotalPaid { get; set; }
        public int TotalBills { get; set; }
        public int PaidBills { get; set; }
        public int OverdueBills { get; set; }
    }

    public class AdminBillingDashboardViewModel
    {
        public decimal TotalRevenue { get; set; }
        public decimal OutstandingAmount { get; set; }
        public decimal OverdueAmount { get; set; }
        public int TotalBills { get; set; }
        public int PaidBills { get; set; }
        public int OverdueBills { get; set; }
        public List<MonthlyRevenueViewModel> MonthlyRevenue { get; set; }
        public List<Bill> RecentBills { get; set; }
        public string SelectedPeriod { get; set; }
        public string PeriodLabel { get; set; }
    }

    public class MonthlyRevenueViewModel
    {
        public string Month { get; set; }
        public decimal Amount { get; set; }
    }

    public class CreateBillViewModel
    {
        [Required]
        public string HomeownerId { get; set; }
        
        [BindNever]
        public List<SelectListItem> Homeowners { get; set; }

        [Required]
        public DateTime BillingDate { get; set; } = DateTime.Today;

        [Required]
        public DateTime DueDate { get; set; } = DateTime.Today.AddDays(15);

        [Required]
        public string BillingPeriod { get; set; }

        public string Notes { get; set; }

        public List<BillItemViewModel> BillItems { get; set; } = new List<BillItemViewModel>();
    }

    public class EditBillViewModel
    {
        public int Id { get; set; }

        [Required]
        public string HomeownerId { get; set; }
        
        [BindNever]
        public List<SelectListItem> Homeowners { get; set; }

        [Required]
        public DateTime BillingDate { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        public string BillingPeriod { get; set; }

        public string Notes { get; set; }

        public List<BillItemViewModel> BillItems { get; set; } = new List<BillItemViewModel>();
    }

    public class BillItemViewModel
    {
        public int? Id { get; set; }

        [Required]
        public int FeeTypeId { get; set; }
        
        [BindNever]
        public List<SelectListItem> FeeTypes { get; set; }

        [Required]
        [StringLength(255)]
        public string Description { get; set; }

        [Required]
        [Range(0.01, 999999.99)]
        public decimal Amount { get; set; }

        public string Notes { get; set; }
    }

    public class CreatePaymentViewModel
    {
        [Required]
        public int BillId { get; set; }

        public string HomeownerId { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [Required]
        [Range(0.01, 999999.99)]
        public decimal Amount { get; set; }

        [Required]
        public int PaymentMethodId { get; set; }
        
        [BindNever]
        public List<SelectListItem> PaymentMethods { get; set; }

        [Required]
        [StringLength(100)]
        public string TransactionReference { get; set; }

        public string Notes { get; set; }

        [Required]
        public IFormFile PaymentProofImage { get; set; }
    }

    public class VerifyPaymentViewModel
    {
        public int PaymentId { get; set; }
        public int BillId { get; set; }
        public string HomeownerName { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string TransactionReference { get; set; }
        public string PaymentProofFile { get; set; }
        public string Status { get; set; }
    }

    public class EditBillingSettingsViewModel
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(255)]
        public string Description { get; set; }

        [Required]
        [Range(0, 100)]
        public decimal LateFeePercentage { get; set; }

        [Required]
        [Range(1, 180)]
        public int LateFeeDays { get; set; }

        [Required]
        [Range(1, 31)]
        public int BillingCycleDay { get; set; }

        [Required]
        [Range(1, 60)]
        public int PaymentDueDays { get; set; }
    }

    public class PaginationInfo
    {
        public int TotalItems { get; set; }
        public int ItemsPerPage { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages => (int)Math.Ceiling((decimal)TotalItems / ItemsPerPage);
    }
} 