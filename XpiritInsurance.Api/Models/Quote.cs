namespace XpiritInsurance.Api.Models
{
    public record Quote(string UserName, InsuranceType InsuranceType, decimal AmountPerMonth);
}
