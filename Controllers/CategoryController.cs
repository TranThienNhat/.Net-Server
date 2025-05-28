using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using SHOPAPI.Data;
using SHOPAPI.DTOs.Categories;
using SHOPAPI.Models;

namespace SHOPAPI.Controllers
{
    public class CategoryController : ApiController
    {
        private readonly AppDbContext db = new AppDbContext();

        [Authorize(Roles = "ADMIN")]
        [HttpGet]
        [Route("api/category")]
        public IHttpActionResult GetCategory()
        {
            var categories = db.Categories.Select(c => new CategoryReadDto
            {
                Id = c.Id,
                Name = c.Name,
            }).ToList();

            return Ok(categories);
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPost]
        [Route("api/category")]
        public IHttpActionResult createCategory([FromBody] CategoryReadDto categoryDto)
        {
            if (categoryDto == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            var category = new Category
            {
                Name = categoryDto.Name
            };

            db.Categories.Add(category);
            db.SaveChanges();

            return Ok(new { message = "Thêm sản phẩm thành công!", category.Id });
        }

        [Authorize(Roles = "ADMIN")]
        [HttpPut]
        [Route("api/category/{id}")]
        public IHttpActionResult UpdateCategory(int id, [FromBody] CategoryReadDto categoryDto)
        {
            if (categoryDto == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            var category = db.Categories.Find(id);
            if (category == null)
            {
                return NotFound();
            }

            category.Name = categoryDto.Name;
            db.SaveChanges();

            return Ok(new { message = $"Danh mục có ID {id} đã được cập nhật thành công!", category });
        }


        [Authorize(Roles = "ADMIN")]
        [HttpDelete]
        [Route("api/category/{id}")]
        public IHttpActionResult DeleteCategory(int id)
        {
            var category = db.Categories.Find(id);
            if (category == null)
            {
                return NotFound();
            }

            db.Categories.Remove(category);
            db.SaveChanges();

            return Ok($"Danh mục có ID {id} đã được xóa thành công.");
        }
    }
}
