using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.draw;
using SHOPAPI.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web;

namespace API.Services
{
    public class InvoiceService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;

        public InvoiceService()
        {
            // Lấy cấu hình từ web.config
            _smtpHost = ConfigurationManager.AppSettings["SmtpHost"];
            _smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
            _smtpUsername = ConfigurationManager.AppSettings["SmtpUsername"];
            _smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"];
            _fromEmail = ConfigurationManager.AppSettings["FromEmail"];
        }

        public async Task SendInvoiceEmailAsync(Order order)
        {
            try
            {
                using (var client = new SmtpClient(_smtpHost, _smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_fromEmail),
                        Subject = $"Hóa đơn cho đơn hàng #{order.Id}",
                        Body = $"Chào {order.Name},\n\nCảm ơn bạn đã đặt hàng. Vui lòng xem hóa đơn đính kèm.\n\nTrân trọng.",
                        IsBodyHtml = false
                    };

                    mailMessage.To.Add(order.Email);

                    // Generate PDF
                    var pdfBytes = GenerateInvoicePdf(order);
                    var attachment = new Attachment(new MemoryStream(pdfBytes), $"Hoadon_Donhang_{order.Id}.pdf", "application/pdf");
                    mailMessage.Attachments.Add(attachment);

                    await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Email with PDF failed: {ex.Message}");
                throw;
            }
        }

