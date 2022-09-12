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
    public class InsuranceController : ControllerBase
    {
        private readonly ILogger<InsuranceController> _logger;
        private readonly QuoteAmountService _quoteAmountService;
        private readonly InsuranceService _insuranceService;

        public InsuranceController(ILogger<InsuranceController> logger, QuoteAmountService quoteAmountService, InsuranceService insuranceService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _quoteAmountService = quoteAmountService ?? throw new ArgumentNullException(nameof(quoteAmountService));
            _insuranceService = insuranceService ?? throw new ArgumentNullException(nameof(insuranceService));
        }

        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<Insurance>))]
        [HttpGet]
        public async Task<IActionResult> GetInsurances()
        {
            string userName = HttpContext.User.GetDisplayName() ?? "unknown";
            var insurances = await _insuranceService.GetInsurances(userName);
            return Ok(insurances);
        }

        [ProducesDefaultResponseType]
        [HttpPost]
        public async Task<IActionResult> BuyInsurance([FromBody] Quote quote)
        {
            string userName = HttpContext.User.GetDisplayName() ?? "unknown";
            decimal amount = quote.AmountPerMonth;
            if (amount < 5 || amount > 150)
            {
                amount = await _quoteAmountService.CalculateQuote(userName, quote.InsuranceType);
            }
            await _insuranceService.AddInsurance(quote with { UserName = userName, AmountPerMonth = amount });

            _logger.LogInformation("Sold insurance {InsuranceType} to user {UserName} for {AmountPerMonth}", quote.InsuranceType, userName, amount);
            return Ok();
        }

        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(Quote))]
        [HttpGet("quote")]
        public async Task<IActionResult> CalculateQuote([FromQuery]InsuranceType insuranceType)
        {
            string userName = HttpContext.User.GetDisplayName() ?? "unknown";
            decimal amount = await _quoteAmountService.CalculateQuote(userName, insuranceType);

            return Ok(new Quote(userName, insuranceType, amount));
        }
    }
}
