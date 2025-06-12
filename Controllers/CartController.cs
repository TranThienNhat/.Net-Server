using API.DTOs.Cart;
using API.Models;
using SHOPAPI.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;

namespace API.Controllers
{
    public class CartController : ApiController
    {
        private readonly AppDbContext db = new AppDbContext();

        [Authorize(Roles = "USER")]
        [HttpGet]
        [Route("api/cart")]
        public IHttpActionResult GetCart()
        {
            var identity = (ClaimsIdentity)User.Identity;
            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            int userId = Convert.ToInt32(userIdClaim.Value);

            var cart = db.Carts.FirstOrDefault(c => c.UserId == userId);
            if (cart == null)
                return NotFound();

            var items = db.CartItems
                .Where(ci => ci.CartId == cart.Id)
                .Select(ci => new CartItemReadDto
                {
                    CartItemId = ci.Id,
                    ProductId = ci.ProductId,
                    Name = ci.Product.Name,
                    ImageUrl = ci.Product.ImageUrl,
                    Price = ci.Product.Price,
                    Quantity = ci.Quantity
                })
                .ToList();

            return Ok(new
            {
                items
            });
        }

        [Authorize(Roles = "USER")]
        [HttpPost]
        [Route("api/cart/add")]
        public IHttpActionResult AddToCart(AddCartItemDto dto)
        {
            if (dto == null || dto.ProductId <= 0 || dto.Quantity <= 0)
                return BadRequest("Dữ liệu không hợp lệ.");

            var identity = (ClaimsIdentity)User.Identity;
            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            int userId = Convert.ToInt32(userIdClaim.Value);

            var cart = db.Carts.FirstOrDefault(c => c.UserId == userId);
            if (cart == null)
                return NotFound();

            var product = db.Products.FirstOrDefault(p => p.Id == dto.ProductId);
            if (product == null)
                return BadRequest("Sản phẩm không tồn tại.");

            var existingItem = db.CartItems
                .FirstOrDefault(ci => ci.CartId == cart.Id && ci.ProductId == dto.ProductId);

            if (existingItem != null)
            {
                existingItem.Quantity += dto.Quantity;
            }
            else
            {
                var newCartItem = new CartItem
                {
                    CartId = cart.Id,
                    ProductId = dto.ProductId,
                    Quantity = dto.Quantity
                };

                db.CartItems.Add(newCartItem);
            }

            db.SaveChanges();

            return Ok(new
            {
                message = "Thêm sản phẩm vào giỏ hàng thành công.",
                cartId = cart.Id
            });
        }

        [Authorize(Roles = "USER")]
        [HttpDelete]
        [Route("api/cart/item/{id}")]
        public IHttpActionResult DeleteCartItem(int id)
        {
            var identity = (ClaimsIdentity)User.Identity;
            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            int userId = Convert.ToInt32(userIdClaim.Value);

            var cartItem = db.CartItems
                .FirstOrDefault(ci => ci.Id == id && ci.Cart.UserId == userId);
            if (cartItem == null)
                return NotFound();

            db.CartItems.Remove(cartItem);
            db.SaveChanges();

            return Ok(new { message = "Đã xóa sản phẩm khỏi giỏ hàng." });
        }

        [Authorize(Roles = "USER")]
        [HttpDelete]
        [Route("api/cart/clear")]
        public IHttpActionResult ClearCart()
        {
            var identity = (ClaimsIdentity)User.Identity;
            var userIdClaim = identity.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized();

            int userId = Convert.ToInt32(userIdClaim.Value);

            var cart = db.Carts.FirstOrDefault(c => c.UserId == userId);
            if (cart == null) return NotFound();

            var cartItems = db.CartItems.Where(ci => ci.CartId == cart.Id).ToList();
            if (!cartItems.Any())
                return Ok(new { message = "Giỏ hàng đã trống." });

            db.CartItems.RemoveRange(cartItems);
            db.SaveChanges();

            return Ok(new { message = "Đã xóa toàn bộ giỏ hàng." });
        }
    }
}
