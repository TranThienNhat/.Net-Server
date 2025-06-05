using iTextSharp.text;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.draw;
using SHOPAPI.Models;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace SHOPAPI.Services
{

    public class EmailService
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUsername;
        private readonly string _smtpPassword;
        private readonly string _fromEmail;

        public EmailService()
        {
            // Lấy cấu hình từ web.config
            _smtpHost = ConfigurationManager.AppSettings["SmtpHost"];
            _smtpPort = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]);
            _smtpUsername = ConfigurationManager.AppSettings["SmtpUsername"];
            _smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"];
            _fromEmail = ConfigurationManager.AppSettings["FromEmail"];
        }

        public async Task SendOrderConfirmationEmailAsync(Order order)
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
                        Subject = $"Xác nhận đơn hàng #{order.Id}",
                        Body = GenerateEmailBody(order),
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(order.Email);

                    await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            {
                // Log lỗi (có thể dùng NLog, Serilog, etc.)
                System.Diagnostics.Debug.WriteLine($"Email sending failed: {ex.Message}");
                throw; // hoặc xử lý lỗi theo cách khác
            }
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
                    var attachment = new Attachment(new MemoryStream(pdfBytes), $"Invoice_{order.Id}.pdf", "application/pdf");
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




        public byte[] GenerateInvoicePdf(Order order)
        {
            using (var ms = new MemoryStream())
            {
                Document doc = new Document(PageSize.A4, 40, 40, 40, 40);
                PdfWriter.GetInstance(doc, ms);
                doc.Open();

                // === FONTS VỚI HỖ TRỢ UNICODE ===
                // Sử dụng font hỗ trợ Unicode - cách 1: Sử dụng font có sẵn
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

        private string GenerateEmailBody(Order order)
        {
            var orderItems = "";
            foreach (var item in order.OrderItems)
            {
                orderItems += $@"
        <tr>
            <td style=""padding: 15px; border-bottom: 1px solid #e2e8f0; color: #2d3748; font-weight: 500;"">{item.Product?.Name ?? "Sản phẩm"}</td>
            <td style=""padding: 15px; border-bottom: 1px solid #e2e8f0; text-align: center; color: #4a5568;"">{item.Quantity}</td>
            <td style=""padding: 15px; border-bottom: 1px solid #e2e8f0; text-align: right; color: #2d3748; font-weight: 600;"">{item.PriceAtPurchase:N0}đ</td>
            <td style=""padding: 15px; border-bottom: 1px solid #e2e8f0; text-align: right; color: #2d3748; font-weight: 600;"">{(item.PriceAtPurchase * item.Quantity):N0}đ</td>
        </tr>";
            }

            return $@"
    <!DOCTYPE html>
    <html>
    <head>
        <meta charset=""UTF-8"">
        <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
        <style>
            @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap');
            
            body {{
                font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
                line-height: 1.6;
                color: #1a1a1a;
                margin: 0;
                padding: 20px;
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                min-height: 100vh;
            }}
            
            .email-container {{
                max-width: 600px;
                margin: 0 auto;
                background: #ffffff;
                border-radius: 16px;
                overflow: hidden;
                box-shadow: 0 20px 40px rgba(0, 0, 0, 0.15);
            }}
            
            .header {{
                background: linear-gradient(135deg, #4facfe 0%, #00f2fe 100%);
                padding: 40px 30px;
                text-align: center;
                color: white;
                position: relative;
            }}
            
            .header::before {{
                content: '';
                position: absolute;
                top: 0;
                left: 0;
                right: 0;
                bottom: 0;
                background: url('data:image/svg+xml,<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 100 100""><defs><pattern id=""grain"" width=""100"" height=""100"" patternUnits=""userSpaceOnUse""><circle cx=""50"" cy=""50"" r=""1"" fill=""rgba(255,255,255,0.1)""/></pattern></defs><rect width=""100"" height=""100"" fill=""url(%23grain)""/></svg>');
                opacity: 0.1;
            }}
            
            .checkmark {{
                display: inline-block;
                width: 70px;
                height: 70px;
                background: rgba(255, 255, 255, 0.2);
                border-radius: 50%;
                margin-bottom: 20px;
                line-height: 70px;
                font-size: 32px;
                backdrop-filter: blur(10px);
                border: 2px solid rgba(255, 255, 255, 0.3);
                position: relative;
                z-index: 1;
            }}
            
            .header h1 {{
                margin: 0;
                font-size: 28px;
                font-weight: 700;
                text-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
                position: relative;
                z-index: 1;
            }}
            
            .content {{
                padding: 40px 30px;
            }}
            
            .greeting {{
                font-size: 18px;
                margin-bottom: 30px;
                color: #2d3748;
            }}
            
            .order-summary {{
                background: linear-gradient(135deg, #f8f9ff 0%, #f0f4ff 100%);
                border-radius: 12px;
                padding: 25px;
                margin: 30px 0;
                border-left: 4px solid #4facfe;
                box-shadow: 0 4px 12px rgba(79, 172, 254, 0.1);
            }}
            
            .order-summary h3 {{
                color: #2d3748;
                margin: 0 0 20px 0;
                font-size: 20px;
                font-weight: 600;
            }}
            
            .info-grid {{
                display: grid;
                grid-template-columns: 1fr 1fr;
                gap: 15px;
                margin-bottom: 20px;
            }}
            
            .info-item {{
                background: white;
                padding: 15px;
                border-radius: 8px;
                box-shadow: 0 2px 8px rgba(0, 0, 0, 0.05);
                transition: all 0.2s ease;
            }}
            
            .info-item:hover {{
                transform: translateY(-2px);
                box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
            }}
            
            .info-label {{
                font-size: 12px;
                font-weight: 600;
                text-transform: uppercase;
                color: #718096;
                margin-bottom: 5px;
                letter-spacing: 0.5px;
            }}
            
            .info-value {{
                font-weight: 600;
                color: #2d3748;
                font-size: 14px;
            }}
            
            .full-width {{
                grid-column: 1 / -1;
            }}
            
            .products-section h3 {{
                color: #2d3748;
                margin: 40px 0 20px 0;
                font-size: 20px;
                font-weight: 600;
            }}
            
            .products-table {{
                width: 100%;
                border-collapse: collapse;
                background: white;
                border-radius: 12px;
                overflow: hidden;
                box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
            }}
            
            .products-table th {{
                background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
                color: white;
                padding: 18px 15px;
                text-align: left;
                font-weight: 600;
                font-size: 14px;
                text-transform: uppercase;
                letter-spacing: 0.5px;
            }}
            
            .products-table th:nth-child(2),
            .products-table th:nth-child(3),
            .products-table th:nth-child(4) {{
                text-align: center;
            }}
            
            .products-table td {{
                padding: 15px;
                border-bottom: 1px solid #e2e8f0;
                transition: background-color 0.2s ease;
            }}
            
            .products-table tr:last-child td {{
                border-bottom: none;
            }}
            
            .products-table tr:hover {{
                background-color: #f7fafc;
            }}
            
            .total-section {{
                background: linear-gradient(135deg, #ffecd2 0%, #fcb69f 100%);
                border-radius: 16px;
                padding: 30px;
                margin: 30px 0;
                text-align: center;
                position: relative;
                overflow: hidden;
            }}
            
            .total-section::before {{
                content: '';
                position: absolute;
                top: -50%;
                left: -50%;
                width: 200%;
                height: 200%;
                background: radial-gradient(circle, rgba(255,255,255,0.3) 0%, transparent 70%);
                animation: shimmer 3s ease-in-out infinite;
            }}
            
            @keyframes shimmer {{
                0%, 100% {{ opacity: 0; }}
                50% {{ opacity: 1; }}
            }}
            
            .total-label {{
                font-size: 16px;
                color: #8b4513;
                margin-bottom: 10px;
                font-weight: 500;
                position: relative;
                z-index: 1;
            }}
            
            .total-amount {{
                font-size: 36px;
                font-weight: 700;
                color: #2d3748;
                margin: 10px 0;
                position: relative;
                z-index: 1;
                text-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
            }}
            
            .footer {{
                background: linear-gradient(135deg, #f7fafc 0%, #edf2f7 100%);
                padding: 30px;
                text-align: center;
                border-radius: 0 0 16px 16px;
            }}
            
            .footer p {{
                margin: 10px 0;
                color: #718096;
                font-size: 14px;
            }}
            
            .footer .highlight {{
                color: #4facfe;
                font-weight: 600;
            }}
            
            .footer .heart {{
                color: #e53e3e;
                animation: heartbeat 1.5s ease-in-out infinite;
            }}
            
            @keyframes heartbeat {{
                0%, 50%, 100% {{ transform: scale(1); }}
                25%, 75% {{ transform: scale(1.1); }}
            }}
            
            @media (max-width: 600px) {{
                body {{
                    padding: 10px;
                }}
                
                .email-container {{
                    border-radius: 12px;
                }}
                
                .info-grid {{
                    grid-template-columns: 1fr;
                }}
                
                .header, .content {{
                    padding: 30px 20px;
                }}
                
                .products-table th,
                .products-table td {{
                    padding: 12px 8px;
                    font-size: 13px;
                }}
                
                .total-amount {{
                    font-size: 28px;
                }}
            }}
        </style>
    </head>
    <body>
        <div class=""email-container"">
            <div class=""header"">
                <div class=""checkmark"">✓</div>
                <h1>Đặt hàng thành công!</h1>
            </div>
            
            <div class=""content"">
                <div class=""greeting"">
                    Xin chào <strong>{order.Name}</strong>,<br>
                    Cảm ơn bạn đã tin tưởng và đặt hàng tại cửa hàng chúng tôi! 🎉
                </div>
                
                <div class=""order-summary"">
                    <h3>📦 Thông tin đơn hàng #{order.Id}</h3>
                    <div class=""info-grid"">
                        <div class=""info-item"">
                            <div class=""info-label"">Ngày đặt</div>
                            <div class=""info-value"">{order.OrderDate:dd/MM/yyyy HH:mm}</div>
                        </div>
                        <div class=""info-item"">
                            <div class=""info-label"">Số điện thoại</div>
                            <div class=""info-value"">{order.PhoneNumber}</div>
                        </div>
                        <div class=""info-item full-width"">
                            <div class=""info-label"">Địa chỉ giao hàng</div>
                            <div class=""info-value"">{order.Address}</div>
                        </div>
                        {(string.IsNullOrEmpty(order.Note) ? "" : $@"
                        <div class=""info-item full-width"">
                            <div class=""info-label"">Ghi chú</div>
                            <div class=""info-value"">{order.Note}</div>
                        </div>")}
                    </div>
                </div>
                
                <div class=""products-section"">
                    <h3>🛍️ Chi tiết sản phẩm</h3>
                    <table class=""products-table"">
                        <thead>
                            <tr>
                                <th>Sản phẩm</th>
                                <th>Số lượng</th>
                                <th>Đơn giá</th>
                                <th>Thành tiền</th>
                            </tr>
                        </thead>
                        <tbody>
                            {orderItems}
                        </tbody>
                    </table>
                </div>
                
                <div class=""total-section"">
                    <div class=""total-label"">Tổng thanh toán</div>
                    <div class=""total-amount"">{order.TotalPrice:N0}đ</div>
                </div>
            </div>
            
            <div class=""footer"">
                <p>🚚 <span class=""highlight"">Chúng tôi sẽ liên hệ với bạn sớm nhất để xác nhận và giao hàng.</span></p>
                <p>Cảm ơn bạn đã tin tưởng và mua sắm tại cửa hàng! <span class=""heart"">❤️</span></p>
                <p style=""font-size: 12px; color: #a0aec0; margin-top: 20px;"">
                    Email này được gửi tự động, vui lòng không trả lời trực tiếp.
                </p>
            </div>
        </div>
    </body>
    </html>";
        }

    }
}