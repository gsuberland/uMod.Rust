using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using uMod.Libraries;
using uMod.Libraries.Covalence;
using uMod.Logging;
using uMod.Plugins;

namespace uMod.Rust
{
    /// <summary>
    /// The core Rust plugin
    /// </summary>
    public partial class Rust : CSPlugin
    {
        #region Initialization

        /// <summary>
        /// Initializes a new instance of the Rust class
        /// </summary>
        public Rust()
        {
            // Set plugin info attributes
            Title = "Rust";
            Author = RustExtension.AssemblyAuthors;
            Version = RustExtension.AssemblyVersion;
        }

        // Libraries
        internal readonly Lang lang = Interface.uMod.GetLibrary<Lang>();
        internal readonly Permission permission = Interface.uMod.GetLibrary<Permission>();

        // Instances
        internal static readonly RustProvider Covalence = RustProvider.Instance;
        internal readonly PluginManager pluginManager = Interface.uMod.RootPluginManager;
        internal readonly IServer Server = Covalence.CreateServer();

        internal bool serverInitialized;

        /// <summary>
        /// Checks if the permission system has loaded, shows an error if it failed to load
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        private bool PermissionsLoaded(IPlayer player)
        {
            if (!permission.IsLoaded)
            {
                player.Reply(string.Format(lang.GetMessage("PermissionsNotLoaded", this, player.Id), permission.LastException.Message));
                return false;
            }

            return true;
        }

        #endregion Initialization

        #region Core Hooks

        /// <summary>
        /// Called when the plugin is initializing
        /// </summary>
        [HookMethod("Init")]
        private void Init()
        {
            // Configure remote error logging
            RemoteLogger.SetTag("game", Title.ToLower());
            RemoteLogger.SetTag("game version", Server.Version);

            // Set up default permission groups
            if (permission.IsLoaded)
            {
                int rank = 0;
                foreach (string defaultGroup in Interface.uMod.Config.Options.DefaultGroups)
                {
                    if (!permission.GroupExists(defaultGroup))
                    {
                        permission.CreateGroup(defaultGroup, defaultGroup, rank++);
                    }
                }

                permission.RegisterValidate(s =>
                {
                    if (ulong.TryParse(s, out ulong temp))
                    {
                        int digits = temp == 0 ? 1 : (int)Math.Floor(Math.Log10(temp) + 1);
                        return digits >= 17;
                    }

                    return false;
                });

                permission.CleanUp();
            }
        }

        /// <summary>
        /// Called when another plugin has been loaded
        /// </summary>
        /// <param name="plugin"></param>
        [HookMethod("OnPluginLoaded")]
        private void OnPluginLoaded(Plugin plugin)
        {
            if (serverInitialized)
            {
                // Call OnServerInitialized for hotloaded plugins
                plugin.CallHook("OnServerInitialized", false);
            }
        }

        /// <summary>
        /// Called when the server is first initialized
        /// </summary>
        [HookMethod("IOnServerInitialized")]
        private void IOnServerInitialized()
        {
            if (!serverInitialized)
            {
                if (Interface.uMod.CheckConsole() && global::ServerConsole.Instance != null)
                {
                    global::ServerConsole.Instance.enabled = false;
                    UnityEngine.Object.Destroy(global::ServerConsole.Instance);
                    typeof(SingletonComponent<global::ServerConsole>).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, null);
                }

                Analytics.Collect();
                RustExtension.ServerConsole();

                if (!Interface.uMod.Config.Options.Modded)
                {
                    Interface.uMod.LogWarning("The server is currently listed under Community. Please be aware that Facepunch only allows admin tools" +
                        "(that do not affect gameplay or make the server appear modded) under the Community section");
                }

                serverInitialized = true;

                // Let plugins know server startup is complete
                Interface.CallHook("OnServerInitialized", serverInitialized);
            }
        }

        /// <summary>
        /// Called when the server is saved
        /// </summary>
        [HookMethod("OnServerSave")]
        private void OnServerSave()
        {
            Interface.uMod.OnSave();
            Covalence.PlayerManager.SavePlayerData();
        }

        /// <summary>
        /// Called when the server is shutting down
        /// </summary>
        [HookMethod("OnServerShutdown")]
        private void OnServerShutdown()
        {
            Interface.uMod.OnShutdown();
            Covalence.PlayerManager.SavePlayerData();
        }

        #endregion Core Hooks

        #region Command Handling

        /// <summary>
        /// Parses the specified command
        /// </summary>
        /// <param name="argstr"></param>
        /// <param name="command"></param>
        /// <param name="args"></param>
        private void ParseCommand(string argstr, out string command, out string[] args)
        {
            List<string> arglist = new List<string>();
            StringBuilder sb = new StringBuilder();
            bool inlongarg = false;

            foreach (char c in argstr)
            {
                if (c == '"')
                {
                    if (inlongarg)
                    {
                        string arg = sb.ToString().Trim();
                        if (!string.IsNullOrEmpty(arg))
                        {
                            arglist.Add(arg);
                        }

                        sb.Clear();
                        inlongarg = false;
                    }
                    else
                    {
                        inlongarg = true;
                    }
                }
                else if (char.IsWhiteSpace(c) && !inlongarg)
                {
                    string arg = sb.ToString().Trim();
                    if (!string.IsNullOrEmpty(arg))
                    {
                        arglist.Add(arg);
                    }

                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            if (sb.Length > 0)
            {
                string arg = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(arg))
                {
                    arglist.Add(arg);
                }
            }

            if (arglist.Count == 0)
            {
                command = null;
                args = null;
                return;
            }

            command = arglist[0];
            arglist.RemoveAt(0);
            args = arglist.ToArray();
        }

        #endregion Command Handling
    }
}
