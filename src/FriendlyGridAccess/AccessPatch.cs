using HarmonyLib;
using Sandbox.Game.Entities;
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

            PatchGetUserRelationToOwner(
                harmony);
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

            var postfix =
                typeof(AccessPatch).GetMethod(
                    nameof(HasPlayerAccessPostfix),
                    BindingFlags.Static |
                    BindingFlags.NonPublic);

            if (postfix == null)
            {
                throw new MissingMethodException(
                    typeof(AccessPatch).FullName,
                    nameof(HasPlayerAccessPostfix));
            }

            harmony.Patch(
                target,
                postfix: new HarmonyMethod(postfix));

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

            var postfix =
                typeof(AccessPatch).GetMethod(
                    nameof(HasPlayerAccessWithNobodyCheckPostfix),
                    BindingFlags.Static |
                    BindingFlags.NonPublic);

            if (postfix == null)
            {
                throw new MissingMethodException(
                    typeof(AccessPatch).FullName,
                    nameof(HasPlayerAccessWithNobodyCheckPostfix));
            }

            harmony.Patch(
                target,
                postfix: new HarmonyMethod(postfix));

            Plugin.Log.Info(
                "FriendlyGridAccess patched " +
                "MyTerminalBlock.HasPlayerAccessWithNobodyCheck.");
        }

        private static void PatchGetUserRelationToOwner(
            Harmony harmony)
        {
            var target = AccessTools.Method(
                typeof(MyCubeBlock),
                "GetUserRelationToOwner",
                new[]
                {
                    typeof(long),
                    typeof(MyRelationsBetweenPlayerAndBlock)
                });

            if (target == null)
            {
                throw new MissingMethodException(
                    typeof(MyCubeBlock).FullName,
                    "GetUserRelationToOwner(long, MyRelationsBetweenPlayerAndBlock)");
            }

            var postfix =
                typeof(AccessPatch).GetMethod(
                    nameof(GetUserRelationToOwnerPostfix),
                    BindingFlags.Static |
                    BindingFlags.NonPublic);

            if (postfix == null)
            {
                throw new MissingMethodException(
                    typeof(AccessPatch).FullName,
                    nameof(GetUserRelationToOwnerPostfix));
            }

            harmony.Patch(
                target,
                postfix: new HarmonyMethod(postfix));

            Plugin.Log.Info(
                "FriendlyGridAccess patched " +
                "MyCubeBlock.GetUserRelationToOwner.");
        }

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

            if (__0 == 0)
                return;

            TryGrantFriendlyAccess(
                __instance,
                __0,
                ref __result);
        }

        private static void GetUserRelationToOwnerPostfix(
            MyCubeBlock __instance,
            long __0,
            ref MyRelationsBetweenPlayerAndBlock __result)
        {
            /*
             * Never weaken an existing Owner/FactionShare result.
             */
            if (__result ==
                    MyRelationsBetweenPlayerAndBlock.Owner ||
                __result ==
                    MyRelationsBetweenPlayerAndBlock.FactionShare)
            {
                return;
            }

            if (__0 == 0)
                return;

            try
            {
                if (HasFriendlyGridPermission(
                        __instance,
                        __0))
                {
                    /*
                     * Make approved cross-faction players look
                     * faction-shared to vanilla access logic.
                     *
                     * Ownership itself is NOT changed.
                     */
                    __result =
                        MyRelationsBetweenPlayerAndBlock.FactionShare;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error(
                    e,
                    "Friendly relation check failed; " +
                    "preserving vanilla relation.");
            }
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

                if (block == null)
                    return;

                if (HasFriendlyGridPermission(
                        block,
                        identityId))
                {
                    result = true;
                }
            }
            catch (Exception e)
            {
                Plugin.Log.Error(
                    e,
                    "Friendly access check failed; " +
                    "preserving vanilla denial.");
            }
        }

        private static bool HasFriendlyGridPermission(
            MyCubeBlock block,
            long identityId)
        {
            if (block == null ||
                block.CubeGrid == null)
            {
                return false;
            }

            var plugin =
                Plugin.Instance;

            if (plugin == null ||
                plugin.Store == null ||
                plugin.Config == null)
            {
                return false;
            }

            var playerFaction =
                FactionHelper.GetFactionForIdentity(
                    identityId);

            if (playerFaction == null)
                return false;

            var ownerFaction =
                FactionHelper.GetGridOwnerFaction(
                    block.CubeGrid);

            if (ownerFaction == null)
                return false;

            /*
             * Vanilla already handles members
             * of the owning faction.
             */
            if (ownerFaction.FactionId ==
                playerFaction.FactionId)
            {
                return false;
            }

            var gridId =
                block.CubeGrid.EntityId;

            /*
             * Explicit FGA grant is required.
             */
            if (!plugin.Store.IsGranted(
                    gridId,
                    playerFaction.FactionId))
            {
                return false;
            }

            /*
             * Current faction reputation must still
             * meet the configured threshold.
             *
             * Set MinimumReputation to 500 in config
             * for your new rule.
             */
            if (!FactionHelper.MeetsThreshold(
                    ownerFaction.FactionId,
                    playerFaction.FactionId))
            {
                return false;
            }

            return true;
        }
    }
}
