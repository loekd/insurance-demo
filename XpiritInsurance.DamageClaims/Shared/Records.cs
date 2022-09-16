using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XpiritInsurance.DamageClaims.Shared
{
    public record Insurance(InsuranceType InsuranceType, decimal AmountPerMonth);
    public enum InsuranceType { House, Boat, Health }
    public record DamageClaim(string UserName, [property:Required]InsuranceType InsuranceType, [property:Range(1, 100)]decimal Amount);
}
