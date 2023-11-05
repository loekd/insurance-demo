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

        //health insurances require a verified user
        //there's a special endpoint for this below
        if (quote.InsuranceType == InsuranceType.Health)
        {
            return BadRequest(new ErrorViewModel
            {
                RequestId = HttpContext.TraceIdentifier,
                Message = $"Health insurance requires verified identity. Call 'official' endpoint."
            });
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

    //assert that user is identified for health insurance, by checking a scope claim value 'IdentityVerified'
    //this is set when a 'DigiD' identity is linked to the current user account.
    [Authorize(Policy = Program.ElevatedPrivilegesPolicyName)]
    [ProducesDefaultResponseType]
    [HttpPost("official")]
    public async Task<IActionResult> BuyOfficialInsurance([FromBody] Quote quote)
    {
        string userName = HttpContext.User.GetDisplayName() ?? "unknown";

        var existingQuote = await _quoteAmountService.GetExistingQuote(quote.Id);

        if (existingQuote != null)
        {
            await _insuranceService.AddInsurance(userName, existingQuote);
            await _quoteAmountService.DeleteQuote(userName, existingQuote);

            _logger.LogInformation("Sold official insurance {InsuranceType} to user {UserName} for {AmountPerMonth}", quote.InsuranceType, userName, existingQuote.AmountPerMonth);
            return Ok();
        }
        else
        {
            _logger.LogWarning("Unable to find existing official quote for {InsuranceType}, user {UserName} with id {Id}", quote.InsuranceType, userName, quote.Id);

            return BadRequest(new ErrorViewModel
            {
                RequestId = HttpContext.TraceIdentifier,
                Message = $"Quote {quote.Id} not found"
            });
        }
    }
}
