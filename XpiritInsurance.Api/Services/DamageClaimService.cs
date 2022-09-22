
using Dapr.Client;
using XpiritInsurance.Api.Models;

namespace XpiritInsurance.Api.Services
{
    public class DamageClaimService
    {
        private const string _stateStoreName = "damageclaim_state";
        private readonly DaprClient _daprClient;
        private readonly Dictionary<string, string> _queryMetadata = new() { { "queryIndexName", "damageClaimIndex" } };
        private readonly Dictionary<string, string> _storeMetadata = new() { { "contentType", "application/json" } };

        public DamageClaimService(DaprClient daprClient)
        {
            _daprClient = daprClient ?? throw new ArgumentNullException(nameof(daprClient));
        }

        public async Task<IReadOnlyCollection<DamageClaim>> GetExistingDamageClaims(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new InvalidOperationException("Username cannot be null");

            string query = $"{{\"filter\": {{ \"EQ\": {{ \"userName\": \"{userName}\" }}}}, \"sort\": [{{\"key\": \"insuranceType\",\"order\": \"ASC\"}}]}}";
            var response = await _daprClient.QueryStateAsync<DamageClaim>(_stateStoreName, query, metadata: _queryMetadata);
            if (response.Results.Count == 0)
                return Array.Empty<DamageClaim>();

            return response.Results.Select(s => s.Data).ToList();
        }

        public virtual async Task<DamageClaim> ClaimNewDamage(DamageClaim damageClaim)
        {
            if (string.IsNullOrWhiteSpace(damageClaim.UserName))

                throw new InvalidOperationException("Username cannot be null");

            var existing = await GetExistingDamageClaims(damageClaim.UserName);
            if (existing.Any(dc => dc.InsuranceType == damageClaim.InsuranceType))
                throw new InvalidOperationException("Existing claim cannot be overwritten");

            await _daprClient.SaveStateAsync(_stateStoreName, damageClaim.Id.ToGuidString(), damageClaim, metadata: _storeMetadata);
            return damageClaim;
        }
    }

    public static class GuidExtensions
    {
        public static string ToGuidString(this Guid guid)
        {
            return guid.ToString("N");
        }
    }
}
