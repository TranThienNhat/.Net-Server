using SHOPAPI.Models.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using static Microsoft.ApplicationInsights.MetricDimensionNames.TelemetryContext;

namespace SHOPAPI.Models
{
    public class Order
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Note { get; set; }
        public DateTime OrderDate { get; set; }
        public long TotalPrice { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public virtual Users User { get; set; }
        public virtual ICollection<OrderItem> OrderItems { get; set; }
    }

}