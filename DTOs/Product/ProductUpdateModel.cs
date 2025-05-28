using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace API.DTOs.Product
{
    public class ProductUpdateModel
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public long Price { get; set; }
    }
}