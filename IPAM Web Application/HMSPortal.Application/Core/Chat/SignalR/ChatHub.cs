﻿using Azure;
using HMSPortal.Application.AppServices.IServices;
using HMSPortal.Application.Core.Cache;
using HMSPortal.Application.Core.Chat.Bot;
using HMSPortal.Application.Core.Chat.Message;
using HMSPortal.Application.Core.MessageBrocker.KafkaBus;
using HMSPortal.Application.ViewModels.Chat;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Protocol.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HMSPortal.Application.Core.Chat.SignalR
{
    public class ChatHub : Hub
    {

        private readonly ResponseModerator _responseModerator;
        private static readonly ConcurrentDictionary<string, ChatTempData> UserConnections = new ConcurrentDictionary<string,ChatTempData>();
        public ChatHub(ResponseModerator responseModerator)
        {
            _responseModerator=responseModerator;
 
        }
        public static void AddOrUpdateUserConnection(string userId, string connectionId)
        {
            var userConnection = new ChatTempData { UserId = userId, ConnectionId = connectionId };
            UserConnections.AddOrUpdate(userId, userConnection, (key, oldValue) => userConnection);
        }
        public override Task OnConnectedAsync()
        {
            var userId = Context.User.GetUserId();
            if (userId != null)
            {
                // Store the connection ID associated with the user ID
                AddOrUpdateUserConnection(userId, Context.ConnectionId);
               
            }

            return base.OnConnectedAsync();
        }
        public async Task SendGreeting(string user, string message)
        {
            var response = new ChatResponse();
            if (message == "WelcomeMessage")
            {    response = await _responseModerator.GetGreeing(message, user);
                await Clients.All.SendAsync(response.Endpoint, "Bot", response.Message);

            }
            else
            {
                await Clients.All.SendAsync(response.Endpoint, "Bot", response);
            }


        }
        public async Task SendMessage(string user, string message)
        {

            List<string> menu = new List<string> { "Schedule", "Cancel" , "Reschedule", "Exit" };

			//var response = GetMenu(message);
			var receievdChat = new BotMessageViewModel
			{
				Content = message,
				UserId = user,
				Type = CoreValiables.ChatSent,
				HasOptions = true,
                Options = JsonConvert.SerializeObject(menu)

			};
           await _responseModerator.Savemessage(receievdChat);

			await Clients.All.SendAsync("ReceiveMenu", "Bot", menu);
        }
        public async Task SendSheduleCategory(string user, string message)
        {
            if (UserConnections.TryGetValue(user, out ChatTempData chatTempData))
            {
                chatTempData.ScheduleType = message;
                // Alternatively, if you want to add to existing questions:
                // chatTempData.Questions.AddRange(newQuestions);

                // Update the dictionary entry to reflect the changes
                UserConnections[user] = chatTempData;
            }
            if (message == "Check-ups")
            {
                
                await Clients.All.SendAsync("ReceiveMessage", "Bot", "Please briefly describe the reason for your check-up, such as general wellness, routine monitoring, or follow-up on a previous condition");

            }
            else
            {
                await Clients.All.SendAsync("ReceiveMessage", "Bot", "Please describe the symptoms or health issues you are experiencing.\nInclude any relevant details such as the onset of symptoms, their severity, and how they have been affecting your daily life");

            }
        }
        public async Task ValidateHealthCondition(string user, string message)
        {
            var respo = await _responseModerator.ValideHealthCondition(message, user);
			

			if (respo.Messages != null && respo.Messages.Any()) 
            {
                if (UserConnections.TryGetValue(user, out ChatTempData chatTempData))
                {
                    chatTempData.HealthCondition = message;
                    chatTempData.Questions = respo.Messages;
                    // Alternatively, if you want to add to existing questions:
                    // chatTempData.Questions.AddRange(newQuestions);

                    // Update the dictionary entry to reflect the changes
                    UserConnections[user] = chatTempData;
                }
				
				await Clients.All.SendAsync("ReceiveQuestions", "Bot", respo.Messages.ToList());
            }
            else
            {
				
				await Clients.All.SendAsync(respo.Endpoint, "Bot", respo.Message);

            }

        }

        public async Task SubmitQuestions(string userId, string answersJson)
        {
			var receievdChat = new BotMessageViewModel
			{
				Content = answersJson,
				UserId = userId,
				Type = CoreValiables.ChatRecieved,
				HasOptions = false

			};
			await _responseModerator.Savemessage(receievdChat);

			if (UserConnections.TryGetValue(userId, out ChatTempData chatTempData))
            {

                chatTempData.QuestionsAndAnswers = answersJson;
                UserConnections[userId] = chatTempData;
            }
            var response = await _responseModerator.AnalyseSymmtonFeedback(answersJson, userId, chatTempData.HealthCondition);
            if(response.validation_status.Contains("VALID"))
            {
                chatTempData.Result = response.message;
                UserConnections[userId] = chatTempData;
                var mesg = "Please select a date suitable for your appointment;";
				var sentChat = new BotMessageViewModel
				{
					Content = mesg,
					UserId = userId,
					Type = CoreValiables.ChatSent,
					HasOptions = false

				};
				await _responseModerator.Savemessage(sentChat);
				await Clients.All.SendAsync("ShowDatePicker", "Bot", mesg);
           
            }
            else
            {
                var mesg = "Please select a date suitable for your appointment\";\r\n";
				var sentChat = new BotMessageViewModel
				{
					Content = mesg,
					UserId = userId,
					Type = CoreValiables.ChatSent,
					HasOptions = false

				};
				await _responseModerator.Savemessage(sentChat);
				await Clients.All.SendAsync("ShowDatePicker", "Bot", mesg);

            }
            // Process the answers as needed
        }

        public async Task SendSymtoms(string user, string message)
        {
            var response = await _responseModerator.ValideHealthCondition(user, message);
            await Clients.All.SendAsync("ReceiveMessage", "Bot", "Please describe the symptoms or health issues you are experiencing.\nInclude any relevant details such as the onset of symptoms, their severity, and how they have been affecting your daily life");

        }

        public async Task ReadMenu(string user, string message)
        {
           if(message == "Menu")
            {

                List<string> menu = new List<string> { "Schedule", "Cancel", "Reschedule", "Exit" };
				var receievdChat = new BotMessageViewModel
				{
					Content = message,
					UserId = user,
					Type = CoreValiables.ChatSent,
					HasOptions = true,
					Options = JsonConvert.SerializeObject(menu)

				};
				await _responseModerator.Savemessage(receievdChat);

				await Clients.All.SendAsync("ReceiveMenu", "Bot", menu);
            }
           else if(message.Contains("Check-ups") || message.Contains("New Health Concerns"))
            {
                if (UserConnections.TryGetValue(user, out ChatTempData chatTempData))
                {
                    chatTempData.ScheduleType = message;
                    // Alternatively, if you want to add to existing questions:
                    // chatTempData.Questions.AddRange(newQuestions);

                    // Update the dictionary entry to reflect the changes
                    UserConnections[user] = chatTempData;
                }
                var sysMessage = "Please describe the symptoms or health issues you are experiencing.\nInclude any relevant details such as the onset of symptoms, their severity, and how they have been affecting your daily life";
                if (message == "Check-ups")
                {
                    sysMessage = "Please briefly describe the reason for your check-up, such as general wellness, routine monitoring, or follow-up on a previous condition";
                    //Log User and Bot
                   


                }
				var receievdChat = new BotMessageViewModel
				{
					Content = message,
					UserId = user,
					Type = CoreValiables.ChatRecieved,
					HasOptions = false,

				};
				await _responseModerator.Savemessage(receievdChat);

				var sentChat = new BotMessageViewModel
				{
					Content = sysMessage,
					UserId = user,
					Type = CoreValiables.ChatSent,
					HasOptions = false,

				};
				await _responseModerator.Savemessage(sentChat);
				await Clients.All.SendAsync("ReceieveSheduleCategory", "Bot", sysMessage);
			}
            else
            {
				
				if (UserConnections.TryGetValue(user, out ChatTempData chatTempData))
                {
                    MenuType menu = (MenuType)Enum.Parse(typeof(MenuType), message);
                    chatTempData.MenuType = menu;
                    // Alternatively, if you want to add to existing questions:
                    // chatTempData.Questions.AddRange(newQuestions);

                    // Update the dictionary entry to reflect the changes
                    UserConnections[user] = chatTempData;
                }
                var response = await _responseModerator.ReadMenuAsync(message, user);
                
                await Clients.All.SendAsync(response.Endpoint, "Bot", response.Message);


            }

        }

        public async Task SendAppointments(string user, string message)
        {
            var appontments = await _responseModerator.GetAppointmentByUser(message, user);

            if( message == "Reschedule")
            {
                await Clients.All.SendAsync("ReceiveReschedule", user, appontments);

            }
            else
            {
                await Clients.All.SendAsync("ReceiveCancleSchedule", user, appontments);

            }

            // await Clients.All.SendAsync(botResponse.Endpoint, user, botResponse.Message);


        }

        public async Task SendCancellation(string user, string message)
        {
            if (UserConnections.TryGetValue(user, out ChatTempData chatTempData))
            {
                chatTempData.MenuType = MenuType.Cancel;
          
                UserConnections[user] = chatTempData;
            }
            var appontments = await _responseModerator.CancelAppointmentAsync(message, user);

            // await Clients.All.SendAsync(botResponse.Endpoint, user, botResponse.Message);
            await Clients.All.SendAsync(appontments.Endpoint, user, appontments.Message);

        }

        public async Task SendRescheduleDate(string user, string message)
        {
            if (UserConnections.TryGetValue(user, out ChatTempData chatTempData))
            {
                chatTempData.MenuType = MenuType.Reschedule;
                // Alternatively, if you want to add to existing questions:
                // chatTempData.Questions.AddRange(newQuestions);

                // Update the dictionary entry to reflect the changes
                UserConnections[user] = chatTempData;
            }
            var appontments = await _responseModerator.GetAppointmentByUser(message, user);

            // await Clients.All.SendAsync(botResponse.Endpoint, user, botResponse.Message);
            await Clients.All.SendAsync("ReceiveReschedule", user, message);


        }

        public async Task SendDate(string user, string message)
        {
            var resp =  await _responseModerator.GetAvaialbleSlotsAsync(message, user);
            if(resp.ResponseType == ResponseType.DropDown)
            {
                var date = DateTime.Parse(message);
                if (UserConnections.TryGetValue(user, out ChatTempData chatTempData))
                {

                    chatTempData.Date = date;
                    UserConnections[user] = chatTempData;
                }
                await Clients.All.SendAsync(resp.Endpoint, user, resp.Messages);

            }
            else
            {
                await Clients.All.SendAsync(resp.Endpoint, user, resp.Message);
            }

            // await Clients.All.SendAsync(botResponse.Endpoint, user, botResponse.Message);


        }

        public async Task SendSyncDate(string user, string message)
        {
            if (UserConnections.TryGetValue(user, out ChatTempData chatTempData))
            {
                chatTempData.MenuType = MenuType.Reschedule;
                chatTempData.AppointmentId = message;
                UserConnections[user] = chatTempData;
            }
            var msg = "Kindly select a date";

            // await Clients.All.SendAsync(botResponse.Endpoint, user, botResponse.Message);
            await Clients.All.SendAsync("ShowDatePicker", user, msg);


        }
        public async Task SendDescription(string user, string message)
        {
            var botResponse =  await _responseModerator.ReadMessageAsync(message, user);

            // await Clients.All.SendAsync(botResponse.Endpoint, user, botResponse.Message);
            await Clients.All.SendAsync("ShowDatePicker", user, message);


        }
        public async Task SendReschedule(string user, string message)
        {
            var botResponse = await _responseModerator.ReadMessageAsync(message, user);

            // await Clients.All.SendAsync(botResponse.Endpoint, user, botResponse.Message);
            await Clients.All.SendAsync("ShowDatePicker", user, message);


        }
        public async Task BookAppointment(string user, string message)
        {
            if (UserConnections.TryGetValue(user, out ChatTempData chatTempData))
            {
                if(message.Contains("@"))
                {
                    chatTempData.Slot = message.Split('@')[0];

					var receievdChat = new BotMessageViewModel
					{
						Content = chatTempData.Slot,
						UserId = user,
						Type = CoreValiables.ChatRecieved,
						HasOptions = false,

					};
					await _responseModerator.Savemessage(receievdChat);
				}

                UserConnections[user] = chatTempData;
            }

            if(chatTempData.MenuType == MenuType.Reschedule)
            {
               var response =  await _responseModerator.RescheduleAppointment(user, chatTempData);
				await Clients.All.SendAsync("ReceiveSuccessMessage", user, response.Message);
			}
            else
            {
                var botResponse = await _responseModerator.BookAppointmentAsync(message, user, chatTempData);

                // await Clients.All.SendAsync(botResponse.Endpoint, user, botResponse.Message);
                await Clients.All.SendAsync("ReceiveSuccessMessage", user, botResponse.Message);
            }


        }

       

    }
}
