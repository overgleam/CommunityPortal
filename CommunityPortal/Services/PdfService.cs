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
            // Generate PDF document
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(50);

                    page.Header().Element(container => CreateHeaderElement(viewModel, container));
                    page.Content().Element(container => CreateContentElement(viewModel, container));
                    page.Footer().Element(CreateFooterElement);
                });
            }).GeneratePdf();
        }

        private void CreateHeaderElement(BillDetailsViewModel viewModel, IContainer container)
        {
            container.Row(row =>
            {
                // Logo or community name
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Community Portal").Bold().FontSize(24);
                    column.Item().Text($"Invoice #{viewModel.Bill.Id}").FontSize(14);
                });

                // Bill information
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("BILL").Bold().FontSize(20);
                    column.Item().Text($"Date: {viewModel.Bill.BillingDate:MMMM dd, yyyy}");
                    column.Item().Text($"Due Date: {viewModel.Bill.DueDate:MMMM dd, yyyy}");
                    column.Item().Text($"Status: {viewModel.Bill.Status}").Bold();
                });
            });
        }

        private void CreateContentElement(BillDetailsViewModel viewModel, IContainer container)
        {
            container.Column(column =>
            {
                // Client and billing information section
                column.Item().Element(c => CreateBillingInfoSection(viewModel, c));
                column.Item().PaddingTop(10);

                // Bill items section
                column.Item().Element(c => CreateBillItemsSection(viewModel, c));
                column.Item().PaddingTop(10);

                // Payment summary
                column.Item().Element(c => CreateSummarySection(viewModel, c));

                // Payment history if any
                if (viewModel.Payments.Count > 0)
                {
                    column.Item().PaddingTop(20);
                    column.Item().Element(c => CreatePaymentHistorySection(viewModel, c));
                }

                // Notes if any
                if (!string.IsNullOrEmpty(viewModel.Bill.Notes))
                {
                    column.Item().PaddingTop(20);
                    column.Item().Element(c => CreateNotesSection(viewModel, c));
                }
            });
        }

        private void CreateBillingInfoSection(BillDetailsViewModel viewModel, IContainer container)
        {
            container.Row(row =>
            {
                // Billing info
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Billing Information").Bold().FontSize(14);
                    column.Item().Text($"Bill #: {viewModel.Bill.Id}");
                    column.Item().Text($"Billing Period: {viewModel.Bill.BillingPeriod}");
                    if (viewModel.Bill.Status == "Paid" && viewModel.Bill.PaidDate.HasValue)
                    {
                        column.Item().Text($"Paid Date: {viewModel.Bill.PaidDate.Value:MMMM dd, yyyy}");
                    }
                });

                // Homeowner info
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Homeowner Information").Bold().FontSize(14);
                    column.Item().Text($"Name: {viewModel.Homeowner.FirstName} {viewModel.Homeowner.LastName}");
                    column.Item().Text($"Address: Block {viewModel.Homeowner.BlockNumber}, House {viewModel.Homeowner.HouseNumber}");
                    column.Item().Text($"Full Address: {viewModel.Homeowner.Address}");
                });
            });
        }

        private void CreateBillItemsSection(BillDetailsViewModel viewModel, IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Text("Bill Items").Bold().FontSize(14);
                column.Item().Table(table =>
                {
                    // Define columns
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);  // Description
                        columns.RelativeColumn(2);  // Fee Type
                        columns.RelativeColumn(1);  // Amount
                        columns.RelativeColumn(3);  // Notes
                    });

                    // Add header row
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten3).Text("Description").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Text("Fee Type").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Text("Amount").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Text("Notes").Bold();
                    });

                    // Add data rows
                    foreach (var item in viewModel.BillItems)
                    {
                        table.Cell().Text(item.Description);
                        table.Cell().Text(item.FeeType.Name);
                        table.Cell().Text(item.Amount.ToString("C2"));
                        table.Cell().Text(string.IsNullOrEmpty(item.Notes) ? "-" : item.Notes);
                    }

                    // Add total row
                    table.Cell().Text("");
                    table.Cell().Text("Total:").Bold();
                    table.Cell().Text(viewModel.Bill.TotalAmount.ToString("C2")).Bold();
                    table.Cell().Text("");
                });
            });
        }

        private void CreateSummarySection(BillDetailsViewModel viewModel, IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Text("Payment Summary").Bold().FontSize(14);
                column.Item().Grid(grid =>
                {
                    grid.Columns(2);
                    grid.Item().Text("Total Amount:").Bold();
                    grid.Item().AlignRight().Text(viewModel.Bill.TotalAmount.ToString("C2"));
                    
                    grid.Item().Text("Paid Amount:").Bold();
                    grid.Item().AlignRight().Text(viewModel.Bill.PaidAmount.ToString("C2"));
                    
                    grid.Item().Text("Balance:").Bold();
                    grid.Item().AlignRight().Text(viewModel.Bill.BalanceAmount.ToString("C2"));
                    
                    if (viewModel.Bill.IsPenaltyApplied)
                    {
                        grid.Item().Text("Penalty:").Bold();
                        grid.Item().AlignRight().Text(viewModel.Bill.PenaltyAmount.ToString("C2"));
                    }
                });
            });
        }

        private void CreatePaymentHistorySection(BillDetailsViewModel viewModel, IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Text("Payment History").Bold().FontSize(14);
                column.Item().Table(table =>
                {
                    // Define columns
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);  // Date
                        columns.RelativeColumn(1);  // Amount
                        columns.RelativeColumn(2);  // Method
                        columns.RelativeColumn(2);  // Reference
                        columns.RelativeColumn(1);  // Status
                    });

                    // Add header row
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Grey.Lighten3).Text("Date").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Text("Amount").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Text("Method").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Text("Reference").Bold();
                        header.Cell().Background(Colors.Grey.Lighten3).Text("Status").Bold();
                    });

                    // Add data rows
                    foreach (var payment in viewModel.Payments)
                    {
                        table.Cell().Text(payment.PaymentDate.ToString("MMM dd, yyyy"));
                        table.Cell().Text(payment.Amount.ToString("C2"));
                        table.Cell().Text(payment.PaymentMethod.Name);
                        table.Cell().Text(payment.TransactionReference);
                        table.Cell().Text(payment.Status);
                    }
                });
            });
        }

        private void CreateNotesSection(BillDetailsViewModel viewModel, IContainer container)
        {
            container.Column(column =>
            {
                column.Item().Text("Notes").Bold().FontSize(14);
                column.Item().Background(Colors.Grey.Lighten5).Padding(5).Text(viewModel.Bill.Notes);
            });
        }

        private void CreateFooterElement(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(text =>
                    {
                        text.Span("Page ").FontSize(10);
                        text.CurrentPageNumber().FontSize(10);
                        text.Span(" of ").FontSize(10);
                        text.TotalPages().FontSize(10);
                    });
                });
                row.RelativeItem().Column(column =>
                {
                    column.Item().AlignRight().Text("Generated on " + DateTime.Now.ToString("yyyy-MM-dd")).FontSize(10);
                });
            });
        }
    }
} 