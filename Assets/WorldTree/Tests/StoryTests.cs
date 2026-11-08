using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WorldTree.Core;
using WorldTree.Core.Content;
using WorldTree.Core.Save;
using WorldTree.Core.Story;
using WorldTree.Tests.Support;
using static WorldTree.Tests.Support.TestContent;

namespace WorldTree.Tests
{
    /// <summary>Phase 5: quest progression, areas, dialogue queue, Memory Leaves, save/restore mid-quest.</summary>
    public class StoryTests
    {
        private FakeClock _clock;
        private StoryCatalog _story;
        private GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = FakeClock.AtNoon();
            _story = StoryContent.Build();
            _s = GameSession.NewGame(TestContent.Catalog, PrototypeContent.StartingLayout(), PrototypeContent.Energy, _clock, 3, _story);
        }

        private void GiveLifebloom(int amount) => _s.Wallet.AddLifebloom(amount);

        private void RestoreAll(AreaDef area)
        {
            GiveLifebloom(area.TotalCost);
            foreach (var _ in area.Tasks) Assert.That(_s.RestoreNextTask(), Is.EqualTo(RestoreResult.Restored).Or.EqualTo(RestoreResult.AreaRestored));
        }

        [Test]
        public void StoryContentIsValid()
        {
            CollectionAssert.IsEmpty(_story.Validate(TestContent.Catalog));
            Assert.AreEqual(500, _story.Areas[0].TotalCost);
            Assert.AreEqual(900, _story.Areas[1].TotalCost);
        }

        [Test]
        public void NewGameStartsInTheGladeWithItsIntroScene()
        {
            Assert.AreEqual(StoryContent.Glade, _s.CurrentArea.Id);
            Assert.AreEqual("scene.glade.intro", _s.PendingDialogue.Id);
            Assert.AreEqual("task.glade.path", _s.NextTask.Id);
        }

        [Test]
        public void TasksAreRestoredInOrderForTheirLifebloomCost()
        {
            GiveLifebloom(40);
            Assert.AreEqual(RestoreResult.Restored, _s.RestoreNextTask());
            Assert.AreEqual(25, _s.Wallet.Lifebloom);
            Assert.AreEqual("task.glade.spring", _s.NextTask.Id);
            Assert.AreEqual(RestoreResult.Restored, _s.RestoreNextTask());
            Assert.AreEqual(5, _s.Wallet.Lifebloom);
            Assert.IsTrue(_s.Quest.IsTaskCompleted("task.glade.spring"));
        }

        [Test]
        public void NotEnoughLifebloomChangesNothing()
        {
            GiveLifebloom(14);
            long v = _s.StateVersion;
            Assert.AreEqual(RestoreResult.NotEnoughLifebloom, _s.RestoreNextTask());
            Assert.AreEqual(14, _s.Wallet.Lifebloom);
            Assert.AreEqual("task.glade.path", _s.NextTask.Id);
            Assert.AreEqual(v, _s.StateVersion);
        }

        [Test]
        public void TaskScenesAreQueuedAndCanBeDismissedInOrder()
        {
            _s.DismissDialogue("scene.glade.intro");
            Assert.IsNull(_s.PendingDialogue);
            GiveLifebloom(35);
            _s.RestoreNextTask();
            Assert.IsNull(_s.PendingDialogue, "the first task has no scene");
            string queued = null;
            _s.DialogueQueued += id => queued = id;
            _s.RestoreNextTask();
            Assert.AreEqual("scene.glade.spring", queued);
            Assert.AreEqual("scene.glade.spring", _s.PendingDialogue.Id);
            _s.DismissDialogue("scene.glade.spring");
            Assert.IsNull(_s.PendingDialogue);
        }

        [Test]
        public void RestoringTheGladeGrantsTheOvenAndOpensTheVillage()
        {
            _s.DismissDialogue("scene.glade.intro");
            AreaDef restored = null;
            _s.AreaRestored += a => restored = a;
            RestoreAll(_story.Areas[0]);

            Assert.AreEqual(StoryContent.Glade, restored.Id);
            Assert.AreEqual(StoryContent.Village, _s.CurrentArea.Id);
            Assert.IsTrue(_s.Quest.HasFlag("area." + StoryContent.Glade + ".restored"));
            Assert.IsTrue(OnBoardOrInSatchel(PrototypeContent.Oven1), "the Hearthstone Oven is granted");
            Assert.IsTrue(_s.Codex.IsDiscovered(PrototypeContent.Oven1));
            var pending = _s.Quest.PendingDialogues.ToList();
            CollectionAssert.AreEqual(new[] { "scene.glade.spring", "scene.glade.lanterns", "scene.glade.hut", "scene.glade.complete", "scene.village.intro" }, pending);
        }

