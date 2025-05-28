using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using SHOPAPI.Models.Enum;

namespace SHOPAPI.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string Note { get; set; }
        public DateTime OrderDate { get; set; }
        public long TotalPrice { get; set; }
        public OrderStatus orderStatus { get; set; }
        public virtual ICollection<OrderItem> OrderItems { get; set; }
    }

}