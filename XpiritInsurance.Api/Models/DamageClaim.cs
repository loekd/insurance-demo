using System.ComponentModel.DataAnnotations;

namespace XpiritInsurance.Api.Models
{
    public record DamageClaim(string? UserName, InsuranceType InsuranceType, decimal Amount);

}
