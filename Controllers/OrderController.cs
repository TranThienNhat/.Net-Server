using API.DTOs.Cart;
using API.DTOs.Order;
using API.Models;
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
        public async Task<IHttpActionResult> GetAllOrders()
        {
            try
            {
                var orders = await db.Orders
                    .AsNoTracking()
                    .Include(o => o.User)
                    .Include(o => o.OrderItems.Select(oi => oi.Product))
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
                    })
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [Authorize(Roles = "USER")]
        [HttpGet]
        [Route("api/orders/my")]
        public async Task<IHttpActionResult> GetMyOrders()
        {
            try
            {
                var identity = (ClaimsIdentity)User.Identity;
                var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim == null)
                    return Unauthorized();

                if (!int.TryParse(userIdClaim.Value, out int userId))
                    return Unauthorized();

                var orders = await db.Orders
                    .AsNoTracking()
                    .Include(o => o.User)
                    .Include(o => o.OrderItems.Select(oi => oi.Product))
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
                    })
                    .OrderByDescending(o => o.OrderDate)
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [Authorize(Roles = "USER, ADMIN")]
        [HttpPost]
        [Route("api/orders/create")]
        public async Task<IHttpActionResult> CreateOrder(OrderItemDto dto)
        {
            if (dto == null || dto.ProductId <= 0 || dto.Quantity <= 0)
                return BadRequest("Dữ liệu đơn hàng không hợp lệ.");

            var identity = (ClaimsIdentity)User.Identity;
            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            if (!int.TryParse(userIdClaim.Value, out int userId))
                return Unauthorized();

            using (var transaction = db.Database.BeginTransaction())
            {
                try
                {
                    // Lấy user để đảm bảo tồn tại
                    var user = await db.Users.FindAsync(userId);
                    if (user == null)
                        return BadRequest("Người dùng không tồn tại.");

                    // Lấy thông tin sản phẩm
                    var product = await db.Products.FindAsync(dto.ProductId);
                    if (product == null)
                        return BadRequest($"Sản phẩm với ID {dto.ProductId} không tồn tại.");

                    if (product.Quantity < dto.Quantity)
                        return BadRequest($"Sản phẩm {product.Name} không đủ hàng (còn {product.Quantity}, cần {dto.Quantity}).");

                    CartItem cartItemToRemove = null;

                    // Kiểm tra cart item nếu có
                    if (dto.CartItemId.HasValue)
                    {
                        cartItemToRemove = await db.CartItems
                            .Include(ci => ci.Cart)
                            .FirstOrDefaultAsync(ci => ci.Id == dto.CartItemId.Value && ci.Cart.UserId == userId);

                        if (cartItemToRemove == null)
                            return BadRequest($"Cart item với ID {dto.CartItemId.Value} không tồn tại hoặc không thuộc về bạn.");

                        if (cartItemToRemove.ProductId != dto.ProductId)
                            return BadRequest("ProductId không khớp với cart item.");
                    }

                    // Cập nhật số lượng sản phẩm
                    product.Quantity -= dto.Quantity;
                    if (product.Quantity <= 0)
                    {
                        product.Quantity = 0;
                        product.IsOutOfStock = true;
                    }

                    // Tạo đơn hàng
                    var order = new Order
                    {
                        UserId = userId,
                        Note = dto.Note ?? string.Empty,
                        OrderDate = DateTime.Now,
                        TotalPrice = product.Price * dto.Quantity,
                        OrderStatus = OrderStatus.DangXuLy
                    };

                    db.Orders.Add(order);
                    await db.SaveChangesAsync();

                    // Tạo order item
                    var orderItem = new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = product.Id,
                        Quantity = dto.Quantity,
                        PriceAtPurchase = product.Price
                    };

                    db.OrderItems.Add(orderItem);

                    // Xóa cart item nếu có
                    if (cartItemToRemove != null)
                    {
                        db.CartItems.Remove(cartItemToRemove);
                    }

                    await db.SaveChangesAsync();
                    transaction.Commit();

                    // Gửi email xác nhận (fire-and-forget)
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Load lại order với đầy đủ thông tin để gửi email
                            var orderForEmail = await db.Orders
                                .Include(o => o.User)
                                .Include(o => o.OrderItems.Select(oi => oi.Product))
                                .FirstOrDefaultAsync(o => o.Id == order.Id);

                            if (orderForEmail != null)
                            {
                                await emailService.SendOrderConfirmationEmailAsync(orderForEmail);
                            }
                        }
                        catch (Exception exEmail)
                        {
                            System.Diagnostics.Debug.WriteLine($"Email sending failed: {exEmail.Message}");
                        }
                    });

                    return Ok(new
                    {
                        Message = "Tạo đơn hàng thành công.",
                        OrderId = order.Id,
                        TotalPrice = order.TotalPrice,
                        ItemCount = 1
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
            try
            {
                var order = await db.Orders
                    .Include(o => o.User)
                    .Include(o => o.OrderItems.Select(oi => oi.Product))
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                    return NotFound();

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
            try
            {
                var order = await db.Orders
                    .Include(o => o.User)
                    .Include(o => o.OrderItems.Select(oi => oi.Product))
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order == null)
                    return Request.CreateResponse(HttpStatusCode.NotFound, "Không tìm thấy đơn hàng.");

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
                    .Include(o => o.User)
                    .Include(o => o.OrderItems.Select(oi => oi.Product))
                    .Where(o => o.User.PhoneNumber == dto.PhoneNumber.Trim() &&
                               DbFunctions.TruncateTime(o.OrderDate) == targetDate.Date &&
                               o.OrderStatus == OrderStatus.DaGiao)
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
    }
}