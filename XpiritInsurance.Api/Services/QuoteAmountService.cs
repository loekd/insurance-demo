namespace XpiritInsurance.Api.Services
{
    using global::XpiritInsurance.Api.Models;

    public class QuoteAmountService
    {
        public virtual Task<decimal> CalculateQuote(string userName, InsuranceType insuranceType)
        {
            decimal amount = 0M;
            switch (insuranceType)
            {
                case InsuranceType.House:
                    amount = new Random().Next(30, 70);
                    break;
                case InsuranceType.Boat:
                    amount = new Random(Guid.NewGuid().GetHashCode()).Next(5, 15);
                    break;
                case InsuranceType.Health:
                    amount = new Random(Guid.NewGuid().GetHashCode()).Next(79, 150);
                    break;
                case InsuranceType.Glass:
                    amount = new Random(Guid.NewGuid().GetHashCode()).Next(10, 31);
                    break;

            }

            return Task.FromResult<decimal>(amount);
        }
    }
}
