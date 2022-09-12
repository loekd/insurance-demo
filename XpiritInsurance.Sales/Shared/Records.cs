using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XpiritInsurance.Sales.Shared
{
    public record Insurance(InsuranceType InsuranceType, decimal AmountPerMonth);
    public enum InsuranceType { House, Boat, Health }

}
