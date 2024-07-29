﻿
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HMSPortal.Application.ViewModels
{
    public class AddPatientViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string HouseNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
		[DataType(DataType.DateTime)]
		public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = string.Empty;
		[Required]
        [DataType(DataType.EmailAddress)]
		public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
  

		public IFormFile? Image { get; set; }
        public int Weight { get; set; }
        public int Height { get; set; }
        public Double BMI { get; set; }
		public string? ImageUrl { get; set; }

        public string? Password { get; set; }
		public string? PatientCode { get; set; }
		public string? UserId { get; set; }
	}
}
