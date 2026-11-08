using System.Collections.Generic;
using WorldTree.Core.Story;

namespace WorldTree.Core.Content
{
    /// <summary>
    /// Areas 1–2 and their dialogue (GDD §7.2, §8), defined in code as the single source for tests and
    /// for the Editor menu that creates the editable assets. Task costs come from simulated Lifebloom
    /// income (roadmap D40): Area 1 = 500 (about 2 days), Area 2 = 900 (about 6 more days).
    /// </summary>
    public static class StoryContent
    {
        public const string Glade = "area.sunlit_glade";
        public const string Village = "area.emberroot_village";
        public const string FirstMemoryLeaf = "memory_leaf.1";

        /// <summary>Display names for dialogue speakers (GDD §8.2).</summary>
        public static readonly IReadOnlyDictionary<string, string> Speakers = new Dictionary<string, string>
        {
            { "wren", "Wren" },
            { "thalen", "Elder Thalen" },
            { "pip", "Pip" },
            { "marra", "Marra Hollowbrook" },
            { "sorrel", "Sorrel" },
            { "fen", "Fen" },
            { "memory_leaf", "Memory Leaf" },
        };

        public static StoryCatalog Build() => new StoryCatalog(Areas(), Scenes());

        public static IReadOnlyList<AreaDef> Areas() => new[]
        {
            new AreaDef(Glade, "Sunlit Glade", new[]
            {
                new RestorationTask("task.glade.path", "Clear the withered path", 15),
                new RestorationTask("task.glade.spring", "Wake the sleeping spring", 20, "scene.glade.spring"),
                new RestorationTask("task.glade.bridge", "Mend the old footbridge", 30),
                new RestorationTask("task.glade.clover", "Replant the clover meadow", 35),
                new RestorationTask("task.glade.lanterns", "Light the glade lanterns", 45, "scene.glade.lanterns"),
                new RestorationTask("task.glade.bluebells", "Free the bluebell ring", 50),
                new RestorationTask("task.glade.hut", "Rebuild the warden's hut", 60, "scene.glade.hut"),
                new RestorationTask("task.glade.stones", "Restore the singing stones", 70),
                new RestorationTask("task.glade.fireflies", "Coax back the fireflies", 80),
                new RestorationTask("task.glade.sapling", "Bloom the Heart Sapling", 95),
            }, "scene.glade.intro", "scene.glade.complete", new[] { PrototypeContent.Oven1 }, null),

            new AreaDef(Village, "Emberroot Village", new[]
            {
                new RestorationTask("task.village.hearth", "Rekindle the first hearth", 50),
                new RestorationTask("task.village.bridges", "Sweep ash from the rope bridges", 60),
                new RestorationTask("task.village.lifts", "Mend the lantern lifts", 70),
                new RestorationTask("task.village.honey", "Refill the honey stores", 80, "scene.village.honey"),
                new RestorationTask("task.village.nets", "Reweave the canopy nets", 85),
                new RestorationTask("task.village.bakers", "Restore the bakers' row", 90),
                new RestorationTask("task.village.chimes", "Rehang the wind chimes", 100, "scene.village.chimes"),
                new RestorationTask("task.village.balcony", "Repair the elder's balcony", 110),
                new RestorationTask("task.village.oven", "Relight the great oven", 120),
                new RestorationTask("task.village.song", "Return the village's song", 135),
            }, "scene.village.intro", "scene.village.complete", new[] { "gen.moonwell_spring.2" }, FirstMemoryLeaf),
        };

        public static IReadOnlyList<DialogueScene> Scenes() => new[]
        {
            Scene("scene.glade.intro",
                ("pip", "Psst! Up here! You must be the new warden."),
                ("wren", "Apprentice warden. Wren Ashleaf. Where is everyone?"),
                ("pip", "Gone grey. Gone quiet. The blight crept in with the last moon."),
                ("wren", "Then we start small. One seed at a time.")),
            Scene("scene.glade.spring",
                ("thalen", "The spring remembers you, little warden."),
                ("wren", "Elder Thalen! You're awake?"),
                ("thalen", "Barely. Every corner you restore lends me a breath. Keep merging, keep mending.")),
            Scene("scene.glade.lanterns",
                ("pip", "Lights! Real lights! I'd almost forgotten what my own glow looked like."),
                ("wren", "Save some sparkle for later, Pip.")),
            Scene("scene.glade.hut",
                ("wren", "The old warden's hut. There's a ledger here, half rotted."),
                ("wren", "The last entry only says: 'She is tending it. She thinks she can hold it back.'"),
                ("pip", "She? She who?")),
            Scene("scene.glade.complete",
                ("thalen", "The glade breathes again. You have done well."),
                ("thalen", "But the blight did not begin here. It drifted down from the village above, where the hearths have gone cold."),
                ("thalen", "Take this hearthstone. Emberroot will need its warmth.")),
            Scene("scene.village.intro",
                ("marra", "Customers! Finally! Well, not customers. Nobody has bought a thing in weeks."),
                ("marra", "Folk wander about forgetting their own names. Ovens cold, songs gone."),
                ("wren", "Then let's warm things up.")),
            Scene("scene.village.honey",
                ("marra", "The honey stores are full again! I could hug you. I won't. Badger manners.")),
            Scene("scene.village.chimes",
                ("sorrel", "We didn't break the chimes!"),
                ("fen", "We only borrowed them."),
                ("sorrel", "And then a pale lady in a hood took them!"),
                ("wren", "A pale lady? Where did she go?"),
                ("fen", "Down. Towards the roots.")),
            Scene("scene.village.complete",
                ("thalen", "A leaf has fallen into your hands, Wren. Hold it up to the light."),
                ("memory_leaf", "Long ago, two seedlings grew side by side at the Tree's foot. One flourished. One withered in a single winter."),
                ("memory_leaf", "The Tree never stopped grieving. Some griefs, buried deep enough, grow roots of their own."),
                ("wren", "The blight... is the Tree's sorrow?"),
                ("thalen", "Perhaps. The reflections of Moonmere may show you more.")),
        };

        private static DialogueScene Scene(string id, params (string speaker, string text)[] lines)
        {
            var list = new List<DialogueLine>(lines.Length);
            foreach (var (speaker, text) in lines) list.Add(new DialogueLine(speaker, text));
            return new DialogueScene(id, list);
        }
    }
}
