﻿using System.ComponentModel.DataAnnotations;

namespace XpiritInsurance.Claims.Client.Models
{
    public record Insurance(InsuranceType InsuranceType, decimal AmountPerMonth);
    public enum InsuranceType { House, Boat, Health }
    public record DamageClaim(string? UserName, InsuranceType InsuranceType, decimal Amount);
}