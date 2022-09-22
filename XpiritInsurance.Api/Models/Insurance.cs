namespace XpiritInsurance.Api.Models
{
    public record Insurance(InsuranceType InsuranceType, decimal AmountPerMonth, Guid id, string userName);

    public enum InsuranceType { House, Boat, Car, Glass, Health }
}
