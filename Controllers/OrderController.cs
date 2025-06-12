using API.DTOs.Cart;
using API.DTOs.Order;
using API.Services;
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
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Http;

namespace SHOPAPI.Controllers
{
    public class OrderController : ApiController
    {
        private readonly AppDbContext db = new AppDbContext();
        private readonly EmailService emailService = new EmailService();
        private readonly InvoiceService invoiceService = new InvoiceService();

        [Authorize(Roles = "ADMIN")]
        [HttpGet]
        [Route("api/orders/admin")]
        public IHttpActionResult GetAllOrders()
        {
            var orders = db.Orders
                .AsNoTracking()
                .Include("User")
                .Include("OrderItems.Product")
                .Select(o => new OrderReadDto
                {
                    Id = o.Id,
                    Name = o.User.Name,
                    PhoneNumber = o.User.PhoneNumber,
                    Email = o.User.Email,
                    Address = o.User.Address,
                    Note = o.Note,
                    OrderDate = o.OrderDate,
                    TotalPrice = o.TotalPrice,
                    OrderStatus = o.OrderStatus.ToString(),
                    OrderItems = o.OrderItems.Select(oi => new OrderItemReadDto
                    {
                        ProductId = oi.ProductId,
                        ProductName = oi.Product.Name,
                        Quantity = oi.Quantity,
                        PriceAtPurchase = oi.PriceAtPurchase
                    }).ToList()
                }).ToList();

            return Ok(orders);
        }

        [Authorize(Roles = "USER")]
        [HttpGet]
        [Route("api/orders/my")]
        public IHttpActionResult GetMyOrders()
        {
            var identity = (ClaimsIdentity)User.Identity;
            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            int userId = Convert.ToInt32(userIdClaim.Value);

            var orders = db.Orders
                .AsNoTracking()
                .Include("OrderItems.Product")
                .Where(o => o.UserId == userId)
                .Select(o => new OrderReadDto
                {
                    Id = o.Id,
                    Name = o.User.Name,
                    PhoneNumber = o.User.PhoneNumber,
                    Email = o.User.Email,
                    Address = o.User.Address,
                    Note = o.Note,
                    OrderDate = o.OrderDate,
                    TotalPrice = o.TotalPrice,
                    OrderStatus = o.OrderStatus.ToString(),
                    OrderItems = o.OrderItems.Select(oi => new OrderItemReadDto
                    {
                        ProductId = oi.ProductId,
                        ProductName = oi.Product.Name,
                        Quantity = oi.Quantity,
                        PriceAtPurchase = oi.PriceAtPurchase
                    }).ToList()
                }).ToList();

            return Ok(orders);
        }


