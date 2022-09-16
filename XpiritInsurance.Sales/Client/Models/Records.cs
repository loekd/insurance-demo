namespace XpiritInsurance.Sales.Client.Models
{
    public record Insurance(InsuranceType InsuranceType, decimal AmountPerMonth);
    public enum InsuranceType { House, Boat, Car, Glass, Health }
    public record Quote(string UserName, InsuranceType InsuranceType, decimal AmountPerMonth);
}
