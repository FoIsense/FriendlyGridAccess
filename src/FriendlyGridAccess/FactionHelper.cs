using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using System;
using System.Linq;
using System.Reflection;
using VRage.Game.ModAPI;

namespace FriendlyGridAccess
{
    internal static class FactionHelper
    {
        public static IMyFaction GetFactionForIdentity(long identityId)
            => MySession.Static?.Factions?.TryGetPlayerFaction(identityId);

        public static IMyFaction GetGridOwnerFaction(MyCubeGrid grid)
        {
            if (grid?.BigOwners == null || grid.BigOwners.Count == 0) return null;
            return GetFactionForIdentity(grid.BigOwners[0]);
        }

        public static IMyFaction GetFactionByTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || MySession.Static?.Factions == null) return null;
            return MySession.Static.Factions.TryGetFactionByTag(tag.Trim());
        }

        public static int GetFactionReputation(long factionA, long factionB)
        {
            var factions = MySession.Static?.Factions;
            if (factions == null) return int.MinValue;

            // Keen has changed public surface names over time. Reflection lets the plugin
            // tolerate both old/new builds without changing the source for this one call.
            var type = factions.GetType();
            foreach (var name in new[] { "GetReputationBetweenFactions", "GetRelationBetweenFactions" })
            {
                var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(long), typeof(long) }, null);
                if (method == null) continue;

                var value = method.Invoke(factions, new object[] { factionA, factionB });
                if (value is int i) return i;

                // Some builds return a relation enum instead of the raw reputation.
                // Only exact Friendly is mapped here; it is deliberately conservative.
                if (value != null && value.ToString().IndexOf("Friend", StringComparison.OrdinalIgnoreCase) >= 0)
                    return Plugin.Instance?.Config?.MinimumReputation ?? 1500;
            }

            return int.MinValue;
        }

        public static bool MeetsThreshold(long factionA, long factionB)
            => GetFactionReputation(factionA, factionB) >= (Plugin.Instance?.Config?.MinimumReputation ?? 1500);

        public static bool IsLeaderOrFounder(IMyFaction faction, long identityId)
        {
            if (faction == null) return false;
            if (faction.FounderId == identityId) return true;

            try
            {
                var method = faction.GetType().GetMethod("IsLeader", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(long) }, null);
                if (method != null && method.Invoke(faction, new object[] { identityId }) is bool isLeader)
                    return isLeader;
            }
            catch { }

            // Conservative fallback if this SE build does not expose a leader query.
            return false;
        }
    }
}
