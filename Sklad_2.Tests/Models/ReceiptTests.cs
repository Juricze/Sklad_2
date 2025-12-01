using Sklad_2.Models;
using Xunit;

namespace Sklad_2.Tests.Models
{
    /// <summary>
    /// Unit testy pro Receipt model - kritické finanční výpočty
    /// </summary>
    public class ReceiptTests
    {
        #region Zaokrouhlování na celé koruny (FinalAmountRounded)

        [Fact]
        public void FinalAmountRounded_ExactAmount_NoRounding()
        {
            // Arrange
            var receipt = new Receipt
            {
                TotalAmount = 100.00m
            };

            // Act & Assert
            Assert.Equal(100m, receipt.FinalAmountRounded);
            Assert.Equal(0m, receipt.RoundingAmount);
            Assert.False(receipt.HasRounding);
        }

        [Theory]
        [InlineData(100.49, 100)] // Pod 0.50 - zaokrouhlí dolů
        [InlineData(100.50, 101)] // Přesně 0.50 - zaokrouhlí nahoru (AwayFromZero)
        [InlineData(100.51, 101)] // Nad 0.50 - zaokrouhlí nahoru
        [InlineData(150.25, 150)] // 0.25 - dolů
        [InlineData(150.75, 151)] // 0.75 - nahoru
        public void FinalAmountRounded_VariousAmounts_CorrectRounding(decimal amount, decimal expected)
        {
            // Arrange
            var receipt = new Receipt
            {
                TotalAmount = amount
            };

            // Act & Assert
            Assert.Equal(expected, receipt.FinalAmountRounded);
        }

        [Fact]
        public void RoundingAmount_RoundedUp_PositiveDifference()
        {
            // Arrange - 100.50 → 101 (zaokrouhleno nahoru o 0.50)
            var receipt = new Receipt
            {
                TotalAmount = 100.50m
            };

            // Act & Assert
            Assert.Equal(101m, receipt.FinalAmountRounded);
            Assert.Equal(0.50m, receipt.RoundingAmount); // Kladný rozdíl
            Assert.True(receipt.HasRounding);
        }

        [Fact]
        public void RoundingAmount_RoundedDown_NegativeDifference()
        {
            // Arrange - 100.49 → 100 (zaokrouhleno dolů o -0.49)
            var receipt = new Receipt
            {
                TotalAmount = 100.49m
            };

            // Act & Assert
            Assert.Equal(100m, receipt.FinalAmountRounded);
            Assert.Equal(-0.49m, receipt.RoundingAmount); // Záporný rozdíl
            Assert.True(receipt.HasRounding);
        }

        #endregion

        #region Slevy a AmountToPay

        [Fact]
        public void AmountToPay_NoDiscounts_EqualsTotalAmount()
        {
            // Arrange
            var receipt = new Receipt
            {
                TotalAmount = 500m,
                LoyaltyDiscountAmount = 0m,
                GiftCardRedemptionAmount = 0m
            };

            // Act & Assert
            Assert.Equal(500m, receipt.AmountToPay);
        }

        [Fact]
        public void AmountToPay_LoyaltyDiscount_SubtractsFromTotal()
        {
            // Arrange - Věrnostní sleva 10%
            var receipt = new Receipt
            {
                TotalAmount = 500m,
                HasLoyaltyDiscount = true,
                LoyaltyDiscountAmount = 50m // 10% z 500
            };

            // Act & Assert
            Assert.Equal(450m, receipt.AmountToPay);
            Assert.True(receipt.HasAnyDiscount);
        }

        [Fact]
        public void AmountToPay_GiftCardRedemption_SubtractsFromTotal()
        {
            // Arrange - Dárkový poukaz 100 Kč
            var receipt = new Receipt
            {
                TotalAmount = 500m,
                ContainsGiftCardRedemption = true,
                GiftCardRedemptionAmount = 100m
            };

            // Act & Assert
            Assert.Equal(400m, receipt.AmountToPay);
            Assert.True(receipt.HasAnyDiscount);
        }

