using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MobileServices;
using NandosoBot.DataModels;

namespace NandosoBot
{
	[BotAuthentication]
	public class MessagesController : ApiController
	{
		/// <summary>
		/// POST: api/Messages
		/// Receive a message from a user and reply to it
		/// </summary>
		public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
		{
			if (activity.Type == ActivityTypes.Message)
			{
				string message = activity.Text;
				string botReply = "";




				ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));

				// Setup and get user state data
				StateClient sc = activity.GetStateClient();
				BotData userData = await sc.BotState.GetUserDataAsync(activity.ChannelId, activity.From.Id);

				string userName = userData.GetProperty<string>("userName") ?? "";

				// If bot has asked for username
				if (userData.GetProperty<bool>("askedForUserName"))
				{
					// If username had only just been given
					if (userName == "")
					{
						userData.SetProperty<string>("userName", message);
						botReply = $"Hi {userData.GetProperty<string>("userName")}, what would you like to do today?";
					}
					// If username had been given beforehand already
					else
					{
						if (userData.GetProperty<bool>("gotActivity"))
						{
							if (userData.GetProperty<bool>("askedForDelivery"))
							{
								if (message.ToLower().Trim() == "domestic")
								{
									botReply = "We do free deliveries domestically!";
								}
								else if (message.ToLower().Trim() == "international")
								{
									botReply = "What country do you want to deliver to?";
									userData.SetProperty("askedForCountry", true);
								}
							}
							else if (userData.GetProperty<bool>("complaint"))
							{
								// well, user wants to complain. deal with it
							}
						}
						else
						{
							// If customer is making a complaint
							if (message.ToLower().Contains("complaint") || message.ToLower().Contains("complain"))
							{
								botReply = "I'm sorry that you are not satisfied with us. How may I help?";
								userData.SetProperty<bool>("gotActivity", true);
								userData.SetProperty<bool>("complaint", true);
							}
							// If customer is making an order, ask if international or domestic order
							else if (message.ToLower().Contains("order"))
							{
								botReply =
									"Our restaurant is based in Ulaanbaatar, Mongolia. Are you making a domestic order or an international order?";
								userData.SetProperty<bool>("gotActivity", true);
								userData.SetProperty<bool>("askedForDelivery", true);
							}
						}
					}
				}
				// If bot has NOT asked for username
				else
				{
					botReply = "Hello, what is your name?";
					userData.SetProperty("askedForUserName", true);
				}



				// BOT COMMANDS!!!
				if (message.StartsWith("!"))
				{
					if (message.ToLower().Contains("reset"))
					{
						botReply = "User data cleared and bot reset!";
						await sc.BotState.DeleteStateForUserAsync(activity.ChannelId, activity.From.Id);
					}
					else if (message.ToLower().Contains("help"))
					{
						botReply = "Well...I should be giving you information about how to use this bot, but the creator has been lazy and hasn't implemented this yet :(";
					}
					else if (message.ToLower().Contains("menu"))
					{
						List<Menu> menu = await AzureManager.AzureManagerInstance.GetMenu();
						foreach (Menu m in menu)
						{
							botReply += $"{m.Dish}: ${m.Price} NZD\n\r";
						}
					}
				}




				await sc.BotState.SetUserDataAsync(activity.ChannelId, activity.From.Id, userData);

				// return our reply to the user
				Activity reply = activity.CreateReply(botReply);
				await connector.Conversations.ReplyToActivityAsync(reply);
			}
			else
			{
				HandleSystemMessage(activity);
			}
			var response = Request.CreateResponse(HttpStatusCode.OK);
			return response;
		}

		private Activity HandleSystemMessage(Activity message)
		{
			if (message.Type == ActivityTypes.DeleteUserData)
			{
				// Implement user deletion here
				// If we handle user deletion, return a real message
			}
			else if (message.Type == ActivityTypes.ConversationUpdate)
			{
				// Handle conversation state changes, like members being added and removed
				// Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
				// Not available in all channels
			}
			else if (message.Type == ActivityTypes.ContactRelationUpdate)
			{
				// Handle add/remove from contact lists
				// Activity.From + Activity.Action represent what happened
			}
			else if (message.Type == ActivityTypes.Typing)
			{
				// Handle knowing tha the user is typing
			}
			else if (message.Type == ActivityTypes.Ping)
			{
			}

			return null;
		}
	}
}