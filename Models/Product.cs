using API.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SHOPAPI.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public long Price { get; set; }
        public string ImageUrl { get; set; }
        public bool IsOutOfStock { get; set; }
        public virtual ICollection<Category> Categories { get; set; }
        public virtual ICollection<OrderItem> OrderItems { get; set; }
        public virtual ICollection<CartItem> CartItems { get; set; }
    }
}