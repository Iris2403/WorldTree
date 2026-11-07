using System;
using NUnit.Framework;
using WorldTree.Core.Energy;
using WorldTree.Tests.Support;

namespace WorldTree.Tests
{
    /// <summary>GDD §5 Sap rules.</summary>
    public class EnergyTests
    {
        private FakeClock _clock;
        private EnergyModel _e;

        [SetUp]
        public void SetUp()
        {
            _clock = FakeClock.AtNoon();
            _e = EnergyModel.Full(EnergyConfig.Default);
        }

        private int Now()
        {
            _e.Refresh(_clock.UtcNow);
            return _e.Amount;
        }

        [Test]
        public void SpendingFromCapStartsTimerAtSpendTime()
        {
            Assert.IsTrue(_e.TrySpend(1, _clock.UtcNow));
            Assert.AreEqual(_clock.UtcNow.AddSeconds(120), _e.NextTickUtc);
        }

        [Test]
        public void RegeneratesOnePer120SecondsUpToCap()
        {
            _e.TrySpend(10, _clock.UtcNow);
            _clock.AdvanceSeconds(119);
            Assert.AreEqual(90, Now());
            _clock.AdvanceSeconds(1);
            Assert.AreEqual(91, Now());
            _clock.AdvanceSeconds(120 * 3);
            Assert.AreEqual(94, Now());
        }

        [Test]
        public void SpendingWhileBelowCapDoesNotResetTimer()
        {
            _e.TrySpend(10, _clock.UtcNow);
            var tick = _e.NextTickUtc;
            _clock.AdvanceSeconds(60);
            _e.TrySpend(5, _clock.UtcNow);
            Assert.AreEqual(tick, _e.NextTickUtc);
        }

        [Test]
        public void OfflineGapRegeneratesToCapOnlyAndStopsTimer()
        {
            _e.TrySpend(100, _clock.UtcNow);
            _clock.AdvanceMinutes(60 * 5);
            Assert.AreEqual(100, Now());
            Assert.IsNull(_e.NextTickUtc);
        }

        [Test]
        public void FullFromEmptyTakesExactly200Minutes()
        {
            _e.TrySpend(100, _clock.UtcNow);
            _clock.Advance(TimeSpan.FromMinutes(200) - TimeSpan.FromSeconds(1));
            Assert.AreEqual(99, Now());
            _clock.AdvanceSeconds(1);
            Assert.AreEqual(100, Now());
        }

        [Test]
        public void OverCapAddIsKeptAndDoesNotRegenerate()
        {
            _e.Add(50, _clock.UtcNow);
            Assert.AreEqual(150, Now());
            _clock.AdvanceMinutes(60);
            Assert.AreEqual(150, Now());
            _e.TrySpend(20, _clock.UtcNow);
            Assert.IsNull(_e.NextTickUtc, "still above cap after spending");
            Assert.AreEqual(130, Now());
        }

        [Test]
        public void ClockMovedBackwardsGivesNoSapAndDoesNotCrash()
        {
            _e.TrySpend(50, _clock.UtcNow);
            _clock.AdvanceMinutes(-600);
            Assert.AreEqual(50, Now());
            Assert.AreEqual(_clock.UtcNow.AddSeconds(120), _e.NextTickUtc);
            _clock.AdvanceSeconds(120);
            Assert.AreEqual(51, Now());
        }

        [Test]
        public void TrySpendFailsAtomically()
        {
            _e.TrySpend(95, _clock.UtcNow);
            var tick = _e.NextTickUtc;
            Assert.IsFalse(_e.TrySpend(6, _clock.UtcNow));
            Assert.AreEqual(5, _e.Amount);
            Assert.AreEqual(tick, _e.NextTickUtc);
        }
    }
}
