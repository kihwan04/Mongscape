using Muhanok.Domain;
using NUnit.Framework;

namespace Muhanok.Tests
{
    public sealed class FoodEquivalentTests
    {
        [TestCase(0)][TestCase(-5)][TestCase(double.NaN)][TestCase(double.PositiveInfinity)]
        public void NoFoodsForZeroOrInvalidKcal(double kcal) => Assert.AreEqual(0, FoodEquivalent.For(kcal).Count);

        [Test] public void ThreeMinuteRunShowsSmallFoodsWithTwoDecimals()
        {
            // 3 minutes at 4 MET, 65 kg = 13.65 kcal.
            var foods = FoodEquivalent.For(13.65);
            Assert.AreEqual(2, foods.Count, "tomato 4.55 and candy 0.68; 귤 0.34 is below the minimum");
            Assert.AreEqual("방울토마토", foods[0].Food.Name); Assert.AreEqual("4.55개", foods[0].CountText);
            Assert.AreEqual("사탕", foods[1].Food.Name); Assert.AreEqual("0.68개", foods[1].CountText);
        }

        [Test] public void OnlyFoodsInReadableRangeAndAtMostFive()
        {
            var foods = FoodEquivalent.For(100);
            Assert.AreEqual(5, foods.Count);
            string[] names = { "사탕", "귤", "삶은 달걀", "바나나", "콜라 250ml" };
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(names[i], foods[i].Food.Name);
                Assert.That(foods[i].Count, Is.InRange(FoodEquivalent.MinCount, FoodEquivalent.MaxCount));
            }
            Assert.AreEqual("1.11개", foods[3].CountText);
            Assert.AreEqual("0.91캔", foods[4].CountText);
        }

        [Test] public void FallsBackToNearestFoodOutsideTheTable()
        {
            var tiny = FoodEquivalent.For(1);
            Assert.AreEqual(1, tiny.Count); Assert.AreEqual("방울토마토", tiny[0].Food.Name); Assert.AreEqual("0.33개", tiny[0].CountText);
            var huge = FoodEquivalent.For(6000);
            Assert.AreEqual(1, huge.Count); Assert.AreEqual("라면", huge[0].Food.Name); Assert.AreEqual("12.00봉지", huge[0].CountText);
        }

        [Test] public void TableIsSortedByKcal()
        {
            for (int i = 1; i < FoodEquivalent.Table.Length; i++)
                Assert.Greater(FoodEquivalent.Table[i].Kcal, FoodEquivalent.Table[i - 1].Kcal);
        }
    }
}