        [Test]
        public void TheOvenUnlocksFoodOrders()
        {
            RestoreAll(_story.Areas[0]);
            Assert.That(_s.AvailableItemChains().Select(c => c.Id), Has.Member(PrototypeContent.FoodChain));
        }

        [Test]
        public void RestoringTheVillageGivesTheFirstMemoryLeaf()
        {
            RestoreAll(_story.Areas[0]);
            Assert.IsFalse(_s.Quest.HasFlag(StoryContent.FirstMemoryLeaf), "the glade is the tutorial: no leaf");
            RestoreAll(_story.Areas[1]);
            Assert.IsTrue(_s.Quest.HasFlag(StoryContent.FirstMemoryLeaf));
            Assert.IsNull(_s.CurrentArea, "Areas 3-5 aren't built yet");
            Assert.AreEqual(RestoreResult.NothingToRestore, _s.RestoreNextTask());
        }

        [Test]
        public void StoryRewardsAreNeverLostWhenBoardAndSatchelAreFull()
        {
            FillBoard(_s, "item.nature.08");
            // Fill the satchel from an Open centre cell (the outer cells of the starting board are locked).
            for (int i = 0; i < 4; i++)
            {
                _s.Board.SetItem(P(2, 3), TestContent.Item("item.water.01"));
                Assert.AreEqual(StoreResult.Stored, _s.StoreInSatchel(P(2, 3)));
            }
            _s.Board.SetItem(P(2, 3), TestContent.Item("item.nature.08"));
            Assert.IsTrue(_s.Satchel.IsFull);
            RestoreAll(_story.Areas[0]);
            Assert.AreEqual(5, _s.Satchel.Count, "the oven goes into the satchel even past its capacity");
            Assert.AreEqual(PrototypeContent.Oven1, _s.Satchel.Items[4].ItemId);
        }

        [Test]
        public void SessionWithoutStoryHasNothingToRestore()
        {
            var plain = EmptySession(_clock);
            Assert.IsNull(plain.CurrentArea);
            Assert.IsNull(plain.PendingDialogue);
            Assert.AreEqual(RestoreResult.NothingToRestore, plain.RestoreNextTask());
        }

        [Test]
        public void SaveAndRestoreMidQuest()
        {
            GiveLifebloom(100);
            for (int i = 0; i < 4; i++) _s.RestoreNextTask(); // path, spring, bridge, clover = 100
            _s.DismissDialogue("scene.glade.intro");

            var fs = new InMemoryFileSystem();
            new AtomicSaveStore(fs, "saves").Save(SaveMapper.Capture(_s), _clock.UtcNow, false);
            var data = new AtomicSaveStore(fs, "saves").Load(_clock.UtcNow).Data;
            var r = SaveMapper.Restore(data, TestContent.Catalog, PrototypeContent.Energy, _clock, new List<string>(), _story);

            Assert.AreEqual(StoryContent.Glade, r.CurrentArea.Id);
            Assert.AreEqual("task.glade.lanterns", r.NextTask.Id);
            Assert.AreEqual("scene.glade.spring", r.PendingDialogue.Id, "the unseen scene is still waiting");
            Assert.AreEqual(0, r.Wallet.Lifebloom);
            Assert.AreEqual(Newtonsoft.Json.JsonConvert.SerializeObject(SaveMapper.Capture(_s)),
                            Newtonsoft.Json.JsonConvert.SerializeObject(SaveMapper.Capture(r)));
        }

        [Test]
        public void OlderSaveWithoutQuestDataStartsAtTheFirstArea()
        {
            var data = new SaveData { Width = 7, Height = 9, Sap = 50 };
            var r = SaveMapper.Restore(data, TestContent.Catalog, PrototypeContent.Energy, _clock, new List<string>(), _story);
            Assert.AreEqual(StoryContent.Glade, r.CurrentArea.Id);
            Assert.AreEqual("task.glade.path", r.NextTask.Id);
        }

        private bool OnBoardOrInSatchel(string itemId)
        {
            if (_s.Satchel.Items.Any(i => i.ItemId == itemId)) return true;
            for (int y = 0; y < _s.Board.Height; y++)
                for (int x = 0; x < _s.Board.Width; x++)
                    if (_s.Board.GetItem(P(x, y))?.ItemId == itemId) return true;
            return false;
        }
    }
}
