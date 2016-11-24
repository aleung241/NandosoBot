using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MobileServices;
using NandosoBot.DataModels;
using Newtonsoft.Json.Linq;

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

				// ##############################################################################################################################################################################################################################
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
						botReply = "!reset to clear user data and reset the bot\n\r!menu to show the restaurant menu";
					}
					else if (message.ToLower().Contains("menu"))
					{
						Activity cardReply = activity.CreateReply();
						cardReply.Recipient = activity.From;
						cardReply.Type = "message";
						cardReply.Attachments = new List<Attachment>();
						cardReply.AttachmentLayout = "carousel";
						List<Menu> menu = await AzureManager.AzureManagerInstance.GetMenu();
						foreach (Menu m in menu)
						{
							HeroCard menuCard = new HeroCard()
							{
								Title = m.Dish,
								Text = $"{m.Price} NZD  \n{m.Description}",
								Images = new List<CardImage>
								{
									new CardImage(url: m.Image)
								}
							};
							Attachment attachment = menuCard.ToAttachment();
							cardReply.Attachments.Add(attachment);
						}
						await connector.Conversations.SendToConversationAsync(cardReply);
						return Request.CreateResponse(HttpStatusCode.OK);
					}
					else if (message.ToLower().Contains("cart"))
					{
						//string individualCart = "";
						List<Cart> cart = await CartManager.CartManagerInstance.GetCart();
						if (cart.Count < 1)
						{
							botReply = "Your cart is currently empty";
						}
						else
						{
							Activity cartReply = activity.CreateReply();
							cartReply.Recipient = activity.From;
							cartReply.Type = "message";
							cartReply.Attachments = new List<Attachment>();
							double totalPrice = 0;
							List<ReceiptItem> items = new List<ReceiptItem>();
							foreach (Cart c in cart)
							{
								totalPrice += c.Price;
								items.Add(new ReceiptItem
								{
									Title = c.Dish,
									Price = $"{c.Price}",
									Image = new CardImage(c.Image)
								});
							}
							ReceiptCard receiptCard = new ReceiptCard();
							if (userData.GetProperty<bool>("hasCountry"))
							{
								double convertedPrice = totalPrice / userData.GetProperty<double>("mnt") * userData.GetProperty<double>("rate");
								receiptCard.Title = "Your Order";
								receiptCard.Items = items;
								receiptCard.Total = $"${totalPrice} MNT = ${convertedPrice} {userData.GetProperty<string>("currency")}";
								receiptCard.Buttons = new List<CardAction>
								{
									new CardAction()
									{
										Title = "No this is wrong! Restart!",
										Type = "postBack",
										Value = "!reshop"
									}
								};
							}
							else
							{
								receiptCard.Title = "Your Order";
								receiptCard.Items = items;
								receiptCard.Total = $"${totalPrice}";
							}
							Attachment attach = receiptCard.ToAttachment();
							cartReply.Attachments.Add(attach);
							await connector.Conversations.SendToConversationAsync(cartReply);
							return Request.CreateResponse(HttpStatusCode.OK);
						}
					}
					else if (message.ToLower().Contains("reshop"))
					{
						List<Cart> cart = await CartManager.CartManagerInstance.GetCart();
						foreach (Cart c in cart)
						{
							await CartManager.CartManagerInstance.DeleteCart(c);
						}
					}
				}
				// ##############################################################################################################################################################################################################################
				else
				{
					// If bot has asked for username
					if (userData.GetProperty<bool>("askedForUserName"))
					{
						// If username had only just been given
						if (userName == "")
						{
							userData.SetProperty<string>("userName", message);
							await sc.BotState.SetUserDataAsync(activity.ChannelId, activity.From.Id, userData);
							botReply = $"Hi {userData.GetProperty<string>("userName")}, what would you like to do today? (Hint: order something or make a complaint! Just order something actually...";
						}
						// If username had been given beforehand already
						else
						{
							if (userData.GetProperty<bool>("gotActivity"))
							{
								if (userData.GetProperty<bool>("askedForDelivery"))
								{
									if (userData.GetProperty<bool>("askedForCountry"))
									{
										if (userData.GetProperty<bool>("validCountry"))
										{
											int count = 0;
											string dish = "";
											List<Menu> menu = await AzureManager.AzureManagerInstance.GetMenu();
											Cart cart = new Cart();
											foreach (Menu m in menu)
											{
												if (activity.Text.ToLower().Trim() == m.Dish.ToLower())
												{
													count++;
													dish = m.Dish;
													cart.Dish = m.Dish;
													cart.Price = m.Price;
													cart.Image = m.Image;
												}
											}
											if (count < 1)
											{
												botReply =
													$"Sorry, {activity.Text} is not available in our menu.\n\rPlease see !menu for what's available to order";
											}
											else
											{
												await CartManager.CartManagerInstance.AddToCart(cart);
												count = 0;
												botReply = $"{dish} has been added to cart\n\r!cart to see your cart and place your order";
											}
										}
										else
										{
											List<Country.RootObject> rootObject;

											HttpClient client = new HttpClient();
											string x = await client.GetStringAsync(new Uri("https://restcountries.eu/rest/v1/all"));

											rootObject = JsonConvert.DeserializeObject<List<Country.RootObject>>(x);

											if (rootObject.Any(country => country.name.Equals(message, StringComparison.InvariantCultureIgnoreCase)))
											{
												userData.SetProperty("country", message);
												userData.SetProperty("hasCountry", true);

												var currency = rootObject.FirstOrDefault(item => item.name.ToLower() == message.ToLower()).currencies[0];
												userData.SetProperty("currency", currency);

												Currency.RootObject rates;
												string z =
													await
														client.GetStringAsync(
															new Uri("https://openexchangerates.org/api/latest.json?app_id=e37bb753919b470883daf568b8fab555"));
												rates = JsonConvert.DeserializeObject<Currency.RootObject>(z);

												PropertyInfo prop = typeof(Currency.Rates).GetProperty(currency);
												object rate = prop.GetValue(rates.rates, null);

												userData.SetProperty<double>("rate", Convert.ToDouble(rate));
												userData.SetProperty("mnt", rates.rates.MNT);
												userData.SetProperty("validCountry", true);

												botReply = "What would you like to order?\n\rYou can type !menu to get our menu\n\rYou can type !cart at any time to see what's in your shopping cart currently";
												userData.SetProperty("askedForOrder", true);

												await sc.BotState.SetUserDataAsync(activity.ChannelId, activity.From.Id, userData);
											}
											else
											{
												botReply = $"Sorry, but we do not deliver to {message}";
											}
										}

									}
									else
									{
										if (message.ToLower().Trim() == "domestic")
										{
											botReply = "We do free deliveries domestically! What would you like to order?";
											userData.SetProperty("askedForOrder", true);
											await sc.BotState.SetUserDataAsync(activity.ChannelId, activity.From.Id, userData);
										}
										else if (message.ToLower().Trim() == "international")
										{
											botReply = "What country do you want to deliver to?";
											userData.SetProperty("askedForCountry", true);
											await sc.BotState.SetUserDataAsync(activity.ChannelId, activity.From.Id, userData);
										}
									}
								}
								else if (userData.GetProperty<bool>("complaint"))
								{
									botReply = "Sorry, not implemented yet. Goodbye!";
									await sc.BotState.DeleteStateForUserAsync(activity.ChannelId, activity.From.Id);
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
									await sc.BotState.SetUserDataAsync(activity.ChannelId, activity.From.Id, userData);
								}
								// If customer is making an order, ask if international or domestic order
								else if (message.ToLower().Contains("order"))
								{
									botReply =
										"Our restaurant is based in Ulaanbaatar, Mongolia. Are you making a domestic order or an international order?";
									userData.SetProperty<bool>("gotActivity", true);
									userData.SetProperty<bool>("askedForDelivery", true);
									await sc.BotState.SetUserDataAsync(activity.ChannelId, activity.From.Id, userData);
								}
							}
						}
					}
					// If bot has NOT asked for username
					else
					{
						botReply = "Hello, what is your name?";
						userData.SetProperty("askedForUserName", true);
						await sc.BotState.SetUserDataAsync(activity.ChannelId, activity.From.Id, userData);
					}
				}

				// return our reply to the user
				Activity reply = activity.CreateReply(botReply);
				await connector.Conversations.ReplyToActivityAsync(reply);
			}
			// ##############################################################################################################################################################################################################################
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