        [Authorize(Roles = "USER, ADMIN")]
        [HttpPost]
        [Route("api/orders/create")]
        public async Task<IHttpActionResult> CreateOrder(OrderCreateDto dto)
        {
            if (dto == null || dto.Items == null || !dto.Items.Any())
                return BadRequest("Dữ liệu đơn hàng không hợp lệ.");

            var identity = (ClaimsIdentity)User.Identity;
            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            int userId = Convert.ToInt32(userIdClaim.Value);

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var productIds = dto.Items.Select(i => i.ProductId).ToList();
                    var products = await db.Products
                        .Where(p => productIds.Contains(p.Id))
                        .ToDictionaryAsync(p => p.Id, p => p);

                    foreach (var itemDto in dto.Items)
                    {
                        if (!products.ContainsKey(itemDto.ProductId))
                            return BadRequest($"Sản phẩm có ID {itemDto.ProductId} không tồn tại.");

                        var product = products[itemDto.ProductId];
                        if (product.Quantity < itemDto.Quantity)
                            return BadRequest($"Sản phẩm {product.Name} không đủ hàng.");
                    }

                    var orderItems = new List<OrderItem>();
                    long totalPrice = 0;

                    foreach (var itemDto in dto.Items)
                    {
                        var product = products[itemDto.ProductId];
                        product.Quantity -= itemDto.Quantity;
                        if (product.Quantity == 0)
                            product.IsOutOfStock = true;

                        orderItems.Add(new OrderItem
                        {
                            ProductId = product.Id,
                            Quantity = itemDto.Quantity,
                            PriceAtPurchase = product.Price,
                            Product = product
                        });

                        totalPrice += product.Price * itemDto.Quantity;
                    }

                    var order = new Order
                    {
                        UserId = userId,
                        Note = dto.Note,
                        OrderDate = DateTime.Now,
                        TotalPrice = totalPrice,
                        OrderStatus = OrderStatus.DangXuLy,
                        OrderItems = orderItems
                    };

                    db.Orders.Add(order);
                    db.SaveChanges();
                    transaction.Commit();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await emailService.SendOrderConfirmationEmailAsync(order);
                        }
                        catch (Exception exEmail)
                        {
                            System.Diagnostics.Debug.WriteLine($"Email sending failed: {exEmail.Message}");
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

        [Authorize(Roles = "USER")]
        [HttpPost]
        [Route("api/orders/create-from-cart")]
        public async Task<IHttpActionResult> CreateOrderFromCart(OrderFromCartDto dto)
        {
            if (dto == null || dto.CartItemIds == null || !dto.CartItemIds.Any())
                return BadRequest("Dữ liệu đơn hàng không hợp lệ.");

            var identity = (ClaimsIdentity)User.Identity;
            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            int userId = Convert.ToInt32(userIdClaim.Value);

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var cartItems = db.CartItems
                        .Include("Product")
                        .Include("Cart")
                        .Where(ci => dto.CartItemIds.Contains(ci.Id) && ci.Cart.UserId == userId)
                        .ToList();

                    if (!cartItems.Any())
                        return BadRequest("Không tìm thấy cart item hợp lệ.");

                    var orders = new List<Order>();

                    // Không cần GroupBy — gom tất cả cartItems thành 1 đơn duy nhất
                    var orderItems = new List<OrderItem>();
                    long totalPrice = 0;

                    foreach (var item in cartItems)
                    {
                        var product = item.Product;

                        if (product.Quantity < item.Quantity)
                            return BadRequest($"Sản phẩm {product.Name} không đủ hàng.");

                        product.Quantity -= item.Quantity;
                        if (product.Quantity == 0)
                            product.IsOutOfStock = true;

                        orderItems.Add(new OrderItem
                        {
                            ProductId = product.Id,
                            Quantity = item.Quantity,
                            PriceAtPurchase = product.Price
                        });

                        totalPrice += product.Price * item.Quantity;
                    }

                    var order = new Order
                    {
                        UserId = userId,
                        OrderDate = DateTime.Now,
                        TotalPrice = totalPrice,
                        OrderStatus = OrderStatus.DangXuLy,
                        OrderItems = orderItems
                    };

                    db.Orders.Add(order);
                    db.CartItems.RemoveRange(cartItems);
                    await db.SaveChangesAsync();
                    transaction.Commit();


                    // Gửi email xác nhận nhiều đơn
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await invoiceService.SendMultiInvoiceEmailAsync(orders);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Lỗi gửi email: {ex.Message}");
                        }
                    });

