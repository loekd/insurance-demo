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
    public class InsurancesController : ControllerBase
    {
        private readonly ILogger<InsurancesController> _logger;
        private readonly QuoteService _quoteAmountService;
        private readonly InsuranceService _insuranceService;

        public InsurancesController(ILogger<InsurancesController> logger, QuoteService quoteAmountService, InsuranceService insuranceService)
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
            var insurances = await _insuranceService.GetExistingInsurances(userName);
            return Ok(insurances);
        }

        [ProducesDefaultResponseType]
        [HttpPost]
        public async Task<IActionResult> BuyInsurance([FromBody] Quote quote)
        {
            string userName = HttpContext.User.GetDisplayName() ?? "unknown";

            //assert that user is identified for health insurance, by checking a custom claim named 'idVerified'
            //this is set to 'true' when a 'DigiD' identity is connected to the current user account.
            if (quote.InsuranceType == InsuranceType.Health)
            {
                bool idVerified = HttpContext.User.Claims.SingleOrDefault(c => c.Type == "idVerified")?.Value == "true";
                if (!idVerified)
                {
                    return Unauthorized(new ErrorViewModel
                    {
                        RequestId = HttpContext.TraceIdentifier,
                        Message = $"Identity not verified for user {userName}. Connect DigiD identity to enable purchase of Health insurance."
                    });
                }
            }

            var existingQuote = await _quoteAmountService.GetExistingQuote(quote.Id);

            if (existingQuote != null)
            {
                await _insuranceService.AddInsurance(userName, existingQuote);
                await _quoteAmountService.DeleteQuote(userName, existingQuote);

                _logger.LogInformation("Sold insurance {InsuranceType} to user {UserName} for {AmountPerMonth}", quote.InsuranceType, userName, existingQuote.AmountPerMonth);
                return Ok();
            }
            else
            {
                _logger.LogWarning("Unable to find existing quote for {InsuranceType}, user {UserName} with id {Id}", quote.InsuranceType, userName, quote.Id);

                return BadRequest(new ErrorViewModel
                {
                    RequestId = HttpContext.TraceIdentifier,
                    Message = $"Quote {quote.Id} not found"
                });
            }
        }
    }
}
