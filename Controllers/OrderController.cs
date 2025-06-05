using API.DTOs.Order;
using SHOPAPI.Data;
using SHOPAPI.DTOs.Order;
using SHOPAPI.Models;
using SHOPAPI.Models.Enum;
using SHOPAPI.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace SHOPAPI.Controllers
{
    public class OrderController : ApiController
    {
        private readonly AppDbContext db = new AppDbContext();
        private readonly EmailService emailService = new EmailService();

        [Authorize(Roles = "ADMIN")]
        [HttpGet]
        [Route("api/orders")]
        public IHttpActionResult GetOrder()
        {
            var orders = db.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems.Select(oi => oi.Product))
                .Select(o => new OrderReadDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    PhoneNumber = o.PhoneNumber,
                    Email = o.Email,
                    Address = o.Address,
                    Note = o.Note,
                    OrderDate = o.OrderDate,
                    TotalPrice = o.TotalPrice,
                    OrderStatus = o.orderStatus.ToString(),
                    OrderItems = o.OrderItems.Select(oi => new OrderItemReadDto
                    {
                        ProductId = oi.ProductId,
                        ProductName = oi.Product.Name,
                        Quantity = oi.Quantity,
                        PriceAtPurchase = oi.PriceAtPurchase,
                    }).ToList()
                }).ToList();
            return Ok(orders);
        }

        [Authorize(Roles = "ADMIN")]
        [HttpGet]
        [Route("api/orders/{id}")]
        public async Task<IHttpActionResult> GetOrderById(int id)
        {
            var order = await db.Orders
                .AsNoTracking()
                .Include(o => o.OrderItems.Select(oi => oi.Product))
                .Where(o => o.Id == id)
                .Select(o => new OrderReadDto
                {
                    Id = o.Id,
                    Name = o.Name,
                    PhoneNumber = o.PhoneNumber,
                    Email = o.Email,
                    Address = o.Address,
                    Note = o.Note,
                    OrderDate = o.OrderDate,
                    TotalPrice = o.TotalPrice,
                    OrderStatus = o.orderStatus.ToString(),
                    OrderItems = o.OrderItems.Select(oi => new OrderItemReadDto
                    {
                        ProductId = oi.ProductId,
                        ProductName = oi.Product.Name,
                        Quantity = oi.Quantity,
                        PriceAtPurchase = oi.PriceAtPurchase,
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (order == null)
                return NotFound();

            return Ok(order);
        }

        
        [HttpPost]
        [Route("api/orders/create")]
        public async Task<IHttpActionResult> CreateOrder(OrderCreateDto dto)
        {
            if (dto == null || dto.Items == null || !dto.Items.Any())
                return BadRequest("Dữ liệu đơn hàng không hợp lệ.");

            if (string.IsNullOrEmpty(dto.Email))
                return BadRequest("Email là bắt buộc.");

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Lấy tất cả product IDs một lần thay vì query từng cái
                    var productIds = dto.Items.Select(i => i.ProductId).ToList();
                    var products = await db.Products
                        .Where(p => productIds.Contains(p.Id))
                        .ToDictionaryAsync(p => p.Id, p => p);

                    // Validation nhanh
                    foreach (var itemDto in dto.Items)
                    {
                        if (!products.ContainsKey(itemDto.ProductId))
                            return BadRequest($"Sản phẩm có ID {itemDto.ProductId} không tồn tại.");

                        var product = products[itemDto.ProductId];
                        if (product.Quantity < itemDto.Quantity)
                            return BadRequest($"Sản phẩm {product.Name} không đủ hàng.");
                    }

                    // Tạo order
                    var order = new Order
                    {
                        Name = dto.Name,
                        PhoneNumber = dto.PhoneNumber,
                        Email = dto.Email,
                        Address = dto.Address,
                        Note = dto.Note,
                        OrderDate = DateTime.Now,
                        TotalPrice = 0,
                        orderStatus = OrderStatus.DangXuLy,
                        OrderItems = new List<OrderItem>()
                    };

                    var orderItems = new List<OrderItem>();
                    long totalPrice = 0;

                    foreach (var itemDto in dto.Items)
                    {
                        var product = products[itemDto.ProductId];

                        // Trừ tồn kho
                        product.Quantity -= itemDto.Quantity;
                        if (product.Quantity == 0)
                            product.IsOutOfStock = true;

                        // Tạo OrderItem
                        var orderItem = new OrderItem
                        {
                            ProductId = product.Id,
                            Quantity = itemDto.Quantity,
                            PriceAtPurchase = product.Price,
                            Product = product
                        };

                        orderItems.Add(orderItem);
                        totalPrice += product.Price * itemDto.Quantity;
                    }

                    order.OrderItems = orderItems;
                    order.TotalPrice = totalPrice;

                    db.Orders.Add(order);

                    db.SaveChanges();

                    transaction.Commit();

                    _= Task.Run(async () =>
                    {
                        try
                        {
                            await emailService.SendOrderConfirmationEmailAsync(order);
                        }
                        catch (Exception emailEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Email sending failed: {emailEx.Message}");
                        }
                    });

                    return Ok(new
                    {
                        Message = "Tạo đơn hàng thành công.",
                        OrderId = order.Id
                    });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return InternalServerError(ex);
                }
            }
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPut]
        [Route("api/orders/{id}")]
        public IHttpActionResult updateOrder(int id, UpdateOrderStatusDto dto)
        {
            var order = db.Orders.Include("OrderItems.Product").FirstOrDefault(o => o.Id == id);

            if (order == null) return NotFound();

            if (!Enum.TryParse(dto.Status, out OrderStatus newStatus))
                return BadRequest("Trạng thái không hợp lệ.");

            if (newStatus == OrderStatus.DaHuy && order.orderStatus != OrderStatus.DaHuy)
            {
                foreach (var orderItem in order.OrderItems)
                {
                    var product = orderItem.Product;
                    product.Quantity += orderItem.Quantity;
                    product.IsOutOfStock = false;
                }
            }

            order.orderStatus = newStatus;
            db.SaveChanges();

            return Ok("Cập nhật trạng thái đơn hàng thành công.");
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPut]
        [Route("api/orders/{id}/info")]
        public async Task<IHttpActionResult> UpdateOrderInfo(int id, UpdateOrderInfoDto dto)
        {
            // Kiểm tra tính hợp lệ của dữ liệu đầu vào
            if (dto == null)
                return BadRequest("Dữ liệu không hợp lệ.");

            // Kiểm tra email hợp lệ
            if (string.IsNullOrWhiteSpace(dto.Email) || !IsValidEmail(dto.Email))
                return BadRequest("Email không hợp lệ.");

            try
            {
                // Tìm đơn hàng theo ID
                var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);
                if (order == null)
                    return NotFound();

                // Kiểm tra trạng thái đơn hàng
                if (order.orderStatus != OrderStatus.DangXuLy)
                    return BadRequest("Chỉ có thể cập nhật thông tin khi đơn hàng đang xử lý.");

                // Cập nhật thông tin (chỉ cập nhật khi có giá trị hợp lệ)
                if (!string.IsNullOrWhiteSpace(dto.Name))
                    order.Name = dto.Name.Trim();

                if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                    order.PhoneNumber = dto.PhoneNumber.Trim();

                if (!string.IsNullOrWhiteSpace(dto.Email))
                    order.Email = dto.Email.Trim();

                if (!string.IsNullOrWhiteSpace(dto.Address))
                    order.Address = dto.Address.Trim();

                // Note có thể là null hoặc empty
                order.Note = dto.Note?.Trim();

                // Cập nhật thời gian sửa đổi (nếu có field này)
                // order.UpdatedAt = DateTime.UtcNow;

                await db.SaveChangesAsync();

                return Ok(new
                {
                    message = "Cập nhật thông tin đơn hàng thành công.",
                    orderId = id
                });
            }
            catch (Exception ex)
            {
                // Log exception ở đây
                return InternalServerError(new Exception("Có lỗi xảy ra khi cập nhật đơn hàng."));
            }
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPut]
        [Route("api/orders/{id}/sendinvoice")]
        public async Task<IHttpActionResult> SendInvoicePdf(int id)
        {
            var order = await db.Orders
                .Include(o => o.OrderItems.Select(oi => oi.Product))
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            try
            {
                await emailService.SendInvoiceEmailAsync(order);

                return Ok(new
                {
                    message = $"Hóa đơn PDF đã được gửi đến {order.Email}."
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Gửi hóa đơn thất bại: {ex.Message}");
                return InternalServerError(new Exception("Gửi hóa đơn thất bại."));
            }
        }

        [HttpGet]
        [Route("api/orders/{id}/invoicepdf")]
        public async Task<HttpResponseMessage> GetInvoicePdf(int id)
        {
            var order = await db.Orders
                .Include(o => o.OrderItems.Select(oi => oi.Product))
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return Request.CreateResponse(HttpStatusCode.NotFound, "Không tìm thấy đơn hàng.");

            try
            {
                // Tạo PDF
                var pdfBytes = emailService.GenerateInvoicePdf(order);

                var result = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(pdfBytes)
                };

                result.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                result.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("inline")
                {
                    FileName = $"Invoice_Order_{order.Id}.pdf"
                };

                return result;
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        // Helper method để kiểm tra email hợp lệ
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}