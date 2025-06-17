using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace API.DTOs.Cart
{
    public class CreateOrderFromCartDto
    {
        public int CartItemId { get; set; }
        public string Note { get; set; }
    }
}