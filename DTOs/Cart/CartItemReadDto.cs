using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace API.DTOs.Cart
{
    public class CartItemReadDto
    {
        public int CartItemId { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
        public long Price { get; set; }
        public int Quantity { get; set; }
    }
}