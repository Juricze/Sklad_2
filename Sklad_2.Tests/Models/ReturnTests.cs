using Sklad_2.Models;
using Xunit;

namespace Sklad_2.Tests.Models
{
    /// <summary>
    /// Unit testy pro Return model - kritické výpočty vratek
    /// </summary>
    public class ReturnTests
    {
        #region Zaokrouhlování vratek (FinalRefundRounded)

        [Fact]
        public void FinalRefundRounded_ExactAmount_NoRounding()
        {
            // Arrange
            var returnItem = new Return
            {
                TotalRefundAmount = 100.00m
            };

            // Act & Assert
            Assert.Equal(100m, returnItem.FinalRefundRounded);
            Assert.Equal(0m, returnItem.RefundRoundingAmount);
            Assert.False(returnItem.HasRefundRounding);
        }

        [Theory]
        [InlineData(100.49, 100)] // Pod 0.50 - zaokrouhlí dolů
        [InlineData(100.50, 101)] // Přesně 0.50 - zaokrouhlí nahoru (AwayFromZero)
        [InlineData(100.51, 101)] // Nad 0.50 - zaokrouhlí nahoru
        [InlineData(75.25, 75)]   // 0.25 - dolů
        [InlineData(75.75, 76)]   // 0.75 - nahoru
        public void FinalRefundRounded_VariousAmounts_CorrectRounding(decimal amount, decimal expected)
        {
            // Arrange
            var returnItem = new Return
            {
                TotalRefundAmount = amount
            };

            // Act & Assert
            Assert.Equal(expected, returnItem.FinalRefundRounded);
        }

        [Fact]
        public void RefundRoundingAmount_RoundedUp_PositiveDifference()
        {
            // Arrange - 50.50 → 51 (zaokrouhleno nahoru o 0.50)
            var returnItem = new Return
            {
                TotalRefundAmount = 50.50m
            };

            // Act & Assert
            Assert.Equal(51m, returnItem.FinalRefundRounded);
            Assert.Equal(0.50m, returnItem.RefundRoundingAmount); // Kladný rozdíl
            Assert.True(returnItem.HasRefundRounding);
        }

        [Fact]
        public void RefundRoundingAmount_RoundedDown_NegativeDifference()
        {
            // Arrange - 50.49 → 50 (zaokrouhleno dolů o -0.49)
            var returnItem = new Return
            {
                TotalRefundAmount = 50.49m
            };

            // Act & Assert
            Assert.Equal(50m, returnItem.FinalRefundRounded);
            Assert.Equal(-0.49m, returnItem.RefundRoundingAmount); // Záporný rozdíl
            Assert.True(returnItem.HasRefundRounding);
        }

        #endregion

        #region AmountToRefund s věrnostní slevou

        [Fact]
        public void AmountToRefund_NoLoyaltyDiscount_EqualsTotalRefund()
        {
            // Arrange
            var returnItem = new Return
            {
                TotalRefundAmount = 300m,
                LoyaltyDiscountAmount = 0m
            };

            // Act & Assert
            Assert.Equal(300m, returnItem.AmountToRefund);
        }

        [Fact]
        public void AmountToRefund_WithLoyaltyDiscount_SubtractsProportionalDiscount()
        {
            // Arrange - Vratka 300 Kč, poměrná část slevy 30 Kč
            var returnItem = new Return
            {
                TotalRefundAmount = 300m,
                LoyaltyDiscountAmount = 30m // 10% z původní ceny
            };

            // Act & Assert
            Assert.Equal(270m, returnItem.AmountToRefund); // 300 - 30
        }

        #endregion

        #region Zaokrouhlování s věrnostní slevou (KRITICKÉ!)

        [Fact]
        public void FinalRefundRounded_WithLoyaltyDiscount_RoundsAfterDiscount()
        {
            // Arrange - 300 Kč vratka, 30 Kč sleva, výsledek 270.00 → 270
            var returnItem = new Return
            {
                TotalRefundAmount = 300m,
                LoyaltyDiscountAmount = 30m
            };

            // Act & Assert
            Assert.Equal(270m, returnItem.AmountToRefund); // Přesná částka
            Assert.Equal(270m, returnItem.FinalRefundRounded); // Zaokrouhlená
            Assert.Equal(0m, returnItem.RefundRoundingAmount); // Žádný rozdíl
        }

        [Fact]
        public void FinalRefundRounded_ComplexScenario_CorrectCalculation()
        {
            // Arrange - REÁLNÝ SCÉNÁŘ
            // TotalRefundAmount: 456.78 Kč
            // Poměrná část věrnostní slevy: 45.68 Kč
            // AmountToRefund: 411.10 Kč → Zaokrouhleno: 411 Kč
            var returnItem = new Return
            {
                TotalRefundAmount = 456.78m,
                LoyaltyDiscountAmount = 45.68m
            };

            // Act
            var amountToRefund = returnItem.AmountToRefund;
            var finalRounded = returnItem.FinalRefundRounded;
            var rounding = returnItem.RefundRoundingAmount;

            // Assert
            Assert.Equal(411.10m, amountToRefund); // 456.78 - 45.68
            Assert.Equal(411m, finalRounded); // Zaokrouhleno dolů
            Assert.Equal(-0.10m, rounding); // -0.10 Kč
            Assert.True(returnItem.HasRefundRounding);
        }

        [Fact]
        public void FinalRefundRounded_RoundUpScenario_CorrectCalculation()
        {
            // Arrange - Zaokrouhlení nahoru
            // TotalRefundAmount: 200.00 Kč
            // Věrnostní sleva: 19.50 Kč
            // AmountToRefund: 180.50 Kč → 181 Kč
            var returnItem = new Return
            {
                TotalRefundAmount = 200.00m,
                LoyaltyDiscountAmount = 19.50m
            };

            // Act & Assert
            Assert.Equal(180.50m, returnItem.AmountToRefund);
            Assert.Equal(181m, returnItem.FinalRefundRounded); // Zaokrouhleno nahoru
            Assert.Equal(0.50m, returnItem.RefundRoundingAmount); // +0.50 Kč
        }

        #endregion

        #region Edge cases

        [Fact]
        public void FinalRefundRounded_ZeroAmount_ReturnsZero()
        {
            // Arrange
            var returnItem = new Return
            {
                TotalRefundAmount = 0m
            };

            // Act & Assert
            Assert.Equal(0m, returnItem.FinalRefundRounded);
            Assert.Equal(0m, returnItem.RefundRoundingAmount);
            Assert.False(returnItem.HasRefundRounding);
        }

        [Fact]
        public void FinalRefundRounded_VerySmallAmount_RoundsToOne()
        {
            // Arrange - 0.50 Kč → 1 Kč
            var returnItem = new Return
            {
                TotalRefundAmount = 0.50m
            };

            // Act & Assert
            Assert.Equal(1m, returnItem.FinalRefundRounded);
            Assert.Equal(0.50m, returnItem.RefundRoundingAmount);
        }

        [Fact]
        public void FinalRefundRounded_LargeAmount_RoundsCorrectly()
        {
            // Arrange
            var returnItem = new Return
            {
                TotalRefundAmount = 50000.75m
            };

            // Act & Assert
            Assert.Equal(50001m, returnItem.FinalRefundRounded);
            Assert.Equal(0.25m, returnItem.RefundRoundingAmount);
        }

        #endregion
    }
}
