using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using SHOPAPI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace SHOPAPI.Services
{
    public interface IPdfReportService
    {
        Task<byte[]> GenerateSingleOrderPdfAsync(Order order);
    }

    public class PdfReportService : IPdfReportService
    {
        private readonly string _reportPath;

        public PdfReportService()
        {
            // Đảm bảo đường dẫn tuyệt đối nếu chạy từ API (không có HttpContext.Current)
            _reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "SampleReport.rpt");
        }

        public async Task<byte[]> GenerateSingleOrderPdfAsync(Order order)
        {
            return await Task.Run(() =>
            {
                ReportDocument report = null;
                try
                {
                    if (!File.Exists(_reportPath))
                        throw new FileNotFoundException("Crystal report file not found.", _reportPath);

                    report = new ReportDocument();
                    report.Load(_reportPath);

                    // Set parameters cho report
                    report.SetParameterValue("OrderId", order.Id);
                    report.SetParameterValue("ReportTitle", $"HÓA ĐƠN ĐƠN HÀNG #{order.Id}");

                    // Tạo DataTable cho report
                    var reportData = CreateSingleOrderDataTable(order);
                    report.SetDataSource(reportData);

                    // Export to PDF
                    var exportOptions = new ExportOptions();
                    var pdfOptions = new PdfRtfWordFormatOptions();

                    exportOptions.ExportFormatType = ExportFormatType.PortableDocFormat;
                    exportOptions.FormatOptions = pdfOptions;
                    exportOptions.ExportDestinationType = ExportDestinationType.DiskFile;

                    var diskOptions = new DiskFileDestinationOptions();
                    var tempPath = Path.GetTempFileName() + ".pdf";
                    diskOptions.DiskFileName = tempPath;
                    exportOptions.DestinationOptions = diskOptions;

                    report.Export(exportOptions);

                    return File.ReadAllBytes(tempPath);
                }
                finally
                {
                    report?.Close();
                    report?.Dispose();
                }
            });
        }

        private System.Data.DataTable CreateSingleOrderDataTable(Order order)
        {
            var table = new System.Data.DataTable("OrderInvoiceData");

            table.Columns.Add("OrderId", typeof(int));
            table.Columns.Add("OrderDate", typeof(DateTime));
            table.Columns.Add("CustomerName", typeof(string));
            table.Columns.Add("PhoneNumber", typeof(string));
            table.Columns.Add("Email", typeof(string));
            table.Columns.Add("Address", typeof(string));
            table.Columns.Add("Note", typeof(string));
            table.Columns.Add("TotalPrice", typeof(long));
            table.Columns.Add("OrderStatus", typeof(string));
            table.Columns.Add("ProductName", typeof(string));
            table.Columns.Add("Quantity", typeof(int));
            table.Columns.Add("PriceAtPurchase", typeof(long));
            table.Columns.Add("LineTotal", typeof(long));

            if (order.OrderItems?.Any() == true)
            {
                foreach (var item in order.OrderItems)
                {
                    var row = table.NewRow();
                    row["OrderId"] = order.Id;
                    row["OrderDate"] = order.OrderDate;
                    row["CustomerName"] = order.Name ?? "";
                    row["PhoneNumber"] = order.PhoneNumber ?? "";
                    row["Email"] = order.Email ?? "";
                    row["Address"] = order.Address ?? "";
                    row["Note"] = order.Note ?? "";
                    row["TotalPrice"] = order.TotalPrice;
                    row["OrderStatus"] = order.orderStatus.ToString();
                    row["ProductName"] = item.Product?.Name ?? "";
                    row["Quantity"] = item.Quantity;
                    row["PriceAtPurchase"] = item.PriceAtPurchase;
                    row["LineTotal"] = item.PriceAtPurchase * item.Quantity;
                    table.Rows.Add(row);
                }
            }

            return table;
        }
    }
}
