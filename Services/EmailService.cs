using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Configuration;
using SHOPAPI.Models;

namespace SHOPAPI.Services
{
    public interface IEmailService
    {
        Task SendOrderConfirmationEmailAsync(Order order);
    }

    public class EmailService : IEmailService
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

        private string GenerateEmailBody(Order order)
        {
            var orderItems = "";
            foreach (var item in order.OrderItems)
            {
                orderItems += $@"
                    <tr>
                        <td>{item.Product?.Name ?? "Sản phẩm"}</td>
                        <td>{item.Quantity}</td>
                        <td>{item.PriceAtPurchase:N0}đ</td>
                        <td>{(item.PriceAtPurchase * item.Quantity):N0}đ</td>
                    </tr>";
            }

            return $@"
                <html>
                <body>
                    <h2>Cảm ơn bạn đã đặt hàng!</h2>
                    <p>Xin chào {order.Name},</p>
                    <p>Đơn hàng của bạn đã được tạo thành công với thông tin như sau:</p>
                    
                    <h3>Thông tin đơn hàng #  {order.Id}</h3>
                    <p><strong>Ngày đặt:</strong> {order.OrderDate:dd/MM/yyyy HH:mm}</p>
                    <p><strong>Họ tên:</strong> {order.Name}</p>
                    <p><strong>Số điện thoại:</strong> {order.PhoneNumber}</p>
                    <p><strong>Địa chỉ giao hàng:</strong> {order.Address}</p>
                    {(string.IsNullOrEmpty(order.Note) ? "" : $"<p><strong>Ghi chú:</strong> {order.Note}</p>")}
                    
                    <h3>Chi tiết sản phẩm:</h3>
                    <table border='1' style='border-collapse: collapse; width: 100%;'>
                        <thead>
                            <tr style='background-color: #f2f2f2;'>
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
                    
                    <h3 style='color: #d9534f;'>Tổng tiền: {order.TotalPrice:N0}đ</h3>
                    
                    <p>Chúng tôi sẽ liên hệ với bạn sớm nhất để xác nhận và giao hàng.</p>
                    <p>Cảm ơn bạn đã tin tưởng và mua sắm tại cửa hàng!</p>
                </body>
                </html>";
        }
    }
}