using System.Collections.Generic;
using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Content;
using WorldTree.Core.Layout;
using WorldTree.Core.Save;
using WorldTree.Core.Settings;
using WorldTree.Tests.Support;

namespace WorldTree.Tests
{
    /// <summary>06-mobile-layout.md §4 reference devices, and saved settings (GDD §10).</summary>
    public class LayoutAndSettingsTests
    {
        // Device, safe width, safe height (screen minus insets), expected tier, cell, board w, board h, banner.
        [TestCase("iPhone SE (2nd/3rd gen)", 375f, 647f, LayoutTier.Compact, 51.3f, 359f, 462f, 0f)]
        [TestCase("Android 16:9 with 3-button nav", 360f, 568f, LayoutTier.Minimum, 45.8f, 320f, 412f, 0f)]
        [TestCase("Android 20:9", 360f, 760f, LayoutTier.Regular, 49.1f, 344f, 442f, 98f)]
        [TestCase("iPhone 13 mini", 375f, 728f, LayoutTier.Regular, 51.3f, 359f, 462f, 0f)]
        [TestCase("iPhone 15/16", 393f, 759f, LayoutTier.Regular, 53.9f, 377f, 485f, 0f)]
        [TestCase("iPhone 15/16 Pro Max", 430f, 839f, LayoutTier.Regular, 59.1f, 414f, 532f, 87f)]
        [TestCase("iPad (10th gen)", 820f, 1136f, LayoutTier.Regular, 80f, 560f, 720f, 196f)]
        public void ReferenceDevicesMatchTheLayoutSpec(string device, float width, float height, LayoutTier tier, float cell,
            float boardWidth, float boardHeight, float banner)
        {
            var l = BoardLayoutSolver.Solve(width, height, 7, 9);
            Assert.AreEqual(tier, l.Tier, device);
            Assert.AreEqual(cell, l.Cell, 0.1f, device);
            Assert.AreEqual(boardWidth, l.BoardWidth, 1f, device);
            Assert.AreEqual(boardHeight, l.BoardHeight, 1f, device);
            Assert.AreEqual(banner, l.BannerHeight, 1f, device);
        }

        [Test]
        public void CellIsAlwaysBetween44And80Points()
        {
            for (float w = 300; w <= 1200; w += 37)
                for (float h = 500; h <= 1600; h += 41)
                {
                    var l = BoardLayoutSolver.Solve(w, h, 7, 9);
                    Assert.That(l.Cell, Is.InRange(BoardLayoutSolver.MinCell, BoardLayoutSolver.MaxCell), w + "x" + h);
                }
        }

        [Test]
        public void SettingsDefaultOnAndSurviveSaveAndLoad()
        {
            var clock = FakeClock.AtNoon();
            var s = TestContent.EmptySession(clock);
            Assert.IsTrue(s.Settings.Music && s.Settings.Sound && s.Settings.Haptics);
            Assert.IsFalse(s.Settings.ReducedMotion || s.Settings.LargeText);

            s.SetSetting(SettingKind.Music, false);
            s.SetSetting(SettingKind.Haptics, false);
            s.SetSetting(SettingKind.ReducedMotion, true);
            s.SetSetting(SettingKind.LargeText, true);
            var r = SaveMapper.Restore(SaveMapper.Capture(s), TestContent.Catalog, PrototypeContent.Energy, clock, new List<string>());
            Assert.IsFalse(r.Settings.Music);
            Assert.IsTrue(r.Settings.Sound);
            Assert.IsFalse(r.Settings.Haptics);
            Assert.IsTrue(r.Settings.ReducedMotion);
            Assert.IsTrue(r.Settings.LargeText);
        }

        [Test]
        public void OlderSavesGetDefaultSettings()
        {
            var r = SaveMapper.Restore(new SaveData { Width = 7, Height = 9 }, TestContent.Catalog, PrototypeContent.Energy, FakeClock.AtNoon(), new List<string>());
            Assert.IsTrue(r.Settings.Music && r.Settings.Sound && r.Settings.Haptics);
        }

        [Test]
        public void ChangingASettingIsSavedAndRaisesAnEvent()
        {
            var s = TestContent.EmptySession(FakeClock.AtNoon());
            long v = s.StateVersion;
            SettingKind? changed = null;
            s.SettingChanged += (k, value) => changed = k;
            s.SetSetting(SettingKind.Sound, false);
            Assert.AreEqual(SettingKind.Sound, changed);
            Assert.Greater(s.StateVersion, v);
        }
    }
}
