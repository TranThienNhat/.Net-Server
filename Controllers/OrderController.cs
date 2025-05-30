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
        private readonly IEmailService _emailService;

        public OrderController()
        {
            _emailService = new EmailService();
            // Tối ưu EF tracking
            db.Configuration.AutoDetectChangesEnabled = false;
            db.Configuration.ValidateOnSaveEnabled = false;
        }

        [Authorize(Roles = "ADMIN")]
        [HttpGet]
        [Route("api/orders")]
        public IHttpActionResult GetOrder()
        {
            // Sử dụng AsNoTracking để tăng performance cho read-only operations
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

                    // Bulk processing các items
                    var orderItems = new List<OrderItem>();
                    decimal totalPrice = 0;

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
                    order.TotalPrice = ((long)totalPrice);

                    db.Orders.Add(order);

                    // Enable change detection chỉ khi save
                    db.Configuration.AutoDetectChangesEnabled = true;
                    await db.SaveChangesAsync();
                    db.Configuration.AutoDetectChangesEnabled = false;

                    transaction.Commit();

                    // Gửi email async và không chờ (fire-and-forget)
                    // Điều này giúp response nhanh hơn
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendOrderConfirmationEmailAsync(order);
                        }
                        catch (Exception emailEx)
                        {
                            // Log lỗi email
                            System.Diagnostics.Debug.WriteLine($"Email sending failed: {emailEx.Message}");
                            // TODO: Có thể queue email để retry sau
                        }
                    });

                    return Ok(new
                    {
                        Message = "Tạo đơn hàng thành công.",
                        OrderId = order.Id,
                        TotalPrice = order.TotalPrice
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

        [HttpPut]
        [Route("api/orders/{id}/info")]
        public async Task<IHttpActionResult> UpdateOrderInfo(int id, UpdateOrderInfoDto dto)
        {
            if (dto == null)
                return BadRequest("Dữ liệu không hợp lệ.");

            // Validate email nếu có
            if (!string.IsNullOrEmpty(dto.Email) && !IsValidEmail(dto.Email))
                return BadRequest("Email không hợp lệ.");

            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            // Chỉ cho phép cập nhật khi đơn hàng đang xử lý
            if (order.orderStatus != OrderStatus.DangXuLy)
                return BadRequest("Chỉ có thể cập nhật thông tin khi đơn hàng đang xử lý.");

            // Cập nhật thông tin
            if (!string.IsNullOrEmpty(dto.Name))
                order.Name = dto.Name;

            if (!string.IsNullOrEmpty(dto.PhoneNumber))
                order.PhoneNumber = dto.PhoneNumber;

            if (!string.IsNullOrEmpty(dto.Email))
                order.Email = dto.Email;

            if (!string.IsNullOrEmpty(dto.Address))
                order.Address = dto.Address;

            // Note có thể là null hoặc empty
            order.Note = dto.Note;

            db.Configuration.AutoDetectChangesEnabled = true;
            await db.SaveChangesAsync();
            db.Configuration.AutoDetectChangesEnabled = false;

            return Ok("Cập nhật thông tin đơn hàng thành công.");
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPut]
        [Route("api/orders/{id}/status")]
        public async Task<IHttpActionResult> UpdateOrderStatus(int id, UpdateOrderStatusDto dto)
        {
            // Sử dụng async để tối ưu performance
            var order = await db.Orders
                .Include(o => o.OrderItems.Select(oi => oi.Product))
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null) return NotFound();

            if (!Enum.TryParse(dto.Status, out OrderStatus newStatus))
                return BadRequest("Trạng thái không hợp lệ.");

            // Chỉ xử lý khi thay đổi sang trạng thái hủy
            if (newStatus == OrderStatus.DaHuy && order.orderStatus != OrderStatus.DaHuy)
            {
                // Bulk update tồn kho
                foreach (var orderItem in order.OrderItems)
                {
                    var product = orderItem.Product;
                    product.Quantity += orderItem.Quantity;
                    product.IsOutOfStock = false;
                }
            }

            order.orderStatus = newStatus;

            db.Configuration.AutoDetectChangesEnabled = true;
            await db.SaveChangesAsync();
            db.Configuration.AutoDetectChangesEnabled = false;

            return Ok("Cập nhật trạng thái đơn hàng thành công.");
        }

        // Helper method để validate email
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}