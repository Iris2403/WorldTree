using System;
using System.Collections.Generic;
using WorldTree.Core.Items;

namespace WorldTree.Core.Story
{
    /// <summary>All areas, in play order, and all dialogue scenes. Validated on construction.</summary>
    public sealed class StoryCatalog
    {
        private readonly Dictionary<string, DialogueScene> _scenes = new Dictionary<string, DialogueScene>(StringComparer.Ordinal);

        public StoryCatalog(IReadOnlyList<AreaDef> areas, IEnumerable<DialogueScene> scenes)
        {
            Areas = areas ?? throw new ArgumentNullException(nameof(areas));
            foreach (var scene in scenes)
            {
                if (_scenes.ContainsKey(scene.Id)) throw new ArgumentException("Duplicate dialogue scene: " + scene.Id);
                _scenes.Add(scene.Id, scene);
            }
        }

        public IReadOnlyList<AreaDef> Areas { get; }

        public bool TryGetScene(string id, out DialogueScene scene)
        {
            if (id == null)
            {
                scene = null;
                return false;
            }
            return _scenes.TryGetValue(id, out scene);
        }

        /// <summary>Content errors: duplicate IDs, missing scenes, unknown reward items. Empty when valid.</summary>
        public List<string> Validate(ICatalog items)
        {
            var errors = new List<string>();
            var areaIds = new HashSet<string>(StringComparer.Ordinal);
            var taskIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var area in Areas)
            {
                if (!areaIds.Add(area.Id)) errors.Add("Duplicate area: " + area.Id);
                CheckScene(area.IntroDialogueId, area.Id + " intro", errors);
                CheckScene(area.CompletionDialogueId, area.Id + " completion", errors);
                foreach (var item in area.RewardItemIds)
                    if (!items.TryGetItem(item, out _)) errors.Add(area.Id + " rewards unknown item " + item);
                foreach (var task in area.Tasks)
                {
                    if (!taskIds.Add(task.Id)) errors.Add("Duplicate task: " + task.Id);
                    CheckScene(task.DialogueId, task.Id, errors);
                }
            }
            return errors;
        }

        private void CheckScene(string id, string owner, List<string> errors)
        {
            if (id != null && !_scenes.ContainsKey(id)) errors.Add(owner + " refers to missing dialogue scene " + id);
        }
    }
}
