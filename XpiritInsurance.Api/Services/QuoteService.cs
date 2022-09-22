using Dapr.Client;
using XpiritInsurance.Api.Models;

namespace XpiritInsurance.Api.Services
{
    public class QuoteService
    {
        private const string _stateStoreName = "quote_state";
        private readonly DaprClient _daprClient;
        private readonly Dictionary<string, string> _queryMetadata = new() { { "contentType", "application/json" }, { "queryIndexName", "quoteIndex" } };
        private readonly Dictionary<string, string> _storeMetadata = new() { { "contentType", "application/json" } };


        public QuoteService(DaprClient daprClient)
        {
            _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
        }

        private Task<decimal> CalculateQuote(InsuranceType insuranceType)
        {
            decimal amount = 0M;
            switch (insuranceType)
            {
                case InsuranceType.House:
                    amount = new Random().Next(30, 70);
                    break;
                case InsuranceType.Boat:
                    amount = new Random(Guid.NewGuid().GetHashCode()).Next(5, 15);
                    break;
                case InsuranceType.Health:
                    amount = new Random(Guid.NewGuid().GetHashCode()).Next(79, 150);
                    break;
                case InsuranceType.Glass:
                    amount = new Random(Guid.NewGuid().GetHashCode()).Next(10, 31);
                    break;
            }

            return Task.FromResult(amount);
        }

        public async Task<IReadOnlyCollection<Quote>> GetExistingQuotes(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new InvalidOperationException("Username cannot be null");

            string query = $"{{\"filter\": {{ \"EQ\": {{ \"userName\": \"{userName}\" }}}}, \"sort\": [{{\"key\": \"insuranceType\",\"order\": \"ASC\"}}]}}";
            
            var response = await _daprClient.QueryStateAsync<Quote>(_stateStoreName, query, metadata: _queryMetadata);
            if (response.Results.Count == 0)
                return Array.Empty<Quote>();

            return response.Results.Select(s => s.Data).ToList();
        }

        public async Task<Quote?> GetExistingQuote(Guid quoteId)
        {
            var result = await _daprClient.GetStateEntryAsync<Quote>(_stateStoreName, quoteId.ToGuidString(), metadata: _queryMetadata);
            return result.Value;
        }

        public virtual async Task<Quote> RequestQuote(string userName, InsuranceType insuranceType)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new InvalidOperationException("Username cannot be null");

            var existingQuotes = await GetExistingQuotes(userName);
            var existingQuote = existingQuotes.SingleOrDefault(dc => dc.InsuranceType == insuranceType);
            if (existingQuote != null)
                return existingQuote;

            decimal amountPerMonth = await CalculateQuote(insuranceType);
            var quote = new Quote(userName, insuranceType, amountPerMonth, Guid.NewGuid());
            await _daprClient.SaveStateAsync(_stateStoreName, quote.Id.ToGuidString(), quote, metadata: _storeMetadata);
            return quote;
        }

        public async Task DeleteQuote(string userName, Quote existingQuote)
        {
            var quote = await GetExistingQuote(existingQuote.Id);
            if (quote != null && quote.UserName == userName)
                await _daprClient.DeleteStateAsync(_stateStoreName, existingQuote.Id.ToGuidString(), metadata: _storeMetadata);
        }
    }
}
