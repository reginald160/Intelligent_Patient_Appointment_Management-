﻿using HMS.Infrastructure.Repositories.IRepository;
using HMSPortal.Application.AppServices.IServices;
using HMSPortal.Application.Core.Helpers;
using HMSPortal.Application.Core;
using HMSPortal.Application.Core.Response;
using HMSPortal.Application.ViewModels;
using HMSPortal.Domain.Enums;
using HMSPortal.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HMS.Infrastructure.Persistence.DataContext;
using HMSPortal.Application.ViewModels.Patient;
using HMSPortal.Application.ViewModels.Doctor;
using Microsoft.EntityFrameworkCore;
using HMSPortal.Application.Core.MessageBrocker.EmmaBrocker;
using Newtonsoft.Json;
using HMSPortal.Application.Core.Notification;
using HMSPortal.Application.Core.Notification.Email;
using Twilio.TwiML.Messaging;

namespace HMS.Infrastructure.Repositories.Repository
{
	public class DoctorRepo : IDoctorServices
	{
		private readonly ApplicationDbContext _db;
		private readonly IIdentityRespository _identityRespository;
		private readonly IMessageBroker _messageBroker;
		private readonly INotificatioServices notificatioServices;

        public DoctorRepo(ApplicationDbContext db, IIdentityRespository identityRespository, IMessageBroker messageBroker, INotificatioServices notificatioServices)
        {
            _db=db;
            _identityRespository=identityRespository;
            _messageBroker=messageBroker;
            this.notificatioServices=notificatioServices;
        }

        public bool CheckExistingDoctor ( string email)
		{
			return _identityRespository.ExistingUserEmail(email);
		}
        public List <GetDoctorViewModel> GetAllDoctors()
        {
            var clockins = _db.UserClockIns.Where(x => x.ClockInTime.Value.Date.Equals(DateTime.UtcNow.Date))
                .Select(x => x.UserId).ToList();
            var doctors = _db.Doctors.Where(x => !x.IsDeleted).Select (viewModel => new GetDoctorViewModel
            {
                Id = viewModel.Id,
                FirstName = viewModel.FirstName,
                LastName = viewModel.LastName,
                DoctorCode = viewModel.DoctorCode,
                Specialty = viewModel.Specialty,
                Age = viewModel.Age,
                BackgroundHistory = viewModel.BackgroundHistory,
                Phone = viewModel.Phone,
                DateOfBirth = viewModel.DateOfBirth,
                Address = viewModel.Address,
                Gender = viewModel.Gender,
				IsActive = clockins.Contains(viewModel.UserId),
                Email = viewModel.Email,
                PostalCode = viewModel.PostalCode,
                HouseNumber = viewModel.HouseNumber,
                ImageUrl = viewModel.ImageUrl,
                UserId = viewModel.UserId,
            }).ToList();

			return doctors;
        }
		public async Task UpdateDoctorAsync(EditDoctorViewModel model)
		{
			var doctor = await _db.Doctors.FindAsync(model.Id);

			if (doctor == null)
			{
				throw new Exception("Doctor not found");
			}


			var viewModelProperties = typeof(EditDoctorViewModel).GetProperties();
			var entityProperties = typeof(Doctor).GetProperties().Select(p => p.Name).ToHashSet();

			foreach (var property in viewModelProperties)
			{
				if (entityProperties.Contains(property.Name))
				{
					var newValue = property.GetValue(model);
					var currentValue = typeof(Doctor).GetProperty(property.Name)?.GetValue(doctor);

					if (newValue != null && !newValue.Equals(currentValue))
					{
						typeof(Doctor).GetProperty(property.Name)?.SetValue(doctor, newValue);
						_db.Entry(doctor).Property(property.Name).IsModified = true;
					}
				}
			}

			await _db.SaveChangesAsync();
		}
		public async Task<Dictionary<string, Guid>> GetAllDoctorsDroptDown(string department = null)
		{
			var doctors = _db.Doctors
			   .Where(x => !x.IsDeleted && x.IsActive);

            if (department != null)
			{
                doctors = doctors.Where(x=> x.Specialty == department);
            }
            var result = doctors.Select(m => new
                {
                    FullName = m.FirstName + " " + m.LastName,
                    Id = m.Id
                })
                .ToDictionary(d => d.FullName, d => d.Id);
            return result;
        }
	
