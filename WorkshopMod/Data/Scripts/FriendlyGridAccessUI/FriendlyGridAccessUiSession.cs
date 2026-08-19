using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace FriendlyGridAccessUI
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public sealed class FriendlyGridAccessUiSession : MySessionComponentBase
    {
        private const ushort Channel = 48751;
        private readonly Dictionary<long, long> _selectedFactionByBlock = new Dictionary<long, long>();
        public override void BeforeStart()
        {
            if (MyAPIGateway.Utilities.IsDedicated) return;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(Channel, OnServerMessage);
            AddControls();
        }
        protected override void UnloadData()
        {
            if (!MyAPIGateway.Utilities.IsDedicated && MyAPIGateway.Multiplayer != null)
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(Channel, OnServerMessage);
            _selectedFactionByBlock.Clear();
        }
        private void AddControls()
        {
            var sep = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyTerminalBlock>("FGA_Separator");
            sep.Visible = IsVisible; MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(sep);

            var label = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, IMyTerminalBlock>("FGA_Label");
            label.Label = MyStringId.GetOrCompute("Friendly Grid Access (server-authoritative)"); label.Visible = IsVisible; MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(label);

            var combo = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, IMyTerminalBlock>("FGA_Faction");
            combo.Title = MyStringId.GetOrCompute("Friendly faction");
            combo.Tooltip = MyStringId.GetOrCompute("Factions with reputation >= 500 are eligible. The server re-checks the configured threshold when granting.");
            combo.Visible = IsVisible; combo.Enabled = IsVisible; combo.ComboBoxContent = FillFactions;
            combo.Getter = b => _selectedFactionByBlock.TryGetValue(b.EntityId, out var id) ? id : 0L;
            combo.Setter = (b, id) => _selectedFactionByBlock[b.EntityId] = id;
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(combo);

            var grant = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("FGA_Grant");
            grant.Title = MyStringId.GetOrCompute("Grant friendly access"); grant.Tooltip = MyStringId.GetOrCompute("Keeps current ownership; grants the selected friendly faction access to this grid.");
            grant.Visible = IsVisible; grant.Enabled = IsVisible; grant.Action = b => Send(b, "GRANT");
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(grant);

            var revoke = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyTerminalBlock>("FGA_Revoke");
            revoke.Title = MyStringId.GetOrCompute("Revoke friendly access"); revoke.Visible = IsVisible; revoke.Enabled = IsVisible; revoke.Action = b => Send(b, "REVOKE");
            MyAPIGateway.TerminalControls.AddControl<IMyTerminalBlock>(revoke);
        }
        private bool IsVisible(IMyTerminalBlock block) => block != null && block.CubeGrid != null && MyAPIGateway.Session?.Player != null;
        private void FillFactions(List<MyTerminalControlComboBoxItem> items)
        {
            items.Clear(); var player = MyAPIGateway.Session?.Player; var factions = MyAPIGateway.Session?.Factions; if (player == null || factions == null) return;
            var mine = factions.TryGetPlayerFaction(player.IdentityId); if (mine == null) return;
            foreach (var faction in factions.Factions.Values)
            {
                if (faction == null || faction.FactionId == mine.FactionId) continue;
                var rep = factions.GetReputationBetweenFactions(mine.FactionId, faction.FactionId); if (rep < 500) continue;
                items.Add(new MyTerminalControlComboBoxItem { Key = faction.FactionId, Value = new StringBuilder($"[{faction.Tag}] {faction.Name} (rep {rep})") });
            }
        }
        private void Send(IMyTerminalBlock block, string verb)
        {
            if (block?.CubeGrid == null) return;
            if (!_selectedFactionByBlock.TryGetValue(block.EntityId, out var factionId) || factionId == 0) { MyAPIGateway.Utilities.ShowNotification("FGA: select a friendly faction first.", 3500, "Red"); return; }
            var payload = Encoding.UTF8.GetBytes($"{verb}|{block.CubeGrid.EntityId}|{factionId}");
            MyAPIGateway.Multiplayer.SendMessageToServer(Channel, payload);
        }
        private void OnServerMessage(ushort channel, byte[] data, ulong sender, bool fromServer)
        {
            if (!fromServer || data == null) return; var text = Encoding.UTF8.GetString(data); if (!text.StartsWith("REPLY|")) return;
            MyAPIGateway.Utilities.ShowNotification(text.Substring(6), 5000, "White");
        }
    }
}
