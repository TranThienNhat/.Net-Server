using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace API.DTOs.Order
{
    public class OrderReportRequestDto
    {
        [Required]
        [Phone]
        public string PhoneNumber { get; set; }

        public DateTime? OrderDate { get; set; }

        [Required]
        public bool SendConfirmation { get; set; }
    }
}