        public async Task SendMultiInvoiceEmailAsync(List<Order> orders)
        {
            try
            {
                // Kiểm tra nếu danh sách đơn hàng trống
                if (orders == null || !orders.Any())
                {
                    throw new ArgumentException("Danh sách đơn hàng không được rỗng");
                }

                // Lấy thông tin người nhận từ đơn hàng đầu tiên (vì tất cả đơn hàng thuộc về cùng 1 người)
                var firstOrder = orders.First();
                var customerEmail = firstOrder.Email;
                var customerName = firstOrder.Name;

                // Kiểm tra email hợp lệ
                if (string.IsNullOrEmpty(customerEmail))
                {
                    throw new ArgumentException("Email khách hàng không được rỗng");
                }

                using (var client = new SmtpClient(_smtpHost, _smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(_smtpUsername, _smtpPassword);

                    // Tạo 1 email duy nhất cho tất cả đơn hàng
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(_fromEmail),
                        Subject = $"Báo cáo tổng hợp {orders.Count} đơn hàng của bạn",
                        Body = CreateEmailBody(customerName, orders),
                        IsBodyHtml = false
                    };

                    mailMessage.To.Add(customerEmail);

                    // Tạo PDF chứa tất cả đơn hàng và đính kèm
                    var pdfBytes = GenerateMultipleOrdersPdf(orders);
                    var attachment = new Attachment(
                        new MemoryStream(pdfBytes),
                        $"Hoadon_DonHang_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                        "application/pdf"
                    );
                    mailMessage.Attachments.Add(attachment);

                    // Gửi 1 email duy nhất
                    await client.SendMailAsync(mailMessage);

                    System.Diagnostics.Debug.WriteLine($"Đã gửi báo cáo {orders.Count} đơn hàng đến {customerEmail}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Gửi email báo cáo đơn hàng thất bại: {ex.Message}");
                throw;
            }
        }

        // Phương thức tạo nội dung email
        private string CreateEmailBody(string customerName, List<Order> orders)
        {
            var orderIds = string.Join(", #", orders.Select(o => o.Id));
            var totalAmount = orders.SelectMany(o => o.OrderItems)
                                   .Sum(item => item.Quantity * item.Product.Price);

            return $@"Chào {customerName},

            Cảm ơn bạn đã tin tưởng và mua sắm tại Simple House!

            Đây là báo cáo tổng hợp cho {orders.Count} đơn hàng của bạn:
            - Mã đơn hàng: #{orderIds}
            - Tổng giá trị: {totalAmount:N0} VNĐ
            - Ngày tạo báo cáo: {DateTime.Now:dd/MM/yyyy HH:mm}

            Vui lòng xem chi tiết trong file PDF đính kèm.

            Nếu có bất kỳ thắc mắc nào, vui lòng liên hệ:
            - Hotline: 0123-456-789
            - Email: simplehouse123@gmail.com

            Trân trọng,
            Simple House Team";
        }

        // Phương thức tạo PDF tổng hợp cho nhiều đơn hàng
        public byte[] GenerateMultipleOrdersPdf(List<Order> orders)
        {
            using (var ms = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4, 40, 40, 40, 40);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // === FONTS VỚI HỖ TRỢ UNICODE ===
                string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                var titleFont = new Font(baseFont, 20, Font.BOLD, BaseColor.DARK_GRAY);
                var headerFont = new Font(baseFont, 14, Font.BOLD, BaseColor.BLACK);
                var subHeaderFont = new Font(baseFont, 12, Font.BOLD, BaseColor.BLACK);
                var normalFont = new Font(baseFont, 11, Font.NORMAL, BaseColor.BLACK);
                var smallFont = new Font(baseFont, 10, Font.NORMAL, BaseColor.GRAY);

                // === HEADER SECTION ===
                PdfPTable headerTable = new PdfPTable(2);
                headerTable.WidthPercentage = 100;
                headerTable.SetWidths(new float[] { 60f, 40f });

                // Left side - Company info
                PdfPCell companyCell = new PdfPCell();
                companyCell.Border = Rectangle.NO_BORDER;
                companyCell.AddElement(new Paragraph("SIMPLE HOUSE", titleFont));
                companyCell.AddElement(new Paragraph("Cửa hàng nội thất Simple House", smallFont));
                companyCell.AddElement(new Paragraph("Hotline: 0123-456-789", smallFont));
                companyCell.AddElement(new Paragraph("Email: simplehouse123@gmail.com", smallFont));
                headerTable.AddCell(companyCell);

                // Right side - Report info
                PdfPCell invoiceInfoCell = new PdfPCell();
                invoiceInfoCell.Border = Rectangle.NO_BORDER;
                invoiceInfoCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                invoiceInfoCell.AddElement(new Paragraph("BÁO CÁO ĐƠN HÀNG", headerFont));
                invoiceInfoCell.AddElement(new Paragraph($"Ngày: {DateTime.Now:dd/MM/yyyy}", normalFont));
                invoiceInfoCell.AddElement(new Paragraph($"Giờ: {DateTime.Now:HH:mm}", normalFont));
                invoiceInfoCell.AddElement(new Paragraph($"Tổng số đơn: {orders.Count}", normalFont));
                headerTable.AddCell(invoiceInfoCell);

                doc.Add(headerTable);

                // Separator line
                LineSeparator line = new LineSeparator(1f, 100f, BaseColor.LIGHT_GRAY, Element.ALIGN_CENTER, -2);
                doc.Add(new Chunk(line));
                doc.Add(new Paragraph(" ")); // Space

                // === CUSTOMER INFORMATION (từ đơn hàng đầu tiên vì thông tin giống nhau) ===
                var firstOrder = orders.First();
                PdfPTable customerTable = new PdfPTable(2);
                customerTable.WidthPercentage = 100;
                customerTable.SetWidths(new float[] { 50f, 50f });

                // Left column
                PdfPCell leftCustomerCell = new PdfPCell();
                leftCustomerCell.Border = Rectangle.NO_BORDER;
                leftCustomerCell.Padding = 5;
                if (!string.IsNullOrEmpty(firstOrder.Name))
                    leftCustomerCell.AddElement(new Paragraph($"Họ tên: {firstOrder.Name}", normalFont));
                if (!string.IsNullOrEmpty(firstOrder.PhoneNumber))
                    leftCustomerCell.AddElement(new Paragraph($"Số điện thoại: {firstOrder.PhoneNumber}", normalFont));
                if (!string.IsNullOrEmpty(firstOrder.Email))
                    leftCustomerCell.AddElement(new Paragraph($"Email: {firstOrder.Email}", normalFont));
                customerTable.AddCell(leftCustomerCell);

                // Right column  
                PdfPCell rightCustomerCell = new PdfPCell();
                rightCustomerCell.Border = Rectangle.NO_BORDER;
                rightCustomerCell.Padding = 5;
                if (!string.IsNullOrEmpty(firstOrder.Address))
                    rightCustomerCell.AddElement(new Paragraph($"Địa chỉ: {firstOrder.Address}", normalFont));

                // Gộp ghi chú từ tất cả đơn hàng (nếu có)
                var allNotes = orders.Where(o => !string.IsNullOrEmpty(o.Note)).Select(o => o.Note).Distinct();
                if (allNotes.Any())
                    rightCustomerCell.AddElement(new Paragraph($"Ghi chú: {string.Join("; ", allNotes)}", normalFont));

                customerTable.AddCell(rightCustomerCell);

                doc.Add(customerTable);
                doc.Add(new Paragraph(" ")); // Space

                // === UNIFIED PRODUCT TABLE ===
                PdfPTable productTable = new PdfPTable(5);
                productTable.WidthPercentage = 100;
                productTable.SetWidths(new float[] { 8f, 35f, 15f, 20f, 22f });

                // Table headers
                string[] headers = { "STT", "Tên sản phẩm", "Số lượng", "Đơn giá", "Thành tiền" };
                foreach (string headerText in headers)
                {
                    PdfPCell headerCell = new PdfPCell(new Phrase(headerText, subHeaderFont));
                    headerCell.BackgroundColor = new BaseColor(240, 240, 240);
                    headerCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    headerCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    headerCell.Padding = 8;
                    headerCell.Border = Rectangle.BOX;
                    headerCell.BorderColor = BaseColor.GRAY;
                    productTable.AddCell(headerCell);
                }

                // Gộp tất cả sản phẩm từ tất cả đơn hàng
                int stt = 1;
                decimal grandTotalAllOrders = 0;

                foreach (var order in orders)
                {
                    foreach (var item in order.OrderItems)
                    {
                        decimal itemTotal = item.Quantity * item.Product.Price;
                        grandTotalAllOrders += itemTotal;

                        // STT
                        PdfPCell sttCell = new PdfPCell(new Phrase(stt.ToString(), normalFont));
                        sttCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        sttCell.Padding = 6;
                        sttCell.Border = Rectangle.BOX;
                        sttCell.BorderColor = BaseColor.LIGHT_GRAY;
                        productTable.AddCell(sttCell);

                        // Product name
                        PdfPCell nameCell = new PdfPCell(new Phrase(item.Product.Name, normalFont));
                        nameCell.Padding = 6;
                        nameCell.Border = Rectangle.BOX;
                        nameCell.BorderColor = BaseColor.LIGHT_GRAY;
                        productTable.AddCell(nameCell);

                        // Quantity
                        PdfPCell qtyCell = new PdfPCell(new Phrase(item.Quantity.ToString(), normalFont));
                        qtyCell.HorizontalAlignment = Element.ALIGN_CENTER;
                        qtyCell.Padding = 6;
                        qtyCell.Border = Rectangle.BOX;
                        qtyCell.BorderColor = BaseColor.LIGHT_GRAY;
                        productTable.AddCell(qtyCell);

                        // Unit price
                        PdfPCell priceCell = new PdfPCell(new Phrase($"{item.Product.Price:N0} VNĐ", normalFont));
                        priceCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                        priceCell.Padding = 6;
                        priceCell.Border = Rectangle.BOX;
                        priceCell.BorderColor = BaseColor.LIGHT_GRAY;
                        productTable.AddCell(priceCell);

                        // Total
                        PdfPCell totalsCell = new PdfPCell(new Phrase($"{itemTotal:N0} VNĐ", normalFont));
                        totalsCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                        totalsCell.Padding = 6;
                        totalsCell.Border = Rectangle.BOX;
                        totalsCell.BorderColor = BaseColor.LIGHT_GRAY;
                        productTable.AddCell(totalsCell);

                        stt++;
                    }
                }

                doc.Add(productTable);

                // === TOTAL SECTION ===
                PdfPTable totalTable = new PdfPTable(2);
                totalTable.WidthPercentage = 100;
                totalTable.SetWidths(new float[] { 70f, 30f });
                totalTable.SpacingBefore = 10f;

                // Empty cell
                PdfPCell emptyCell = new PdfPCell();
                emptyCell.Border = Rectangle.NO_BORDER;
                totalTable.AddCell(emptyCell);

                // Total
                PdfPCell totalCell = new PdfPCell();
                totalCell.Border = Rectangle.BOX;
                totalCell.BorderColor = BaseColor.BLACK;
                totalCell.BackgroundColor = new BaseColor(220, 220, 220);
                totalCell.Padding = 15;
                totalCell.HorizontalAlignment = Element.ALIGN_CENTER;
                totalCell.AddElement(new Paragraph("TỔNG CỘNG", headerFont));
                totalCell.AddElement(new Paragraph($"{grandTotalAllOrders:N0} VNĐ", titleFont));
                totalTable.AddCell(totalCell);

                doc.Add(totalTable);

                // === SUMMARY SECTION ===
                Paragraph summaryTitle = new Paragraph("TỔNG KẾT", subHeaderFont);
                summaryTitle.SpacingBefore = 20f;
                summaryTitle.SpacingAfter = 10f;
                doc.Add(summaryTitle);

                var totalProducts = orders.SelectMany(o => o.OrderItems).Sum(item => item.Quantity);
                var uniqueProducts = orders.SelectMany(o => o.OrderItems).Select(item => item.Product.Name).Distinct().Count();

                doc.Add(new Paragraph($"• Tổng số đơn hàng: {orders.Count}", normalFont));
                doc.Add(new Paragraph($"• Tổng số sản phẩm bán: {totalProducts}", normalFont));
                doc.Add(new Paragraph($"• Số loại sản phẩm khác nhau: {uniqueProducts}", normalFont));
                doc.Add(new Paragraph($"• Tổng doanh thu: {grandTotalAllOrders:N0} VNĐ", normalFont));

                // === SIGNATURE SECTION ===
                doc.Add(new Paragraph(" ")); // Space
                doc.Add(new Paragraph(" ")); // Space

                PdfPTable signatureTable = new PdfPTable(2);
                signatureTable.WidthPercentage = 100;
                signatureTable.SetWidths(new float[] { 50f, 50f });
                signatureTable.SpacingBefore = 30f;

                // Left side - Customer signature
                PdfPCell customerSignatureCell = new PdfPCell();
                customerSignatureCell.Border = Rectangle.NO_BORDER;
                customerSignatureCell.HorizontalAlignment = Element.ALIGN_CENTER;
                customerSignatureCell.Padding = 10;
                customerSignatureCell.AddElement(new Paragraph("NGƯỜI MUA", subHeaderFont));
                customerSignatureCell.AddElement(new Paragraph("(Ký và ghi rõ họ tên)", smallFont));
                customerSignatureCell.AddElement(new Paragraph(" ")); // Space for signature
                customerSignatureCell.AddElement(new Paragraph(" ")); // Space for signature
                customerSignatureCell.AddElement(new Paragraph(" ")); // Space for signature
                customerSignatureCell.AddElement(new Paragraph(" ")); // Space for signature
                if (!string.IsNullOrEmpty(orders.First().Name))
                {
                    customerSignatureCell.AddElement(new Paragraph(orders.First().Name, normalFont));
                }
                signatureTable.AddCell(customerSignatureCell);

                // Right side - Seller signature
                PdfPCell sellerSignatureCell = new PdfPCell();
                sellerSignatureCell.Border = Rectangle.NO_BORDER;
                sellerSignatureCell.HorizontalAlignment = Element.ALIGN_CENTER;
                sellerSignatureCell.Padding = 10;
                sellerSignatureCell.AddElement(new Paragraph("NGƯỜI BÁN", subHeaderFont));
                sellerSignatureCell.AddElement(new Paragraph("(Ký và ghi rõ họ tên)", smallFont));
                sellerSignatureCell.AddElement(new Paragraph(" ")); // Space for signature
                sellerSignatureCell.AddElement(new Paragraph(" ")); // Space for signature
                sellerSignatureCell.AddElement(new Paragraph(" ")); // Space for signature
                sellerSignatureCell.AddElement(new Paragraph(" ")); // Space for signature
                sellerSignatureCell.AddElement(new Paragraph("Simple House", normalFont));
                signatureTable.AddCell(sellerSignatureCell);

                doc.Add(signatureTable);

                // === FOOTER ===
                Paragraph footer = new Paragraph("Simple House - Báo cáo được tạo tự động", smallFont);
                footer.Alignment = Element.ALIGN_CENTER;
                footer.SpacingBefore = 20f;
                doc.Add(footer);

                doc.Close();
                return ms.ToArray();
            }
        }

