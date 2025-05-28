using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace SHOPAPI.DTOs.Product
{
    public class ProductReadDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Quantity { get; set; }
        public long Price { get; set; }
        public string ImageUrl { get; set; }
        public bool IsOutOfStock { get; set; }
        public List<string> Categories { get; set; }
    }
}