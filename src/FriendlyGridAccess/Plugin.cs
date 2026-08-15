using HarmonyLib;
using NLog;
using System;
using System.IO;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Session;

namespace FriendlyGridAccess
{
    public sealed class Plugin : TorchPluginBase
    {
        internal static readonly Logger Log = LogManager.GetCurrentClassLogger();

        internal static Plugin Instance { get; private set; }

        internal FriendlyAccessStore Store { get; private set; }

        internal PluginConfig Config { get; private set; }

        private ITorchSessionManager _sessionManager;

        private Harmony _harmony;

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            Instance = this;

            try
            {
                Directory.CreateDirectory(StoragePath);

                Config = PluginConfig.Load(
                    Path.Combine(
                        StoragePath,
                        "FriendlyGridAccess.cfg.json"
                    )
                );

                Store = FriendlyAccessStore.Load(
                    Path.Combine(
                        StoragePath,
                        "FriendlyGridAccess.data.json"
                    )
                );

                _sessionManager =
                    torch.Managers.GetManager(
                        typeof(ITorchSessionManager)
                    ) as ITorchSessionManager;

                if (_sessionManager != null)
                {
                    _sessionManager.SessionStateChanged +=
                        SessionStateChanged;

                    Log.Info(
                        "FriendlyGridAccess connected to Torch session manager."
                    );
                }
                else
                {
                    Log.Warn(
                        "FriendlyGridAccess could not locate ITorchSessionManager. " +
                        "The plugin will load, but session save/load handling may not work correctly."
                    );
                }

                _harmony = new Harmony(
                    "com.openai.friendlygridaccess"
                );

                AccessPatch.Apply(_harmony);

                Log.Info(
                    $"FriendlyGridAccess loaded. " +
                    $"Minimum faction reputation: {Config.MinimumReputation}"
                );
            }
            catch (Exception e)
            {
                Log.Error(
                    e,
                    "FriendlyGridAccess failed during initialization."
                );

                throw;
            }
        }

        private void SessionStateChanged(
            ITorchSession session,
            TorchSessionState state
        )
        {
            try
            {
                if (state == TorchSessionState.Loaded)
                {
                    Log.Info(
                        "FriendlyGridAccess session loaded."
                    );

                    if (Store != null)
                    {
                        Store.PruneMissingGrids();
                        Store.Save();
                    }
                }
                else if (state == TorchSessionState.Unloading)
                {
                    Log.Info(
                        "FriendlyGridAccess session unloading."
                    );

                    Store?.Save();
                }
            }
            catch (Exception e)
            {
                Log.Error(
                    e,
                    $"FriendlyGridAccess error handling session state {state}."
                );
            }
        }

        public override void Dispose()
        {
            try
            {
                if (_sessionManager != null)
                {
                    _sessionManager.SessionStateChanged -=
                        SessionStateChanged;
                }

                Store?.Save();

                if (_harmony != null)
                {
                    _harmony.UnpatchAll(
                        "com.openai.friendlygridaccess"
                    );
                }

                Log.Info(
                    "FriendlyGridAccess unloaded."
                );
            }
            catch (Exception e)
            {
                Log.Error(
                    e,
                    "Error while unloading FriendlyGridAccess."
                );
            }
            finally
            {
                _sessionManager = null;
                _harmony = null;
                Instance = null;

                base.Dispose();
            }
        }
    }
}
