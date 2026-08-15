using Sandbox.Game.Entities;
using Sandbox.Game.World;
using System;
using System.Linq;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace FriendlyGridAccess
{
    public sealed class Commands : CommandModule
    {
        [Command("fga", "Friendly Grid Access: !fga grant TAG | revoke TAG | list | status")]
        [Permission(MyPromoteLevel.None)]
        public void Fga(string action = "status", string factionTag = null)
        {
            if (Context.Player == null)
            {
                Context.Respond("FGA: this command must be run by an in-game player.");
                return;
            }

            var identityId = Context.Player.IdentityId;
            var playerFaction = FactionHelper.GetFactionForIdentity(identityId);
            if (playerFaction == null)
            {
                Context.Respond("FGA: you must be in a faction.");
                return;
            }

            var grid = FindNearestGrid(Context.Player.Character?.PositionComp?.GetPosition() ?? Vector3D.Zero);
            if (grid == null)
            {
                Context.Respond($"FGA: no grid found within {Plugin.Instance.Config.CommandGridSearchRadiusMeters:0} m.");
                return;
            }

            var ownerFaction = FactionHelper.GetGridOwnerFaction(grid);
            if (ownerFaction == null)
            {
                Context.Respond("FGA: nearest grid has no faction owner.");
                return;
            }

            if (ownerFaction.FactionId != playerFaction.FactionId)
            {
                Context.Respond($"FGA: nearest grid belongs to [{ownerFaction.Tag}], not your faction [{playerFaction.Tag}].");
                return;
            }

            if (Plugin.Instance.Config.RequireFactionLeaderOrFounder && !FactionHelper.IsLeaderOrFounder(playerFaction, identityId))
            {
                Context.Respond("FGA: only the faction founder/leader can change friendly-grid permissions.");
                return;
            }

            action = (action ?? "status").Trim().ToLowerInvariant();
            if (action == "list" || action == "status")
            {
                var ids = Plugin.Instance.Store.GetGrantedFactions(grid.EntityId);
                if (ids.Count == 0)
                {
                    Context.Respond($"FGA: '{grid.DisplayName}' has no friendly factions granted.");
                    return;
                }

                var parts = ids.Select(id =>
                {
                    var f = MySession.Static.Factions.TryGetFactionById(id);
                    if (f == null) return $"{id} (missing)";
                    var rep = FactionHelper.GetFactionReputation(ownerFaction.FactionId, f.FactionId);
                    return $"[{f.Tag}] rep={rep}";
                });
                Context.Respond($"FGA: '{grid.DisplayName}' grants: {string.Join(", ", parts)}");
                return;
            }

            if (string.IsNullOrWhiteSpace(factionTag))
            {
                Context.Respond("FGA usage: !fga grant TAG | !fga revoke TAG | !fga list");
                return;
            }

            var target = FactionHelper.GetFactionByTag(factionTag);
            if (target == null)
            {
                Context.Respond($"FGA: faction '{factionTag}' not found.");
                return;
            }
            if (target.FactionId == ownerFaction.FactionId)
            {
                Context.Respond("FGA: your own faction already has vanilla access.");
                return;
            }

            if (action == "grant")
            {
                var rep = FactionHelper.GetFactionReputation(ownerFaction.FactionId, target.FactionId);
                if (rep < Plugin.Instance.Config.MinimumReputation)
                {
                    Context.Respond($"FGA: [{target.Tag}] has reputation {rep}; requires {Plugin.Instance.Config.MinimumReputation}.");
                    return;
                }

                var changed = Plugin.Instance.Store.Grant(grid.EntityId, target.FactionId);
                Context.Respond(changed
                    ? $"FGA: [{target.Tag}] can now use '{grid.DisplayName}' while your faction keeps ownership."
                    : $"FGA: [{target.Tag}] was already granted on '{grid.DisplayName}'.");
                return;
            }

            if (action == "revoke")
            {
                var changed = Plugin.Instance.Store.Revoke(grid.EntityId, target.FactionId);
                Context.Respond(changed
                    ? $"FGA: access for [{target.Tag}] revoked on '{grid.DisplayName}'."
                    : $"FGA: [{target.Tag}] did not have an FGA grant on '{grid.DisplayName}'.");
                return;
            }

            Context.Respond("FGA usage: !fga grant TAG | !fga revoke TAG | !fga list");
        }

private MyCubeGrid FindNearestGrid(Vector3D position)
{
    var radius = Plugin.Instance.Config.CommandGridSearchRadiusMeters;
    var radiusSquared = radius * radius;

    MyCubeGrid nearestGrid = null;
    double nearestDistanceSquared = double.MaxValue;

    foreach (var grid in MyEntities.GetEntities().OfType<MyCubeGrid>())
    {
        if (grid == null || grid.Closed)
            continue;

        var box = grid.PositionComp.WorldAABB;

        // Find the closest point on the grid's world bounding box
        // to the player's current position.
        var closestPoint = new Vector3D(
            Math.Max(box.Min.X, Math.Min(position.X, box.Max.X)),
            Math.Max(box.Min.Y, Math.Min(position.Y, box.Max.Y)),
            Math.Max(box.Min.Z, Math.Min(position.Z, box.Max.Z))
        );

        var distanceSquared =
            Vector3D.DistanceSquared(position, closestPoint);

        if (distanceSquared > radiusSquared)
            continue;

        if (distanceSquared < nearestDistanceSquared)
        {
            nearestDistanceSquared = distanceSquared;
            nearestGrid = grid;
        }
    }

    return nearestGrid;
}
    }
}
