using Newtonsoft.Json;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VRage.Game.ModAPI;

namespace FriendlyGridAccess
{
    public sealed class FriendlyAccessStore
    {
        private readonly object _sync = new object();
        private string _path;

        public Dictionary<long, HashSet<long>> GridFactionAccess { get; set; } = new Dictionary<long, HashSet<long>>();

        public static FriendlyAccessStore Load(string path)
        {
            FriendlyAccessStore store;
            if (File.Exists(path))
                store = JsonConvert.DeserializeObject<FriendlyAccessStore>(File.ReadAllText(path)) ?? new FriendlyAccessStore();
            else
                store = new FriendlyAccessStore();

            store._path = path;
            return store;
        }

        public void Save()
        {
            if (string.IsNullOrWhiteSpace(_path)) return;
            lock (_sync)
            {
                File.WriteAllText(_path, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
        }

        public bool Grant(long gridId, long factionId)
        {
            lock (_sync)
            {
                if (!GridFactionAccess.TryGetValue(gridId, out var set))
                {
                    set = new HashSet<long>();
                    GridFactionAccess[gridId] = set;
                }
                var changed = set.Add(factionId);
                if (changed) Save();
                return changed;
            }
        }

        public bool Revoke(long gridId, long factionId)
        {
            lock (_sync)
            {
                if (!GridFactionAccess.TryGetValue(gridId, out var set)) return false;
                var changed = set.Remove(factionId);
                if (set.Count == 0) GridFactionAccess.Remove(gridId);
                if (changed) Save();
                return changed;
            }
        }

        public IReadOnlyCollection<long> GetGrantedFactions(long gridId)
        {
            lock (_sync)
            {
                return GridFactionAccess.TryGetValue(gridId, out var set)
                    ? set.ToArray()
                    : Array.Empty<long>();
            }
        }

        public bool IsGranted(long gridId, long factionId)
        {
            lock (_sync)
                return GridFactionAccess.TryGetValue(gridId, out var set) && set.Contains(factionId);
        }

        public void PruneMissingGrids()
        {
            if (MySession.Static == null) return;
            lock (_sync)
            {
                var existing = new HashSet<long>(MyEntities.GetEntities().OfType<MyCubeGrid>().Select(g => g.EntityId));
                foreach (var gridId in GridFactionAccess.Keys.Where(id => !existing.Contains(id)).ToList())
                    GridFactionAccess.Remove(gridId);
            }
        }
    }
}
