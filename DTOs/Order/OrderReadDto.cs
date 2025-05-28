using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SHOPAPI.DTOs.Order
{
    public class OrderReadDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Note { get; set; }
        public DateTime OrderDate { get; set; }
        public long TotalPrice { get; set; }
        public string OrderStatus { get; set; }
        public List<OrderItemReadDto> OrderItems { get; set; }
    }
}