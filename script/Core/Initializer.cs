using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using LacieEngine.API;
using LacieEngine.Settings;
using LacieEngine.UI;

namespace LacieEngine.Core
{
	public class Initializer : Node
	{
		public override void _Ready()
		{
			Log.Info("Initializing Lacie Engine...");
			System.Threading.Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
			CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
			CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
			
			// Ensure we are on the main thread for the rest of the init
			CallDeferred(nameof(Init));
		}

		public void Init()
		{
			Injector.Init();
			Log.Init();
			Log.Info("Dependency injector initialized!");
			
			string baseLocale = ProjectSettings.GetSetting("lacie_engine/core/translation_base_locale") as string;
			TranslationServer.SetLocale(baseLocale);

			// Optimization: Use ProjectSettings directly if possible, or ensure file paths are mobile-friendly
			foreach (string filename2 in GDUtil.ListFilesInPath("res://definitions/config/", ".cfg"))
			{
				Log.Debug("Processing external settings: ", filename2);
				ConfigFile configFile = new ConfigFile();
				Error err = configFile.Load(filename2);
				if (err != Error.Ok) continue;

				foreach (string section in configFile.GetSections())
				{
					foreach (string key in configFile.GetSectionKeys(section))
					{
						ProjectSettings.SetSetting(section + "/" + key, configFile.GetValue(section, key));
					}
				}
			}

			VisualServer.SetDefaultClearColor(Colors.Black);
			SettingsManager settings = new SettingsManager();
			settings.LoadSettings();

			// iOS doesn't support SetWindowTitle; we wrap it to prevent potential errors
			if (OS.GetName() != "iOS")
			{
				OS.SetWindowTitle(OS.IsDebugBuild() ? $"{settings.ProductName} v{settings.ProductVersion}" : settings.ProductName);
			}

			new Game(settings, GetTree());
			Log.Info("Initializing main screen...");
			Injector.Get<IScreenManager>().Init();
			PerformLoading();
		}

		private async void PerformLoading()
		{
			await GDUtil.DelayOneFrame();
			await Game.Screen.ShowLoadingScreenInstantly();

			// iOS/Mobile Fix: Avoid Task.Run for core engine initialization.
			// iOS (AOT) prefers running these on the main thread to avoid memory access issues.
			bool isMobile = OS.GetName() == "iOS" || OS.GetName() == "Android";

			if (isMobile)
			{
				Log.Info("Mobile platform detected: Running initialization synchronously.");
				// Run on main thread but allow the UI to breathe
				await GDUtil.DelayOneFrame();
				LoadingProc();
			}
			else
			{
				Task task = Task.Run(() => LoadingProc());
				while (!task.IsCompleted)
				{
					await GDUtil.DelayOneFrame();
				}
				if (task.IsFaulted)
				{
					Log.Exception(task.Exception, "LoadingProc failed in background task.");
				}
			}

			await Game.Screen.HideLoadingScreen();
			Log.Info(Game.Settings.ProductName, " ", Game.Settings.ProductVersion, ", Game start!");

			if (Game.Language.GetAvailableLanguages().Count > 1 && string.IsNullOrEmpty(Game.Settings.TranslationSelected))
			{
				ShowLanguageSelection();
			}
			else
			{
				ShowFirstScreen();
			}
			this.QueueFree(); // Use QueueFree instead of custom Delete() for safer memory management in Godot
		}

		private void LoadingProc()
		{
			try
			{
				Injector.Get<IPlatformInitializer>().Init();
				Game.Memory.Init();
				SystemPreload();
				
				Log.Info("Initializing inputs/state/modules...");
				Inputs.Init();
				Game.Core.InitPersistentState();
				
				foreach (var module in Injector.GetAll<IModule>())
				{
					module.Init();
				}

				Game.Language.LoadLanguage(Game.Settings.TranslationSelected);
				Game.Settings.ApplyAll();
			}
			catch (Exception exception)
			{
				Log.Exception(exception, "An error occurred while initializing the game.");
			}
		}

		private void SystemPreload()
		{
			// Optimization for iOS: Ensure we only scan res:// folders that exist
			// Mobile file systems can be more restrictive with directory scanning
			PreloadDirectory("res://resources/font/", ".tres");
			PreloadDirectory("res://resources/material/", ".tres");
			PreloadDirectory("res://assets/img/ui/", ".png");
			PreloadDirectory("res://assets/img/ui/input/", ".png");
			PreloadDirectory("res://assets/sfx/", ".ogg");
			PreloadDirectory("res://resources/animation/", ".tres");

			Game.Memory.SystemCache("res://resources/nodes/common/Player.tscn");
			Game.Memory.SystemCache("res://resources/nodes/common/PlayerSidescroller.tscn");
			Game.Memory.SystemCache("res://resources/nodes/common/StoryPlayer.tscn");
		}

		private void PreloadDirectory(string path, string suffix)
		{
			foreach (string file in GDUtil.ListFilesInPath(path, suffix))
			{
				Game.Memory.SystemCache(file);
			}
		}

		private static void ShowLanguageSelection()
		{
			Game.Settings.SetTranslationLocale(Game.Settings.TranslationBaseLocale);
			TitleLanguageMenuContainer languageSelector = GDUtil.MakeNode<TitleLanguageMenuContainer>("LanguageSelector");
			languageSelector.OnClose = () => ShowFirstScreen();
			Game.Screen.AddToLayer(IScreenManager.Layer.Screen, languageSelector);
			languageSelector.Menu.ResetSelection();
			Game.InputProcessor = Inputs.Processor.Menu;
		}

		private static void ShowFirstScreen()
		{
			if (OS.IsDebugBuild() && Game.Settings.DebugQuickstartOn)
			{
				string room = string.IsNullOrEmpty(Game.Settings.DebugQuickstartRoom) ? Game.Settings.DebugRoom : Game.Settings.DebugQuickstartRoom;
				Game.Core.StartGameFromRoom(room, Game.Settings.DebugQuickstartPoint, Vector2.Zero, null);
			}
			else
			{
				Game.Core.SwitchToScreen(Game.Settings.SceneProvider.FirstScreen);
			}
		}
	}
}
