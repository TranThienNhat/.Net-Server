using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace API.DTOs.Cart
{
    public class AddCartItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}