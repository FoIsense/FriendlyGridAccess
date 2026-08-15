using HarmonyLib;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using System;
using System.IO;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Managers;

namespace FriendlyGridAccess
{
    public sealed class Plugin : TorchPluginBase
    {
        internal static readonly Logger Log = LogManager.GetCurrentClassLogger();
        internal static Plugin Instance { get; private set; }
        internal FriendlyAccessStore Store { get; private set; }
        internal PluginConfig Config { get; private set; }

        private TorchSessionManager _sessionManager;
        private Harmony _harmony;

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;

            Directory.CreateDirectory(StoragePath);
            Config = PluginConfig.Load(Path.Combine(StoragePath, "FriendlyGridAccess.cfg.json"));
            Store = FriendlyAccessStore.Load(Path.Combine(StoragePath, "FriendlyGridAccess.data.json"));

            _sessionManager = torch.Managers.GetManager<TorchSessionManager>();
            if (_sessionManager != null)
                _sessionManager.SessionStateChanged += SessionStateChanged;

            _harmony = new Harmony("com.openai.friendlygridaccess");
            AccessPatch.Apply(_harmony);

            Log.Info($"FriendlyGridAccess loaded. Minimum faction reputation: {Config.MinimumReputation}");
        }

        private void SessionStateChanged(ITorchSession session, TorchSessionState state)
        {
            if (state == TorchSessionState.Loaded)
            {
                Log.Info("FriendlyGridAccess session loaded.");
                Store.PruneMissingGrids();
                Store.Save();
            }
            else if (state == TorchSessionState.Unloading)
            {
                Store.Save();
            }
        }

        public override void Dispose()
        {
            try
            {
                Store?.Save();
                _harmony?.UnpatchAll("com.openai.friendlygridaccess");
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while unloading FriendlyGridAccess.");
            }

            if (_sessionManager != null)
                _sessionManager.SessionStateChanged -= SessionStateChanged;

            Instance = null;
            base.Dispose();
        }
    }
}
