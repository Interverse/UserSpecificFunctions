using System;
using System.IO;
using System.Reflection;
using Terraria;
using Terraria.Localization;
using Terraria.Net;
using Terraria.GameContent.NetModules;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using UserSpecificFunctions.Database;
using UserSpecificFunctions.Extensions;
using Newtonsoft.Json;
using DiscordBridge.Chat;
using Microsoft.Xna.Framework;

namespace UserSpecificFunctions
{
	[ApiVersion(2, 1)]
	public sealed class UserSpecificFunctionsPlugin : TerrariaPlugin
	{
		private static readonly string ConfigPath = Path.Combine(TShock.SavePath, "userspecificfunctions.json");

		private CommandHandler _commandHandler;

		/// <summary>
		/// Gets the UserSpecificFunctions instance.
		/// </summary>
		public static UserSpecificFunctionsPlugin Instance { get; private set; }

		/// <summary>
		/// Gets the <see cref="Config"/> instance.
		/// </summary>
		public Config Configuration { get; private set; } = new Config();

		/// <summary>
		/// Gets the <see cref="DatabaseManager"/> instance.
		/// </summary>
		public DatabaseManager Database { get; } = new DatabaseManager();

		/// <summary>
		/// Gets the author.
		/// </summary>
		public override string Author => "Professor X";

		/// <summary>
		/// Gets the description.
		/// </summary>
		public override string Description => "";

		/// <summary>
		/// Gets the name.
		/// </summary>
		public override string Name => "User Specific Functions";

		/// <summary>
		/// Gets the version.
		/// </summary>
		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		/// <summary>
		/// Initializes a new instance of the <see cref="UserSpecificFunctionsPlugin"/> class.
		/// </summary>
		/// <param name="game">The <see cref="Main"/> instance.</param>
		public UserSpecificFunctionsPlugin(Main game) : base(game)
		{

		}

		/// <summary>
		/// Releases unmanaged and - optionally - managed resources.
		/// </summary>
		/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_commandHandler.Deregister();
				File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(Configuration, Formatting.Indented));

                ChatHandler.PlayerChatting -= OnChat;
                PlayerHooks.PlayerPostLogin -= OnPostLogin;
				PlayerHooks.PlayerPermission -= OnPermission;
			}

			base.Dispose(disposing);
		}

		/// <summary>
		/// Initializes the plugin.
		/// </summary>
		public override void Initialize()
		{
			Database.Connect();
			if (File.Exists(ConfigPath))
			{
				Configuration = JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigPath));
			}

            ChatHandler.PlayerChatting += OnChat;
            PlayerHooks.PlayerPostLogin += OnPostLogin;
			PlayerHooks.PlayerPermission += OnPermission;

			_commandHandler = new CommandHandler(this);
			_commandHandler.Register();
			Instance = this;
		}

		private static void OnChat(object sender, PlayerChattingEventArgs args)
		{
			var player = TShock.Players[args.Player.Index];
			if (player == null)
			{
				return;
			}

			if (!player.HasPermission(TShockAPI.Permissions.canchat) || player.mute)
			{
				return;
			}

			if (args.RawText.StartsWith(TShock.Config.CommandSpecifier) || args.RawText.StartsWith(TShock.Config.CommandSilentSpecifier))
			{
				return;
			}

            bool isColor = false, hasPrefix = false;
            Color chatColor;
			var playerData = player.GetData<PlayerInfo>(PlayerInfo.DataKey);
			var prefix = playerData?.ChatData.Prefix ?? player.Group.Prefix;
			var suffix = playerData?.ChatData.Suffix ?? player.Group.Suffix;
            try {
                playerData?.ChatData.Color?.ParseColor();
                isColor = true;
            } catch (Exception e) {
                isColor = false;
            }

            if (isColor) {
                chatColor = playerData?.ChatData.Color?.ParseColor() ?? player.Group.ChatColor.ParseColor();
            } else {
                chatColor = player.Group.ChatColor.ParseColor();
            }

			if (!TShock.Config.EnableChatAboveHeads)
			{
                if (prefix != "") {
                    args.Message.Prefixes.Clear();
                    args.Message.Prefix(prefix);
                }
                args.Message.Suffix(suffix);
                args.Message.Colorize(chatColor);
            }
			else
			{
				var playerName = player.TPlayer.name;
				player.TPlayer.name = string.Format(TShock.Config.ChatAboveHeadsFormat, player.Group.Name, prefix, player.Name,
					suffix);
				NetMessage.SendData((int) PacketTypes.PlayerInfo, -1, -1, NetworkText.FromLiteral(player.TPlayer.name), args.Player.Index);

				player.TPlayer.name = playerName;

				var packet = NetTextModule.SerializeServerMessage(NetworkText.FromLiteral(args.RawText), chatColor, (byte)args.Player.Index);
				NetManager.Instance.Broadcast(packet, args.Player.Index);

				NetMessage.SendData((int) PacketTypes.PlayerInfo, -1, -1, NetworkText.FromLiteral(playerName), args.Player.Index);

				var msg = string.Format("<{0}> {1}", string.Format(TShock.Config.ChatAboveHeadsFormat, player.Group.Name,
					prefix, player.Name, suffix), args.RawText);

                if (prefix != "") {
                    args.Message.Prefixes.Clear();
                    args.Message.Prefix(prefix);
                }
                args.Message.Suffix(suffix);
                args.Message.Colorize(chatColor);
            }
		}

		private void OnPostLogin(PlayerPostLoginEventArgs e)
		{
			var playerInfo = Database.Get(e.Player.User);
			if (playerInfo != null)
			{
				e.Player.SetData(PlayerInfo.DataKey, playerInfo);
			}
		}

		private static void OnPermission(PlayerPermissionEventArgs e)
		{
			if (e.Player == null || !e.Player.IsLoggedIn || !e.Player.ContainsData(PlayerInfo.DataKey))
			{
				return;
			}

			var playerInfo = e.Player.GetData<PlayerInfo>(PlayerInfo.DataKey);
			e.Handled = playerInfo.Permissions.ContainsPermission(e.Permission);
		}
	}
}
