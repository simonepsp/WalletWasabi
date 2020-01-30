using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using WalletWasabi.Backend.Models;
using WalletWasabi.Interfaces;

namespace WalletWasabi.WebClients.Gemini
{
	public class GeminiExchangeRateProvider : IExchangeRateProvider
	{
		private class GeminiExchangeRateInfo
		{
			public decimal Bid { get; set; }
		}

		public async Task<List<ExchangeRate>> GetExchangeRateAsync()
		{
			using var httpClient = new HttpClient();
			httpClient.BaseAddress = new Uri("https://api.gemini.com");
			using var response = await httpClient.GetAsync("/v1/pubticker/btcusd");
			using var content = response.Content;
			var data = await content.ReadAsJsonAsync<GeminiExchangeRateInfo>();

			var exchangeRates = new List<ExchangeRate>
				{
					new ExchangeRate { Rate = data.Bid, Ticker = "USD" }
				};

			return exchangeRates;
		}
	}
}
