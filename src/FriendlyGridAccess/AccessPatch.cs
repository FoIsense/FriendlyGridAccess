using HarmonyLib;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI.Ingame;
using System;
using System.Reflection;
using VRage.Game;

namespace FriendlyGridAccess
{
    internal static class AccessPatch
    {
        private const string HarmonyId =
            "com.openai.friendlygridaccess";

        internal static void Apply(Harmony harmony)
        {
            if (harmony == null)
                throw new ArgumentNullException(nameof(harmony));

            /*
             * Current Space Engineers exposes terminal access as:
             *
             * HasPlayerAccess(
             *     long playerId,
             *     MyRelationsBetweenPlayerAndBlock defaultNoUser)
             *
             * Older FriendlyGridAccess builds attempted to patch:
             *
             * MyCubeBlock.HasPlayerAccess(long)
             *
             * which no longer exists in current SE builds.
             */

            var terminalBlockType =
                AccessTools.TypeByName(
                    "Sandbox.Game.Entities.Cube.MyTerminalBlock");

            if (terminalBlockType == null)
            {
                throw new TypeLoadException(
                    "Could not locate Sandbox.Game.Entities.Cube.MyTerminalBlock.");
            }

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
                postfix: new HarmonyMethod(postfixMethod));

            Plugin.Log.Info(
                $"FriendlyGridAccess patched " +
                $"{target.DeclaringType?.FullName}.{target.Name}" +
                "(long, MyRelationsBetweenPlayerAndBlock)");
        }

        /*
         * Vanilla is always allowed to decide first.
         *
         * If vanilla already grants access:
         *     do nothing.
         *
         * If vanilla denies access:
         *     FriendlyGridAccess can upgrade the result to true
         *     when the grid has granted the player's faction access
         *     and the faction relationship still meets the configured
         *     reputation threshold.
         *
         * We NEVER convert a vanilla allow into a denial.
         */
            private static void HasPlayerAccessPostfix(
                object __instance,
                long identityId,
                MyRelationsBetweenPlayerAndBlock defaultNoUser,
                ref bool __result)
        {
            if (__result)
                return;

            try
            {
                var block = __instance as MyCubeBlock;

                if (block == null ||
                    block.CubeGrid == null)
                {
                    return;
                }

                var plugin = Plugin.Instance;

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
                 * Same faction is already handled by vanilla.
                 * Do not interfere.
                 */
                if (ownerFaction.FactionId ==
                    playerFaction.FactionId)
                {
                    return;
                }

                var gridId =
                    block.CubeGrid.EntityId;

                /*
                 * This faction must have explicitly been
                 * granted access to this grid.
                 */
                if (!plugin.Store.IsGranted(
                        gridId,
                        playerFaction.FactionId))
                {
                    return;
                }

                /*
                 * Re-check current reputation on every
                 * access decision.
                 *
                 * A grant therefore stops working immediately
                 * if the factions are no longer sufficiently
                 * friendly.
                 */
                if (!FactionHelper.MeetsThreshold(
                        ownerFaction.FactionId,
                        playerFaction.FactionId))
                {
                    return;
                }

                /*
                 * Vanilla denied access, but FriendlyGridAccess
                 * approves this player's faction.
                 */
                __result = true;
            }
            catch (Exception e)
            {
                /*
                 * Fail closed.
                 *
                 * If FriendlyGridAccess itself errors, preserve
                 * the original vanilla denial.
                 */
                Plugin.Log.Error(
                    e,
                    "FriendlyGridAccess access check failed; " +
                    "preserving vanilla denial.");
            }
        }
    }
}
