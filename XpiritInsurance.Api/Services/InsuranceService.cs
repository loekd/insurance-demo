using Dapr.Client;
using XpiritInsurance.Api.Models;

namespace XpiritInsurance.Api.Services
{
    public class InsuranceService
    {
        private const string _stateStoreName = "insurance_state";
        private readonly DaprClient _daprClient;
        private readonly Dictionary<string, string> _queryMetadata = new() { { "queryIndexName", "insuranceIndex" } };
        private readonly Dictionary<string, string> _storeMetadata = new() { { "contentType", "application/json" } };

        public InsuranceService(DaprClient daprClient)
        {
            _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
        }

        public async Task<IReadOnlyCollection<Insurance>> GetExistingInsurances(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new InvalidOperationException("Username cannot be null");

            string query = $"{{\"filter\": {{ \"EQ\": {{ \"userName\": \"{userName}\" }}}}, \"sort\": [{{\"key\": \"insuranceType\",\"order\": \"ASC\"}}]}}";
            var response = await _daprClient.QueryStateAsync<Insurance>(_stateStoreName, query, metadata: _queryMetadata);
            if (response.Results.Count == 0)
                return Array.Empty<Insurance>();

            return response.Results.Select(s => s.Data).ToList();
        }

        public virtual async Task<Quote> AddInsurance(string userName, Quote quote)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new InvalidOperationException("Username cannot be null");

            var existing = await GetExistingInsurances(quote.UserName);
            if (existing.Any(dc => dc.InsuranceType == quote.InsuranceType))
                throw new InvalidOperationException("Existing insurance cannot be overwritten");

            await _daprClient.SaveStateAsync(_stateStoreName, quote.Id.ToGuidString(), new Insurance(quote.InsuranceType, quote.AmountPerMonth, Guid.NewGuid(), userName), metadata: _storeMetadata);
            return quote;
        }
    }
}