		public GetDoctorViewModel GetDoctorById(Guid id)
		{
			var viewModel = _db.Doctors.FirstOrDefault(x => x.Id == id);

			return new GetDoctorViewModel
			{
				Id = viewModel.Id,
				FirstName = viewModel.FirstName,
				LastName = viewModel.LastName,
				DoctorCode = viewModel.DoctorCode,
				Specialty = viewModel.Specialty,
				YearsOfExperience = viewModel.YearsOfExperience,
				Age = viewModel.Age,
				BackgroundHistory = viewModel.BackgroundHistory,
				Phone = viewModel.Phone,
				DateOfBirth = viewModel.DateOfBirth,
				Address = viewModel.Address,
				Gender = viewModel.Gender,
				Email = viewModel.Email,
				PostalCode = viewModel.PostalCode,
				HouseNumber = viewModel.HouseNumber,
				ImageUrl = viewModel.ImageUrl,
				UserId = viewModel.UserId,
			};
		}
		public async Task<AppResponse> CreateDoctor(AddDoctorViewModel viewModel)
		{
			bool existingUser = _db.Users.Any(x=> x.Email == viewModel.Email);
			if(existingUser)
			{
				return new AppResponse
				{
					Message = "User with email " + viewModel.Email+" already exist",
					ResponseCode = "01",
					IsSuccessful = false,


				};
			}



			var userId = await _identityRespository.CreateUser(viewModel.Email ?? "Admin@gmail.com", viewModel.Password, Roles.Doctor);
			var seqNumber = await new SequenceContractHelper().GenerateNextPatientNumberAsync(3);

			var doctorModel = new Doctor
			{
				FirstName = viewModel.FirstName,
				Age = viewModel.Age,
				YearsOfExperience = viewModel.YearsOfExperience,
				Specialty = viewModel.Specialty,
				LastName = viewModel.FirstName,
				DoctorCode = "DT/"+ seqNumber.ToString(CoreValiables.SequenceNumberFormat),
				Phone = viewModel.Phone,
				DateOfBirth = viewModel.DateOfBirth,
				Address = viewModel.Address,
				Gender = viewModel.Gender,
				Email = viewModel.Email,
				DoctorDetails = viewModel.DoctorDetails,
				PostalCode = "",
				HouseNumber = "",
				ImageUrl = viewModel.ImageUrl,
				SerialNumber = "PD/"+ seqNumber.ToString(CoreValiables.SequenceNumberFormat),
				UserId = userId,

			};
			try
			{
				var response = await _db.Doctors.AddAsync(doctorModel);
				await _db.SaveChangesAsync();
				await new SequenceContractHelper().UpdateSequence(seqNumber, 3);
				var token = await _identityRespository.GenerateEmailConfirmationLinkAsync(doctorModel.Email);
				viewModel.ImageUrl = token;
                var emailModel = new DoctorSignupEmailModel
                {
                    Specialization = viewModel.Specialty,
                    SetPasswordLink =token ,
                    DoctorName = viewModel.FirstName,
                    LogoUrl = "https://res.cloudinary.com/dukd0jnep/image/upload/v1718523325/ehxwqremdpkwqvshlhhy.jpg",
                    BGImageUrl = "https://res.cloudinary.com/dukd0jnep/image/upload/v1718523325/ehxwqremdpkwqvshlhhy.jpg",
                };
                var brockerMessage = JsonConvert.SerializeObject(emailModel);

                //await Task.Run(async () =>
                //{
                //    await notificatioServices.SendDoctorSignUpEmail(emailModel);
                //});
                await _messageBroker.PublishAsync(CoreValiables.ConifrmDoctorSignUp, brockerMessage);

                return new AppResponse
				{
					IsSuccessful = true,
					Data  = response.Entity.Id
				};


			}
			catch (Exception ex)
			{
				await _identityRespository.DeleteUser(viewModel.Email);
				return new AppResponse { IsSuccessful = false };
			}
		}

        public async Task<AppResponse> DeleteDoctor(Guid id)
        {
            try
            {
                var doctor = _db.Doctors.FirstOrDefault(x => x.Id ==id);
                doctor.IsDeleted = true;
                _db.Doctors.Update(doctor);
                await _db.SaveChangesAsync();
                return new AppResponse { IsSuccessful = true };
            }
            catch (Exception ex)
            {
                return new AppResponse
                {
                    Message = ex.Message,
                };
            }
        }
		public bool GetUserClockIn(string doctorId)
		{
			// Check if the doctor has already clocked in today
			var today = DateTime.UtcNow.Date;
			return  _db.UserClockIns
				.Where(c => c.UserId == doctorId && c.ClockInTime.HasValue && c.ClockInTime.Value.Date == today)
				.Any();
		
		}

		public async Task<string> ClockInAsync(string doctorId)
		{
			// Check if the doctor has already clocked in today
			try
			{
				var today = DateTime.UtcNow.Date;
				var existingClockIn = await _db.UserClockIns
					.Where(c => c.UserId == doctorId && c.ClockInTime.HasValue && c.ClockInTime.Value.Date == today)
					.FirstOrDefaultAsync();

				if (existingClockIn != null)
				{
					return "Doctor has already clocked in today.";
				}

				var clockIn = new UserClockIn
				{
					UserId = doctorId,
					ClockInTime = DateTime.UtcNow,
					DateCreated = DateTime.UtcNow,
					ClockOutTime = null,
				};

				_db.UserClockIns.Add(clockIn);

				var doctor = _db.Doctors.FirstOrDefault(x => x.UserId == doctorId);
				if (doctor != null)
				{
					doctor.IsActive = true;
				}

				_db.Doctors.Update(doctor);
				await _db.SaveChangesAsync();
				return "1@Clocked in successfully.";
			}
			catch (Exception ex) {
				return "0@Clocked in was not successfull";
			}
		}

		public async Task<string> ClockOutAsync(string doctorId)
		{
			var clocakIn = _db.UserClockIns.FirstOrDefault(c => c.UserId == doctorId);


			var clockIn = await _db.UserClockIns
				.Where(x=> x.DateCreated.Date == DateTime.UtcNow.Date)
				
				.Where(c => c.UserId == doctorId && c.ClockOutTime == null)
				.OrderByDescending(c => c.ClockInTime)
				.FirstOrDefaultAsync();
			try
			{

				if (clockIn != null)
				{
					clockIn.ClockOutTime = DateTime.UtcNow;
					_db.UserClockIns.Update(clockIn);


					var doctor =  _db.Doctors.FirstOrDefault(x=> x.UserId == doctorId);
					if (doctor != null)
					{
						doctor.IsActive = false;
					}

					_db.Doctors.Update(doctor);
					await _db.SaveChangesAsync();
					return "1@Clocked out successfully.";
				}

				return "0@No active clock-in found for today.";
			}
			catch (Exception ex)
			{
				return "0@Error occured while clocking out.";
			}

		}

	}
}
