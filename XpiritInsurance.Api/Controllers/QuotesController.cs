using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;
using XpiritInsurance.Api.Models;
using XpiritInsurance.Api.Services;

namespace XpiritInsurance.Api.Controllers;

[Authorize]
[Authorize(Policy = Program.DefaultPrivilegesPolicyName)]
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
        quote = await _quoteAmountService.RequestQuote(userName, quote.InsuranceType);

        _logger.LogInformation("Quote for insurance {InsuranceType} to user {UserName} for {AmountPerMonth}", quote.InsuranceType, userName, quote.AmountPerMonth);
        return Ok(quote);
    }
}
