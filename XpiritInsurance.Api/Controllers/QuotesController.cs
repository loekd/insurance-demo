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
    public class QuotesController : ControllerBase
    {
        private readonly ILogger<InsurancesController> _logger;
        private readonly QuoteService _quoteAmountService;

        public QuotesController(ILogger<InsurancesController> logger, QuoteService quoteAmountService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _quoteAmountService = quoteAmountService ?? throw new ArgumentNullException(nameof(quoteAmountService));
        }

        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<Quote>))]
        [HttpGet]
        public async Task<IActionResult> GetQuotes()
        {
            string userName = HttpContext.User.GetDisplayName() ?? "unknown";
            var insurances = await _quoteAmountService.GetExistingQuotes(userName);
            return Ok(insurances);
        }

        [ProducesDefaultResponseType]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(Quote))]
        [HttpPost]
        public async Task<IActionResult> RequestQuote([FromBody] Quote quote)
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
                        Message = $"Identity not verified for user {userName}. Connect DigiD identity to enable quotes for Health insurance."
                    });
                }
            }

            quote = await _quoteAmountService.RequestQuote(userName, quote.InsuranceType);

            return Ok(quote);
        }
    }
}
