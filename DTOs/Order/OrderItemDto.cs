using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SHOPAPI.DTOs.Order
{
    public class OrderItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}