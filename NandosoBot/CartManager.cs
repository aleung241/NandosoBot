using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI.WebControls;
using Microsoft.WindowsAzure.MobileServices;
using NandosoBot.DataModels;

namespace NandosoBot
{
	public class CartManager
	{
		private static CartManager instance;
		private MobileServiceClient client;
		private IMobileServiceTable<Cart> cartTable;

		private CartManager()
		{
			this.client = new MobileServiceClient("https://nandoso2016bot.azurewebsites.net");
			this.cartTable = this.client.GetTable<Cart>();
		}

		public MobileServiceClient CartClient
		{
			get { return client; }
		}

		public static CartManager CartManagerInstance
		{
			get
			{
				if (instance == null)
				{
					instance = new CartManager();
				}

				return instance;
			}
		}

		public async Task<List<Cart>> GetCart()
		{
			return await this.cartTable.ToListAsync();
		}

		public async Task AddToCart(Cart cart)
		{
			await this.cartTable.InsertAsync(cart);
		}
	}
}