                    return Ok(new
                    {
                        message = $"Tạo đơn hàng thành công.",
                        orderIds = order.Id
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

            if (newStatus == OrderStatus.DaHuy && order.OrderStatus != OrderStatus.DaHuy)
            {
                foreach (var orderItem in order.OrderItems)
                {
                    var product = orderItem.Product;
                    product.Quantity += orderItem.Quantity;
                    product.IsOutOfStock = false;
                }
            }

            order.OrderStatus = newStatus;
            db.SaveChanges();

            return Ok("Cập nhật trạng thái đơn hàng thành công.");
        }

        [Authorize(Roles = "USER")]
        [HttpPut]
        [Route("api/orders/{id}/cancel")]
        public IHttpActionResult CancelOrder(int id)
        {
            var identity = (ClaimsIdentity)User.Identity;
            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            int userId = Convert.ToInt32(userIdClaim.Value);

            var order = db.Orders.FirstOrDefault(o => o.Id == id && o.UserId == userId);
            if (order == null)
                return NotFound();

            if (order.OrderStatus != OrderStatus.DangXuLy)
                return BadRequest("Chỉ có thể hủy đơn hàng khi đang xử lý.");

            order.OrderStatus = OrderStatus.DaHuy;
            db.SaveChanges();

            return Ok(new
            {
                message = "Đơn hàng đã được hủy thành công.",
                orderId = order.Id
            });
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
                await invoiceService.SendInvoiceEmailAsync(order);

                return Ok(new
                {
                    message = $"Hóa đơn PDF đã được gửi đến {order.User.Email}."
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Gửi hóa đơn thất bại: {ex.Message}");
                return InternalServerError(new Exception("Gửi hóa đơn thất bại."));
            }
        }

        [Authorize(Roles = "ADMIN")]
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
                var pdfBytes = invoiceService.GenerateInvoicePdf(order);

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

        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        [Route("api/orders/report")]
        public async Task<IHttpActionResult> GetOrderReport(OrderReportRequestDto dto)
        {
            if (dto == null)
                return BadRequest("Dữ liệu không hợp lệ.");

            if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
                return BadRequest("Số điện thoại là bắt buộc.");

            try
            {
                // Nếu OrderDate null thì lấy ngày hôm nay
                DateTime targetDate = dto.OrderDate ?? DateTime.Today;

                // Tìm các đơn hàng theo số điện thoại và ngày
                var orders = await db.Orders
                    .Include(o => o.OrderItems.Select(oi => oi.Product))
                    .Where(o => o.User.PhoneNumber == dto.PhoneNumber.Trim() &&
                               DbFunctions.TruncateTime(o.OrderDate) == targetDate.Date &&
                               o.OrderStatus == OrderStatus.DaGiao) // Thêm điều kiện trạng thái đơn hàng
                    .OrderBy(o => o.OrderDate)
                    .ToListAsync();


                if (!orders.Any())
                {
                    return BadRequest("Không có đơn hàng nào được tìm thấy!");
                }

                // Tạo PDF báo cáo
                var pdfBytes = invoiceService.GenerateMultipleOrdersPdf(orders);

                if (dto.SendConfirmation)
                {
                    // Gửi email với PDF đính kèm
                    try
                    {
                        await invoiceService.SendMultiInvoiceEmailAsync(orders);

                        return Ok(new
                        {
                            Message = $"Đã gửi báo cáo {orders.Count} đơn hàng đến email {orders.First().User.Email}",
                            PhoneNumber = dto.PhoneNumber,
                            OrderDate = targetDate.ToString("dd/MM/yyyy"),
                            OrderCount = orders.Count,
                            OrderIds = orders.Select(o => o.Id).ToList(),
                            TotalAmount = orders.Sum(o => o.TotalPrice),
                            EmailSent = true
                        });
                    }
                    catch (Exception emailEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Gửi email thất bại: {emailEx.Message}");
                        return InternalServerError(new Exception("Tạo báo cáo thành công nhưng gửi email thất bại."));
                    }
                }
                else
                {
                    // Trả về file PDF trực tiếp
                    var response = Request.CreateResponse(HttpStatusCode.OK);
                    response.Content = new ByteArrayContent(pdfBytes);
                    response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                    response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("inline")
                    {
                        FileName = $"BaoCao_DonHang_{dto.PhoneNumber}_{targetDate:yyyyMMdd}.pdf"
                    };
                    return ResponseMessage(response);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi tạo báo cáo đơn hàng: {ex.Message}");
                return InternalServerError(new Exception("Có lỗi xảy ra khi tạo báo cáo đơn hàng."));
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