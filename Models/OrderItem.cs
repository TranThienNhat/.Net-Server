using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SHOPAPI.Models
{
    public class OrderItem
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        public int Quantity { get; set; }
        public long PriceAtPurchase { get; set; }
    }
}