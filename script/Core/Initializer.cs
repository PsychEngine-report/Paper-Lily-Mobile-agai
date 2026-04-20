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
			CallDeferred("Init");
		}

		public void Init()
		{
			Injector.Init();
			Log.Init();
			Log.Info("Dependency injector initialized!");
			TranslationServer.SetLocale(ProjectSettings.GetSetting("lacie_engine/core/translation_base_locale") as string);


			foreach (string filename2 in GDUtil.ListFilesInPath("res://definitions/config/", ".cfg"))
			{
				Log.Debug("Processing external settings: ", filename2);
				ConfigFile configFile = new ConfigFile();
				configFile.Load(filename2);
				string[] sections = configFile.GetSections();
				foreach (string section in sections)
				{
					string[] sectionKeys = configFile.GetSectionKeys(section);
					foreach (string key in sectionKeys)
					{
						ProjectSettings.SetSetting(section + "/" + key, configFile.GetValue(section, key));
					}
				}
			}
			VisualServer.SetDefaultClearColor(Colors.Black);
			SettingsManager settings = new SettingsManager();
			settings.LoadSettings();
			if (OS.IsDebugBuild())
			{
				OS.SetWindowTitle(settings.ProductName + " v" + settings.ProductVersion);
			}
			else
			{
				OS.SetWindowTitle(settings.ProductName);
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
			#if IOS
    		Log.Info("iOS detected: Running initialization on main thread.");
    		LoadingProc();
			#else
    		Task task = Task.Run(delegate
    		{
        	LoadingProc();
    		});
			while (!task.IsCompleted)
			{
				await GDUtil.DelayOneFrame();
			}
			#endif
			await Game.Screen.HideLoadingScreen();
			Log.Info(Game.Settings.ProductName, " ", Game.Settings.ProductVersion, ", Game start!");
			if (Game.Language.GetAvailableLanguages().Count > 1 && Game.Settings.TranslationSelected.IsNullOrEmpty())
			{
				ShowLanguageSelection();
			}
			else
			{
				ShowFirstScreen();
			}
			this.Delete();
		}

		private void LoadingProc()
		{
			try
			{
				Injector.Get<IPlatformInitializer>().Init();
				Game.Memory.Init();
				SystemPreload();
				Log.Info("Initializing the inputs...");
				Inputs.Init();
				Log.Info("Initializing the Persistent State...");
				Game.Core.InitPersistentState();
				Log.Info("Initializing modules...");
				Injector.GetAll<IModule>().ForEach(delegate(IModule module)
				{
					module.Init();
				});
				Log.Info("Modules initialized!");
				Log.Info("Initializing language...");
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
			Log.Info("Loading fonts...");
			foreach (string filename in GDUtil.ListFilesInPath("res://resources/font/", ".tres"))
			{
				Game.Memory.SystemCache(filename);
			}
			Log.Info("Loading materials...");
			foreach (string filename2 in GDUtil.ListFilesInPath("res://resources/material/", ".tres"))
			{
				Game.Memory.SystemCache(filename2);
			}
			Log.Info("Loading graphics...");
			foreach (string filename3 in GDUtil.ListFilesInPath("res://assets/img/ui/", ".png"))
			{
				Game.Memory.SystemCache(filename3);
			}
			foreach (string filename4 in GDUtil.ListFilesInPath("res://assets/img/ui/input/", ".png"))
			{
				Game.Memory.SystemCache(filename4);
			}
			Log.Info("Loading sounds...");
			foreach (string filename5 in GDUtil.ListFilesInPath("res://assets/sfx/", "ui_", ".ogg"))
			{
				Game.Memory.SystemCache(filename5);
			}
			Log.Info("Loading animations...");
			foreach (string filename6 in GDUtil.ListFilesInPath("res://resources/animation/", ".tres"))
			{
				Game.Memory.SystemCache(filename6);
			}
			Log.Info("Loading system nodes...");
			Game.Memory.SystemCache("res://resources/nodes/common/Player.tscn");
			Game.Memory.SystemCache("res://resources/nodes/common/PlayerSidescroller.tscn");
			Game.Memory.SystemCache("res://resources/nodes/common/StoryPlayer.tscn");
		}

		private static void ShowLanguageSelection()
		{
			Game.Settings.SetTranslationLocale(Game.Settings.TranslationBaseLocale);
			TitleLanguageMenuContainer languageSelector = GDUtil.MakeNode<TitleLanguageMenuContainer>("LanguageSelector");
			languageSelector.OnClose = delegate
			{
				ShowFirstScreen();
			};
			Game.Screen.AddToLayer(IScreenManager.Layer.Screen, languageSelector);
			languageSelector.Menu.ResetSelection();
			Game.InputProcessor = Inputs.Processor.Menu;
		}

		private static void ShowFirstScreen()
		{
			if (OS.IsDebugBuild() && Game.Settings.DebugQuickstartOn)
			{
				Game.Core.StartGameFromRoom(Game.Settings.DebugQuickstartRoom.IsNullOrEmpty() ? Game.Settings.DebugRoom : Game.Settings.DebugQuickstartRoom, Game.Settings.DebugQuickstartPoint.IsNullOrEmpty() ? null : Game.Settings.DebugQuickstartPoint, Vector2.Zero, null);
			}
			else
			{
				Game.Core.SwitchToScreen(Game.Settings.SceneProvider.FirstScreen);
			}
		}
	}
}
