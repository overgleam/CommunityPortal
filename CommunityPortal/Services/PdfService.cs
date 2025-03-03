using CommunityPortal.Models.Billing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CommunityPortal.Services
{
    public class PdfService
    {
        public PdfService()
        {
            // Set license type to Community (free for open-source and personal use)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateBillPdf(BillDetailsViewModel viewModel)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(35);
                    page.Background(Colors.White);
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10));

                    // Create the PDF structure
                    page.Header().Element(x => CreateHeader(x, viewModel));
                    page.Content().Element(x => CreateContent(x, viewModel));
                    page.Footer().Element(x => CreateFooter(x));
                });
            }).GeneratePdf();
        }

        // Simple header with clear styling
        private void CreateHeader(IContainer container, BillDetailsViewModel viewModel)
        {
            container.Padding(10).Border(1).BorderColor(Colors.Grey.Lighten1).Row(row =>
            {
                // Logo and company details
                row.RelativeItem(2).Column(column =>
                {
                    column.Item().Text("Community Portal").Bold().FontSize(20);
                    column.Item().Text($"Invoice #{viewModel.Bill.Id}").FontSize(12);
                    column.Item().Text("123 Community Street");
                    column.Item().Text("Community City, State 12345");
                    column.Item().Text("admin@communityportal.com");
                });

                // Invoice details
                row.RelativeItem().Column(column =>
                {
                    column.Item().AlignRight().Text("INVOICE").Bold().FontSize(20);
                    column.Item().AlignRight().Text(viewModel.Bill.Status).Bold();
                    
                    column.Item().Height(10); // Spacer
                    
                    column.Item().AlignRight().Text("Issue Date:").Bold();
                    column.Item().AlignRight().Text($"{viewModel.Bill.BillingDate:MM/dd/yyyy}");
                    
                    column.Item().AlignRight().Text("Due Date:").Bold();
                    column.Item().AlignRight().Text($"{viewModel.Bill.DueDate:MM/dd/yyyy}");
                    
                    if (viewModel.Bill.PaidDate.HasValue)
                    {
                        column.Item().AlignRight().Text("Paid Date:").Bold();
                        column.Item().AlignRight().Text($"{viewModel.Bill.PaidDate:MM/dd/yyyy}");
                    }
                });
            });
        }

        // Organized content with improved readability
        private void CreateContent(IContainer container, BillDetailsViewModel viewModel)
        {
            container.PaddingVertical(10).Column(column =>
            {
                // Billing details section
                column.Item().PaddingBottom(10).Row(row =>
                {
                    // Bill to information
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
                    {
                        col.Item().Text("BILL TO").Bold();
                        col.Item().Text(viewModel.Homeowner.User.FullName);
                        col.Item().Text($"Block {viewModel.Homeowner.BlockNumber}, House {viewModel.Homeowner.HouseNumber}");
                        col.Item().Text(viewModel.Homeowner.Address);
                        col.Item().Text(viewModel.Homeowner.User.Email);
                    });
                    
                    // Spacing
                    row.ConstantItem(10);
                    
                    // Payment information
                    row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(col =>
                    {
                        col.Item().Text("PAYMENT INFO").Bold();
                        col.Item().Text($"Bill Number: {viewModel.Bill.Id}");
                        col.Item().Text($"Billing Period: {viewModel.Bill.BillingPeriod}");
                        col.Item().Text($"Total Amount: {viewModel.Bill.TotalAmount:C2}");
                        col.Item().Text($"Balance Due: {viewModel.Bill.BalanceAmount:C2}");
                    });
                });
                
                // Bill Items Table
                column.Item().Element(e => CreateBillItemsTable(e, viewModel));
                
                // Payment Summary
                column.Item().PaddingTop(10).Element(e => CreateSummary(e, viewModel));
                
                // Payment History if available
                if (viewModel.Payments.Count > 0)
                {
                    column.Item().PaddingTop(10).Element(e => CreatePaymentHistory(e, viewModel));
                }
                
                // Notes if available
                if (!string.IsNullOrEmpty(viewModel.Bill.Notes))
                {
                    column.Item().PaddingTop(10).Element(e => CreateNotes(e, viewModel));
                }
                
                // Payment Instructions
                column.Item().PaddingTop(20).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Column(c =>
                {
                    c.Item().Text("PAYMENT INSTRUCTIONS").Bold();
                    c.Item().Text("Please make payments to Community Portal HOA, Account #1234567890");
                    c.Item().Text($"Reference: INV-{viewModel.Bill.Id}");
                    c.Item().Text("For inquiries, contact billing@communityportal.com or (555) 123-4567");
                });
            });
        }

        // Clean bill items table
        private void CreateBillItemsTable(IContainer container, BillDetailsViewModel viewModel)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten1).Column(column =>
            {
                // Table header
                column.Item().Background(Colors.Blue.Medium).Padding(5).Row(row =>
                {
                    row.RelativeItem(3).Text("Description").Bold().FontColor(Colors.White);
                    row.RelativeItem(2).Text("Fee Type").Bold().FontColor(Colors.White);
                    row.RelativeItem(1).AlignRight().Text("Amount").Bold().FontColor(Colors.White);
                    row.RelativeItem(2).Text("Notes").Bold().FontColor(Colors.White);
                });
                
                // Table rows
                bool isAlternate = false;
                foreach (var item in viewModel.BillItems)
                {
                    var background = isAlternate ? Colors.Grey.Lighten3 : Colors.White;
                    
                    column.Item().Background(background).Padding(5).Row(row =>
                    {
                        row.RelativeItem(3).Text(item.Description);
                        row.RelativeItem(2).Text(item.FeeType.Name);
                        row.RelativeItem(1).AlignRight().Text(item.Amount.ToString("C2"));
                        row.RelativeItem(2).Text(string.IsNullOrEmpty(item.Notes) ? "-" : item.Notes);
                    });
                    
                    isAlternate = !isAlternate;
                }
            });
        }

        // Clear payment summary
        private void CreateSummary(IContainer container, BillDetailsViewModel viewModel)
        {
            container.AlignRight().MinWidth(200).Border(1).BorderColor(Colors.Grey.Lighten1).Column(column =>
            {
                column.Item().Background(Colors.Blue.Medium).Padding(5).Text("SUMMARY").Bold().FontColor(Colors.White);
                
                column.Item().Padding(5).Grid(grid =>
                {
                    grid.Columns(2);
                    grid.Item().Text("Total Amount:").Bold();
                    grid.Item().AlignRight().Text(viewModel.Bill.TotalAmount.ToString("C2"));
                    
                    grid.Item().Text("Paid Amount:").Bold();
                    grid.Item().AlignRight().Text(viewModel.Bill.PaidAmount.ToString("C2"));
                    
                    if (viewModel.Bill.IsPenaltyApplied)
                    {
                        grid.Item().Text("Penalty:").Bold();
                        grid.Item().AlignRight().Text(viewModel.Bill.PenaltyAmount.ToString("C2"));
                    }
                    
                    grid.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten1).Text("Amount Due:").Bold();
                    grid.Item().BorderTop(1).BorderColor(Colors.Grey.Lighten1).AlignRight().Text(viewModel.Bill.BalanceAmount.ToString("C2")).Bold();
                });
            });
        }

        // Clean payment history table
        private void CreatePaymentHistory(IContainer container, BillDetailsViewModel viewModel)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten1).Column(column =>
            {
                column.Item().Background(Colors.Blue.Medium).Padding(5).Text("PAYMENT HISTORY").Bold().FontColor(Colors.White);
                
                // Table headers
                column.Item().Background(Colors.Grey.Lighten3).Padding(5).Row(row =>
                {
                    row.RelativeItem(2).Text("Date").Bold();
                    row.RelativeItem(2).Text("Amount").Bold();
                    row.RelativeItem(2).Text("Method").Bold();
                    row.RelativeItem(3).Text("Reference").Bold();
                    row.RelativeItem(1).Text("Status").Bold();
                });
                
                // Payment rows
                bool isAlternate = false;
                foreach (var payment in viewModel.Payments)
                {
                    var background = isAlternate ? Colors.Grey.Lighten3 : Colors.White;
                    
                    column.Item().Background(background).Padding(5).Row(row =>
                    {
                        row.RelativeItem(2).Text(payment.PaymentDate.ToString("MM/dd/yyyy"));
                        row.RelativeItem(2).Text(payment.Amount.ToString("C2"));
                        row.RelativeItem(2).Text(payment.PaymentMethod.Name);
                        row.RelativeItem(3).Text(payment.TransactionReference);
                        row.RelativeItem(1).Text(payment.Status);
                    });
                    
                    isAlternate = !isAlternate;
                }
            });
        }

        // Simple notes section
        private void CreateNotes(IContainer container, BillDetailsViewModel viewModel)
        {
            container.Border(1).BorderColor(Colors.Grey.Lighten1).Column(column =>
            {
                column.Item().Background(Colors.Blue.Medium).Padding(5).Text("NOTES").Bold().FontColor(Colors.White);
                column.Item().Padding(10).Text(viewModel.Bill.Notes);
            });
        }

        // Simple footer with company information
        private void CreateFooter(IContainer container)
        {
            container.BorderTop(1).BorderColor(Colors.Grey.Lighten1).Padding(10).Row(row =>
            {
                row.RelativeItem(3).Column(column =>
                {
                    column.Item().Text("THANK YOU FOR YOUR BUSINESS").Bold();
                    column.Item().Text("Payment is due within 30 days of the issue date.");
                });
                
                row.RelativeItem(1).Column(column =>
                {
                    column.Item().Text(DateTime.Now.ToString("MM/dd/yyyy")).FontSize(8);
                });
            });
        }
    }
} 