        [Fact]
        public void AmountToPay_BothDiscounts_SubtractsBoth()
        {
            // Arrange - Věrnostní sleva 50 Kč + Dárkový poukaz 100 Kč
            var receipt = new Receipt
            {
                TotalAmount = 500m,
                HasLoyaltyDiscount = true,
                LoyaltyDiscountAmount = 50m,
                ContainsGiftCardRedemption = true,
                GiftCardRedemptionAmount = 100m
            };

            // Act & Assert
            Assert.Equal(350m, receipt.AmountToPay); // 500 - 50 - 100
            Assert.True(receipt.HasAnyDiscount);
        }

        #endregion

        #region Kombinace slev + zaokrouhlování (KRITICKÉ!)

        [Fact]
        public void FinalAmountRounded_WithLoyaltyDiscount_RoundsAfterDiscount()
        {
            // Arrange - 500 Kč, sleva 50 Kč, výsledek 450.00 → 450
            var receipt = new Receipt
            {
                TotalAmount = 500m,
                HasLoyaltyDiscount = true,
                LoyaltyDiscountAmount = 50m
            };

            // Act & Assert
            Assert.Equal(450m, receipt.AmountToPay); // Přesná částka
            Assert.Equal(450m, receipt.FinalAmountRounded); // Zaokrouhlená
            Assert.Equal(0m, receipt.RoundingAmount); // Žádný rozdíl
        }

        [Fact]
        public void FinalAmountRounded_WithGiftCard_RoundsAfterRedemption()
        {
            // Arrange - 500.99 Kč, poukaz 100 Kč, výsledek 400.99 → 401
            var receipt = new Receipt
            {
                TotalAmount = 500.99m,
                ContainsGiftCardRedemption = true,
                GiftCardRedemptionAmount = 100m
            };

            // Act & Assert
            Assert.Equal(400.99m, receipt.AmountToPay); // Přesná částka
            Assert.Equal(401m, receipt.FinalAmountRounded); // Zaokrouhlená nahoru
            Assert.Equal(0.01m, receipt.RoundingAmount); // +0.01 Kč
        }

        [Fact]
        public void FinalAmountRounded_ComplexScenario_CorrectCalculation()
        {
            // Arrange - REÁLNÝ SCÉNÁŘ z PRODUKCE
            // TotalAmount: 1234.56 Kč
            // Věrnostní sleva 10%: -123.46 Kč
            // Dárkový poukaz: -200 Kč
            // AmountToPay: 911.10 Kč → Zaokrouhleno: 911 Kč
            var receipt = new Receipt
            {
                TotalAmount = 1234.56m,
                HasLoyaltyDiscount = true,
                LoyaltyDiscountAmount = 123.46m,
                ContainsGiftCardRedemption = true,
                GiftCardRedemptionAmount = 200m
            };

            // Act
            var amountToPay = receipt.AmountToPay;
            var finalRounded = receipt.FinalAmountRounded;
            var rounding = receipt.RoundingAmount;

            // Assert
            Assert.Equal(911.10m, amountToPay); // 1234.56 - 123.46 - 200
            Assert.Equal(911m, finalRounded); // Zaokrouhleno dolů
            Assert.Equal(-0.10m, rounding); // -0.10 Kč
            Assert.True(receipt.HasRounding);
        }

        #endregion

        #region Edge cases

        [Fact]
        public void FinalAmountRounded_ZeroAmount_ReturnsZero()
        {
            // Arrange
            var receipt = new Receipt
            {
                TotalAmount = 0m
            };

            // Act & Assert
            Assert.Equal(0m, receipt.FinalAmountRounded);
            Assert.Equal(0m, receipt.RoundingAmount);
            Assert.False(receipt.HasRounding);
        }

        [Fact]
        public void FinalAmountRounded_VerySmallAmount_RoundsToOne()
        {
            // Arrange - 0.50 Kč → 1 Kč (AwayFromZero)
            var receipt = new Receipt
            {
                TotalAmount = 0.50m
            };

            // Act & Assert
            Assert.Equal(1m, receipt.FinalAmountRounded);
            Assert.Equal(0.50m, receipt.RoundingAmount);
        }

        [Fact]
        public void FinalAmountRounded_LargeAmount_RoundsCorrectly()
        {
            // Arrange - 999999.50 Kč
            var receipt = new Receipt
            {
                TotalAmount = 999999.50m
            };

            // Act & Assert
            Assert.Equal(1000000m, receipt.FinalAmountRounded);
            Assert.Equal(0.50m, receipt.RoundingAmount);
        }

        #endregion
    }
}
