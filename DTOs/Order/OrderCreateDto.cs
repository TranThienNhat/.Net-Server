using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SHOPAPI.DTOs.Order
{
    public class OrderCreateDto
    {
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Note { get; set; }
        public List<OrderItemDto> Items { get; set; }
        public DateTime OrderDate { get; set; }
    }
}