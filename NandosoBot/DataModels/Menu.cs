using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace NandosoBot.DataModels
{
	public class Menu
	{
		[JsonProperty(PropertyName = "Id")]
		public string ID { get; set; }

		[JsonProperty(PropertyName = "dish")]
		public string Dish { get; set; }

		[JsonProperty(PropertyName = "price")]
		public double Price { get; set; }
	}
}