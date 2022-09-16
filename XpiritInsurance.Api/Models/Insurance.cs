namespace XpiritInsurance.Api.Models
{
    public record Insurance(InsuranceType InsuranceType, decimal AmountPerMonth);

    public enum InsuranceType { House, Boat, Car, Glass, Health }
}
