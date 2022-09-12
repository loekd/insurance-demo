
using Microsoft.Collections.Extensions;
using XpiritInsurance.Api.Models;

namespace XpiritInsurance.Api.Services
{
    public class DamageClaimService
    {
        private readonly MultiValueDictionary<string, DamageClaim> Data = new();

        public Task<IReadOnlyCollection<DamageClaim>> GetExistingDamageClaims(string userName)
        {
            IReadOnlyCollection<DamageClaim> result = Array.Empty<DamageClaim>();
            if (Data.TryGetValue(userName, out var insurances))
            {
                result = insurances;
            }
            return Task.FromResult(result);
        }

        public virtual Task<DamageClaim> ClaimNewDamage(DamageClaim claim)
        {
            if (claim.UserName == null)
                throw new InvalidOperationException("Username cannot be null");

            if (Data.TryGetValue(claim.UserName, out var claims) && claims.Any(i => i.InsuranceType == claim.InsuranceType))
            {
                var existing = claims.Single(i => i.InsuranceType == claim.InsuranceType);
                Data.Remove(claim.UserName, existing);
            }

            Data.Add(claim.UserName, claim);
            return Task.FromResult(claim);
        }
    }
}
