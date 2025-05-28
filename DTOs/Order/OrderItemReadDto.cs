using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SHOPAPI.DTOs.Order
{
    public class OrderItemReadDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public long PriceAtPurchase { get; set; }
    }
}