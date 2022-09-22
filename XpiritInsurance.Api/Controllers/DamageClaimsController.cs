namespace XpiritInsurance.Api.Controllers
{
    using System.Net;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Identity.Web;
    using Microsoft.Identity.Web.Resource;
    using XpiritInsurance.Api.Models;
    using XpiritInsurance.Api.Services;

    [Authorize]
    [RequiredScope(RequiredScopesConfigurationKey = "AzureAdB2C:Scopes")]
    [ApiController]
    [Route("[controller]")]
    public class DamageClaimsController : ControllerBase
    {
        private readonly ILogger<InsurancesController> _logger;
        private readonly DamageClaimService _damageClaimService;

        public DamageClaimsController(ILogger<InsurancesController> logger, DamageClaimService damageClaimService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _damageClaimService = damageClaimService ?? throw new ArgumentNullException(nameof(damageClaimService));
        }

        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DamageClaim>))]
        [HttpGet]
        public async Task<IActionResult> GetExistingDamageClaims()
        {
            string userName = HttpContext.User.GetDisplayName() ?? "unknown";
            var existingDamageClaims = await _damageClaimService.GetExistingDamageClaims(userName);
            return Ok(existingDamageClaims);
        }

        [ProducesDefaultResponseType]
        [HttpPost]
        public async Task<IActionResult> ClaimDamage([FromBody]DamageClaim damageClaim)
        {
            string userName = HttpContext.User.GetDisplayName() ?? "unknown";
            decimal amount = damageClaim.Amount;

            await _damageClaimService.ClaimNewDamage(damageClaim
                with
                {
                    UserName = userName
                });

            _logger.LogInformation("Sold insurance {InsuranceType} to user {UserName} for {AmountPerMonth}", damageClaim.InsuranceType, userName, amount);
            return Accepted();
        }
    }
}
