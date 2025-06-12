using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SHOPAPI.DTOs.Order
{
    public class OrderCreateDto
    {
        public string Note { get; set; }
        public List<OrderItemDto> Items { get; set; }
        public DateTime OrderDate { get; set; }
    }
}