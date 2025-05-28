using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SHOPAPI.DTOs.Product
{
    public class UpdateStockDto
    {
        public int ProductId { get; set; }
        public int QuantitySold { get; set; }
    }
}