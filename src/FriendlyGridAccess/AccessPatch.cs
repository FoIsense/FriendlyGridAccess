using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using System;
using System.Reflection;
using VRage.Game;

namespace FriendlyGridAccess
{
    internal static class AccessPatch
    {
        internal static void Apply(Harmony harmony)
        {
            if (harmony == null)
                throw new ArgumentNullException(nameof(harmony));

            var terminalBlockType =
                AccessTools.TypeByName(
                    "Sandbox.Game.Entities.Cube.MyTerminalBlock");

            if (terminalBlockType == null)
            {
                throw new TypeLoadException(
                    "Could not locate Sandbox.Game.Entities.Cube.MyTerminalBlock.");
            }

            PatchHasPlayerAccess(
                harmony,
                terminalBlockType);

            PatchHasPlayerAccessWithNobodyCheck(
                harmony,
                terminalBlockType);
        }

        private static void PatchHasPlayerAccess(
            Harmony harmony,
            Type terminalBlockType)
        {
            var target = AccessTools.Method(
                terminalBlockType,
                "HasPlayerAccess",
                new[]
                {
                    typeof(long),
                    typeof(MyRelationsBetweenPlayerAndBlock)
                });

            if (target == null)
            {
                throw new MissingMethodException(
                    terminalBlockType.FullName,
                    "HasPlayerAccess(long, MyRelationsBetweenPlayerAndBlock)");
            }

            var postfixMethod =
                typeof(AccessPatch).GetMethod(
                    nameof(HasPlayerAccessPostfix),
                    BindingFlags.Static |
                    BindingFlags.NonPublic);

            if (postfixMethod == null)
            {
                throw new MissingMethodException(
                    typeof(AccessPatch).FullName,
                    nameof(HasPlayerAccessPostfix));
            }

            harmony.Patch(
                target,
                postfix:
                    new HarmonyMethod(postfixMethod));

            Plugin.Log.Info(
                "FriendlyGridAccess patched " +
                "MyTerminalBlock.HasPlayerAccess.");
        }

        private static void PatchHasPlayerAccessWithNobodyCheck(
            Harmony harmony,
            Type terminalBlockType)
        {
            var target = AccessTools.Method(
                terminalBlockType,
                "HasPlayerAccessWithNobodyCheck",
                new[]
                {
                    typeof(long),
                    typeof(bool)
                });

            if (target == null)
            {
                throw new MissingMethodException(
                    terminalBlockType.FullName,
                    "HasPlayerAccessWithNobodyCheck(long, bool)");
            }

            var postfixMethod =
                typeof(AccessPatch).GetMethod(
                    nameof(HasPlayerAccessWithNobodyCheckPostfix),
                    BindingFlags.Static |
                    BindingFlags.NonPublic);

            if (postfixMethod == null)
            {
                throw new MissingMethodException(
                    typeof(AccessPatch).FullName,
                    nameof(HasPlayerAccessWithNobodyCheckPostfix));
            }

            harmony.Patch(
                target,
                postfix:
                    new HarmonyMethod(postfixMethod));

            Plugin.Log.Info(
                "FriendlyGridAccess patched " +
                "MyTerminalBlock.HasPlayerAccessWithNobodyCheck.");
        }

        /*
         * __0 means the first argument of the original method.
         *
         * We deliberately use Harmony's positional argument
         * syntax instead of the original argument name so
         * future SE parameter-name changes don't break us.
         */
        private static void HasPlayerAccessPostfix(
            object __instance,
            long __0,
            ref bool __result)
        {
            if (__result)
                return;

            TryGrantFriendlyAccess(
                __instance,
                __0,
                ref __result);
        }

        private static void HasPlayerAccessWithNobodyCheckPostfix(
            object __instance,
            long __0,
            ref bool __result)
        {
            if (__result)
                return;

            /*
             * Identity ID 0 represents Nobody.
             * FGA only grants actual players access.
             */
            if (__0 == 0)
                return;

            TryGrantFriendlyAccess(
                __instance,
                __0,
                ref __result);
        }

        private static void TryGrantFriendlyAccess(
            object instance,
            long identityId,
            ref bool result)
        {
            try
            {
                var block =
                    instance as MyCubeBlock;

                if (block == null ||
                    block.CubeGrid == null)
                {
                    return;
                }

                var plugin =
                    Plugin.Instance;

                if (plugin == null ||
                    plugin.Store == null ||
                    plugin.Config == null)
                {
                    return;
                }

                var playerFaction =
                    FactionHelper.GetFactionForIdentity(
                        identityId);

                if (playerFaction == null)
                    return;

                var ownerFaction =
                    FactionHelper.GetGridOwnerFaction(
                        block.CubeGrid);

                if (ownerFaction == null)
                    return;

                /*
                 * Vanilla already handles same-faction access.
                 */
                if (ownerFaction.FactionId ==
                    playerFaction.FactionId)
                {
                    return;
                }

                var gridId =
                    block.CubeGrid.EntityId;

                /*
                 * Target faction must have been explicitly
                 * granted permission for this grid.
                 */
                if (!plugin.Store.IsGranted(
                        gridId,
                        playerFaction.FactionId))
                {
                    return;
                }

                /*
                 * The factions must still meet the configured
                 * reputation threshold right now.
                 */
                if (!FactionHelper.MeetsThreshold(
                        ownerFaction.FactionId,
                        playerFaction.FactionId))
                {
                    return;
                }

                /*
                 * FriendlyGridAccess only upgrades a vanilla
                 * denial. It never removes vanilla access.
                 */
                result = true;
            }
            catch (Exception e)
            {
                Plugin.Log.Error(
                    e,
                    "Friendly access check failed; " +
                    "preserving vanilla denial.");
            }
        }
    }
}
