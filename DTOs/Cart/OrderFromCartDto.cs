using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace API.DTOs.Cart
{
    public class OrderFromCartDto
    {
        public List<int> CartItemIds { get; set; }
    }
}