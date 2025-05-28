using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using API.DTOs.Product;
using SHOPAPI.Data;
using SHOPAPI.DTOs;
using SHOPAPI.DTOs.Product;
using SHOPAPI.Models;

namespace SHOPAPI.Controllers
{
    public class ProductController : ApiController
    {
        private readonly AppDbContext db = new AppDbContext();

        [HttpGet]
        [Route("api/products")]
        public IHttpActionResult GetProducts(int? categoryId =  null)
        {
            var query = db.Products.Include(p => p.Categories).AsQueryable();
            if (categoryId.HasValue)
            {
                query = query.Where(p => p.Categories.Any(c => c.Id == categoryId.Value));
            }

            var result = query.ToList().Select(p => new ProductReadDto 
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Quantity = p.Quantity,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                IsOutOfStock = p.IsOutOfStock,
                Categories = p.Categories.Select(c => c.Name).ToList()
            }).ToList();

            return Ok(result);
        }

        [HttpGet]
        [Route("api/products/{id}")]
        public IHttpActionResult GetProductById(int id)
        {
            var product = db.Products.Include(p => p.Categories)
                .FirstOrDefault(p => p.Id == id);

            if (product == null)
            {
                return NotFound();
            }

            var result = new ProductReadDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Quantity = product.Quantity,
                Price = product.Price,
                ImageUrl = product.ImageUrl,
                IsOutOfStock = product.IsOutOfStock,
                Categories = product.Categories.Select(c => c.Name).ToList()
            };

            return Ok(result);
        }


        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        [Route("api/products")]
        public IHttpActionResult AddProduct()
        {
            var httpRequest = HttpContext.Current.Request;

            // Đọc dữ liệu sản phẩm từ form
            var name = httpRequest.Form["Name"];
            var description = httpRequest.Form["Description"];
            var quantity = int.Parse(httpRequest.Form["Quantity"]);
            var price = long.Parse(httpRequest.Form["Price"]);
            var categoryIds = httpRequest.Form["CategoryIds"]?.Split(',').Select(int.Parse).ToList();

            // Xử lý file ảnh
            var postedFile = httpRequest.Files["Image"];
            string imageUrl = "";
            if (postedFile != null && postedFile.ContentLength > 0)
            {
                var fileName = Path.GetFileName(postedFile.FileName);
                var path = HttpContext.Current.Server.MapPath("~/Uploads/" + fileName);
                postedFile.SaveAs(path);
                imageUrl = "/Uploads/" + fileName;
            }

            // Tạo đối tượng sản phẩm
            var product = new Product
            {
                Name = name,
                Description = description,
                Quantity = quantity,
                Price = price,
                ImageUrl = imageUrl,
                IsOutOfStock = quantity == 0,
                Categories = db.Categories.Where(c => categoryIds.Contains(c.Id)).ToList()
            };

            db.Products.Add(product);
            db.SaveChanges();

            return Ok(new { message = "Thêm sản phẩm thành công!", product.Id });
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPut]
        [Route("api/products/{id}")]
        public IHttpActionResult UpdateProduct(int id, [FromBody] ProductUpdateModel model)
        {
            if (model == null)
            {
                return BadRequest("Dữ liệu không hợp lệ");
            }

            var product = db.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            // Cập nhật tất cả các trường
            product.Name = model.Name ?? string.Empty;
            product.Description = model.Description ?? string.Empty;
            product.Quantity = model.Quantity;
            product.Price = model.Price;
            product.IsOutOfStock = model.Quantity == 0;

            db.SaveChanges();

            return Ok(new
            {
                message = "Cập nhật sản phẩm thành công!",
                product = new
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Quantity = product.Quantity,
                    Price = product.Price,
                    IsOutOfStock = product.IsOutOfStock
                }
            });
        }

        [Authorize(Roles = "ADMIN")]
        [HttpDelete]
        [Route("api/products/{id}")]
        public IHttpActionResult DeleteProduct(int id)
        {
            var product = db.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }

            db.Products.Remove(product);
            db.SaveChanges();

            return Ok(new { message = "Xóa sản phẩm thành công!" });
        }

    }
}
