using HarmonyLib;
using Sandbox.Game.Entities;
using System;
using System.Reflection;

namespace FriendlyGridAccess
{
    internal static class AccessPatch
    {
        public static void Apply(Harmony harmony)
        {
            // Resolve the exact HasPlayerAccess(long) overload and fail with a useful
            // message if a future Space Engineers build changes/removes it.
            var target = typeof(MyCubeBlock).GetMethod(
                "HasPlayerAccess",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(long) },
                modifiers: null);

            if (target == null)
            {
                throw new MissingMethodException(
                    typeof(MyCubeBlock).FullName,
                    "HasPlayerAccess(long)");
            }

            // GetMethod can return null. Resolve it first so nullable analysis is
            // satisfied and Harmony never receives a null MethodInfo.
            var postfixMethod = typeof(AccessPatch).GetMethod(
                nameof(Postfix),
                BindingFlags.Static | BindingFlags.NonPublic);

            if (postfixMethod == null)
            {
                throw new MissingMethodException(
                    typeof(AccessPatch).FullName,
                    nameof(Postfix));
            }

            harmony.Patch(
                target,
                postfix: new HarmonyMethod(postfixMethod));

            Plugin.Log.Info(
                $"Patched {target.DeclaringType?.FullName}.{target.Name}(long)");
        }

        // We never turn vanilla access OFF. We only upgrade a vanilla denial to an FGA allow.
        private static void Postfix(MyCubeBlock __instance, long playerId, ref bool __result)
        {
            if (__result || __instance?.CubeGrid == null)
                return;

            try
            {
                var plugin = Plugin.Instance;
                if (plugin?.Store == null)
                    return;

                var playerFaction = FactionHelper.GetFactionForIdentity(playerId);
                if (playerFaction == null)
                    return;

                var ownerFaction = FactionHelper.GetGridOwnerFaction(__instance.CubeGrid);
                if (ownerFaction == null || ownerFaction.FactionId == playerFaction.FactionId)
                    return;

                var gridId = __instance.CubeGrid.EntityId;
                if (!plugin.Store.IsGranted(gridId, playerFaction.FactionId))
                    return;

                // Reputation is checked on every access decision, so a relationship drop
                // immediately disables the extra permission without rewriting ownership.
                if (!FactionHelper.MeetsThreshold(ownerFaction.FactionId, playerFaction.FactionId))
                    return;

                __result = true;
            }
            catch (Exception e)
            {
                Plugin.Log.Error(
                    e,
                    "Friendly access check failed; preserving vanilla denial.");
            }
        }
    }
}