        // Phương thức tạo PDF riêng lẻ cho từng đơn hàng (giữ nguyên)
        public byte[] GenerateInvoicePdf(Order order)
        {
            using (var ms = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4, 40, 40, 40, 40);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // === FONTS VỚI HỖ TRỢ UNICODE ===
                string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                var titleFont = new Font(baseFont, 20, Font.BOLD, BaseColor.DARK_GRAY);
                var headerFont = new Font(baseFont, 14, Font.BOLD, BaseColor.BLACK);
                var subHeaderFont = new Font(baseFont, 12, Font.BOLD, BaseColor.BLACK);
                var normalFont = new Font(baseFont, 11, Font.NORMAL, BaseColor.BLACK);
                var smallFont = new Font(baseFont, 10, Font.NORMAL, BaseColor.GRAY);

                // === HEADER SECTION ===
                PdfPTable headerTable = new PdfPTable(2);
                headerTable.WidthPercentage = 100;
                headerTable.SetWidths(new float[] { 60f, 40f });

                // Left side - Company info
                PdfPCell companyCell = new PdfPCell();
                companyCell.Border = Rectangle.NO_BORDER;
                companyCell.AddElement(new Paragraph("SIMPLE HOUSE", titleFont));
                companyCell.AddElement(new Paragraph("Cửa hàng nội thất Simple House", smallFont));
                companyCell.AddElement(new Paragraph("Hotline: 0123-456-789", smallFont));
                companyCell.AddElement(new Paragraph("Email: simplehouse123@gmail.com", smallFont));
                headerTable.AddCell(companyCell);

                // Right side - Invoice info
                PdfPCell invoiceInfoCell = new PdfPCell();
                invoiceInfoCell.Border = Rectangle.NO_BORDER;
                invoiceInfoCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                invoiceInfoCell.AddElement(new Paragraph("HÓA ĐƠN BÁN HÀNG", headerFont));
                invoiceInfoCell.AddElement(new Paragraph($"Số: #{order.Id}", normalFont));
                invoiceInfoCell.AddElement(new Paragraph($"Ngày: {order.OrderDate:dd/MM/yyyy}", normalFont));
                invoiceInfoCell.AddElement(new Paragraph($"Giờ: {order.OrderDate:HH:mm}", normalFont));
                headerTable.AddCell(invoiceInfoCell);

                doc.Add(headerTable);

                // Separator line
                LineSeparator line = new LineSeparator(1f, 100f, BaseColor.LIGHT_GRAY, Element.ALIGN_CENTER, -2);
                doc.Add(new Chunk(line));
                doc.Add(new Paragraph(" ")); // Space

                // === CUSTOMER INFORMATION ===
                Paragraph customerTitle = new Paragraph("THÔNG TIN KHÁCH HÀNG", subHeaderFont);
                customerTitle.SpacingBefore = 10f;
                customerTitle.SpacingAfter = 5f;
                doc.Add(customerTitle);

                PdfPTable customerTable = new PdfPTable(2);
                customerTable.WidthPercentage = 100;
                customerTable.SetWidths(new float[] { 50f, 50f });

                // Left column
                PdfPCell leftCustomerCell = new PdfPCell();
                leftCustomerCell.Border = Rectangle.NO_BORDER;
                leftCustomerCell.Padding = 5;
                if (!string.IsNullOrEmpty(order.Name))
                    leftCustomerCell.AddElement(new Paragraph($"Họ tên: {order.Name}", normalFont));
                if (!string.IsNullOrEmpty(order.PhoneNumber))
                    leftCustomerCell.AddElement(new Paragraph($"Điện thoại: {order.PhoneNumber}", normalFont));
                if (!string.IsNullOrEmpty(order.Email))
                    leftCustomerCell.AddElement(new Paragraph($"Email: {order.Email}", normalFont));
                customerTable.AddCell(leftCustomerCell);

                // Right column  
                PdfPCell rightCustomerCell = new PdfPCell();
                rightCustomerCell.Border = Rectangle.NO_BORDER;
                rightCustomerCell.Padding = 5;
                if (!string.IsNullOrEmpty(order.Address))
                    rightCustomerCell.AddElement(new Paragraph($"Địa chỉ: {order.Address}", normalFont));
                if (!string.IsNullOrEmpty(order.Note))
                    rightCustomerCell.AddElement(new Paragraph($"Ghi chú: {order.Note}", normalFont));
                customerTable.AddCell(rightCustomerCell);

                doc.Add(customerTable);
                doc.Add(new Paragraph(" ")); // Space

                // === PRODUCT TABLE ===
                Paragraph productTitle = new Paragraph("CHI TIẾT ĐƠN HÀNG", subHeaderFont);
                productTitle.SpacingBefore = 10f;
                productTitle.SpacingAfter = 10f;
                doc.Add(productTitle);

                PdfPTable productTable = new PdfPTable(5);
                productTable.WidthPercentage = 100;
                productTable.SetWidths(new float[] { 8f, 35f, 15f, 20f, 22f });

                // Table headers with nice styling
                string[] headers = { "STT", "Tên sản phẩm", "Số lượng", "Đơn giá", "Thành tiền" };
                foreach (string headerText in headers)
                {
                    PdfPCell headerCell = new PdfPCell(new Phrase(headerText, subHeaderFont));
                    headerCell.BackgroundColor = new BaseColor(240, 240, 240);
                    headerCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    headerCell.VerticalAlignment = Element.ALIGN_MIDDLE;
                    headerCell.Padding = 8;
                    headerCell.Border = Rectangle.BOX;
                    headerCell.BorderColor = BaseColor.GRAY;
                    productTable.AddCell(headerCell);
                }

                // Product rows
                int stt = 1;
                decimal grandTotal = 0;

                foreach (var item in order.OrderItems)
                {
                    decimal itemTotal = item.Quantity * item.Product.Price;
                    grandTotal += itemTotal;

                    // STT
                    PdfPCell sttCell = new PdfPCell(new Phrase(stt.ToString(), normalFont));
                    sttCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    sttCell.Padding = 6;
                    sttCell.Border = Rectangle.BOX;
                    sttCell.BorderColor = BaseColor.LIGHT_GRAY;
                    productTable.AddCell(sttCell);

                    // Product name
                    PdfPCell nameCell = new PdfPCell(new Phrase(item.Product.Name, normalFont));
                    nameCell.Padding = 6;
                    nameCell.Border = Rectangle.BOX;
                    nameCell.BorderColor = BaseColor.LIGHT_GRAY;
                    productTable.AddCell(nameCell);

                    // Quantity
                    PdfPCell qtyCell = new PdfPCell(new Phrase(item.Quantity.ToString(), normalFont));
                    qtyCell.HorizontalAlignment = Element.ALIGN_CENTER;
                    qtyCell.Padding = 6;
                    qtyCell.Border = Rectangle.BOX;
                    qtyCell.BorderColor = BaseColor.LIGHT_GRAY;
                    productTable.AddCell(qtyCell);

                    // Unit price
                    PdfPCell priceCell = new PdfPCell(new Phrase($"{item.Product.Price:N0} VNĐ", normalFont));
                    priceCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    priceCell.Padding = 6;
                    priceCell.Border = Rectangle.BOX;
                    priceCell.BorderColor = BaseColor.LIGHT_GRAY;
                    productTable.AddCell(priceCell);

                    // Total
                    PdfPCell totalCell = new PdfPCell(new Phrase($"{itemTotal:N0} VNĐ", normalFont));
                    totalCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                    totalCell.Padding = 6;
                    totalCell.Border = Rectangle.BOX;
                    totalCell.BorderColor = BaseColor.LIGHT_GRAY;
                    productTable.AddCell(totalCell);

                    stt++;
                }

                doc.Add(productTable);

                // === TOTAL SECTION ===
                PdfPTable totalTable = new PdfPTable(2);
                totalTable.WidthPercentage = 100;
                totalTable.SetWidths(new float[] { 70f, 30f });
                totalTable.SpacingBefore = 15f;

                // Empty cell for spacing
                PdfPCell emptyCell = new PdfPCell();
                emptyCell.Border = Rectangle.NO_BORDER;
                totalTable.AddCell(emptyCell);

                // Total amount
                PdfPCell totalAmountCell = new PdfPCell();
                totalAmountCell.Border = Rectangle.BOX;
                totalAmountCell.BorderColor = BaseColor.GRAY;
                totalAmountCell.BackgroundColor = new BaseColor(250, 250, 250);
                totalAmountCell.Padding = 10;
                totalAmountCell.AddElement(new Paragraph("TỔNG CỘNG", subHeaderFont));
                totalAmountCell.AddElement(new Paragraph($"{grandTotal:N0} VNĐ", headerFont));
                totalAmountCell.HorizontalAlignment = Element.ALIGN_CENTER;
                totalTable.AddCell(totalAmountCell);

                doc.Add(totalTable);

                // === SIGNATURE SECTION ===
                PdfPTable signatureTable = new PdfPTable(2);
                signatureTable.WidthPercentage = 100;
                signatureTable.SetWidths(new float[] { 50f, 50f });
                signatureTable.SpacingBefore = 30f;

                // Customer signature
                PdfPCell customerSigCell = new PdfPCell();
                customerSigCell.Border = Rectangle.NO_BORDER;
                customerSigCell.HorizontalAlignment = Element.ALIGN_CENTER;
                customerSigCell.AddElement(new Paragraph("KHÁCH HÀNG", subHeaderFont));
                customerSigCell.AddElement(new Paragraph("(Ký tên)", smallFont));
                customerSigCell.AddElement(new Paragraph(" ", normalFont)); // Space for signature
                customerSigCell.AddElement(new Paragraph(" ", normalFont));
                customerSigCell.AddElement(new Paragraph(" ", normalFont));
                signatureTable.AddCell(customerSigCell);

                // Seller signature
                PdfPCell sellerSigCell = new PdfPCell();
                sellerSigCell.Border = Rectangle.NO_BORDER;
                sellerSigCell.HorizontalAlignment = Element.ALIGN_CENTER;
                sellerSigCell.AddElement(new Paragraph("NGƯỜI BÁN", subHeaderFont));
                sellerSigCell.AddElement(new Paragraph("(Ký tên)", smallFont));
                sellerSigCell.AddElement(new Paragraph(" ", normalFont)); // Space for signature
                sellerSigCell.AddElement(new Paragraph(" ", normalFont));
                sellerSigCell.AddElement(new Paragraph(" ", normalFont));
                sellerSigCell.AddElement(new Paragraph("Simple House", normalFont));
                signatureTable.AddCell(sellerSigCell);

                doc.Add(signatureTable);

                // === FOOTER ===
                Paragraph footer = new Paragraph("Cảm ơn quý khách đã mua hàng! Hẹn gặp lại!", smallFont);
                footer.Alignment = Element.ALIGN_CENTER;
                footer.SpacingBefore = 20f;
                doc.Add(footer);

                Paragraph footerLine2 = new Paragraph("*** HÓA ĐƠN KHÔNG CÓ GIÁ TRỊ THUẾ ***", smallFont);
                footerLine2.Alignment = Element.ALIGN_CENTER;
                footerLine2.SpacingBefore = 5f;
                doc.Add(footerLine2);

                doc.Close();
                return ms.ToArray();
            }
        }
    }
}