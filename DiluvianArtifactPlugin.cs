using BepInEx;
using BepInEx.Configuration;
using RoR2.ContentManagement;
using System;
using UnityEngine;
using RoR2;
using System.Linq;
using System.Collections.Generic;

using System.Security;
using System.Security.Permissions;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace TPDespair.DiluvianArtifact
{
	[BepInPlugin(ModGuid, ModName, ModVer)]

	public class DiluvianArtifactPlugin : BaseUnityPlugin
	{
		public const string ModVer = "1.0.4";
		public const string ModName = "DiluvianArtifact";
		public const string ModGuid = "com.TPDespair.DiluvianArtifact";



		public static Dictionary<string, string> LangTokens = new Dictionary<string, string>();
		public static Dictionary<string, string> SyzygyTokens = new Dictionary<string, string>();



		public static ConfigEntry<int> DiluvifactEnable { get; set; }
		public static ConfigEntry<float> DiluvifactDifficulty { get; set; }
		public static ConfigEntry<int> UnstabifactEnable { get; set; }
		public static ConfigEntry<int> EclifactEnable { get; set; }



		public void Awake()
		{
			RoR2Application.isModded = true;
			NetworkModCompatibilityHelper.networkModList = NetworkModCompatibilityHelper.networkModList.Append(ModGuid + ":" + ModVer);

			ConfigSetup(Config);

			ContentManager.collectContentPackProviders += ContentManager_collectContentPackProviders;

			LanguageOverride();

			Diluvifact.Init();
			ZetUnstabifact.Init();
			ZetEclifact.Init();

			//On.RoR2.Networking.GameNetworkManager.OnClientConnect += (self, user, t) => { };
		}

		public void FixedUpdate()
		{
			InstabilityController.OnFixedUpdate();
		}



		private static void ConfigSetup(ConfigFile Config)
		{
			DiluvifactEnable = Config.Bind(
				"Artifacts", "diluvifactEnable", 1,
				"Artifact of Diluvian. 0 = Disabled, 1 = Artifact Available, 2 = Always Active"
			);
			DiluvifactDifficulty = Config.Bind(
				"Artifacts", "diluvifactDifficultyMult", 1.2f,
				"Artifact of Diluvian difficulty multiplier. Set to 1 or lower to disable."
			);
			UnstabifactEnable = Config.Bind(
				"Artifacts", "unstabifactEnable", 1,
				"Artifact of Instability. 0 = Disabled, 1 = Artifact Available, 2 = Always Active"
			);
			EclifactEnable = Config.Bind(
				"Artifacts", "eclifactEnable", 1,
				"Artifact of the Eclipse. 0 = Disabled, 1 = Artifact Available, 2 = Always Active"
			);
		}

		private void ContentManager_collectContentPackProviders(ContentManager.AddContentPackProviderDelegate addContentPackProvider)
		{
			addContentPackProvider(new DiluvianArtifactContent());
		}

		public static Sprite CreateSprite(byte[] resourceBytes, Color fallbackColor)
		{
			// Create a temporary texture, then load the texture onto it.
			var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
			try
			{
				if (resourceBytes == null)
				{
					FillTexture(tex, fallbackColor);
				}
				else
				{
					tex.LoadImage(resourceBytes, false);
					tex.Apply();
					CleanAlpha(tex);
				}
			}
			catch (Exception e)
			{
				Debug.LogError(e.ToString());
				FillTexture(tex, fallbackColor);
			}

			return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(31, 31));
		}

		public static Texture2D FillTexture(Texture2D tex, Color color)
		{
			var pixels = tex.GetPixels();
			for (var i = 0; i < pixels.Length; ++i)
			{
				pixels[i] = color;
			}

			tex.SetPixels(pixels);
			tex.Apply();

			return tex;
		}

		public static Texture2D CleanAlpha(Texture2D tex)
		{
			var pixels = tex.GetPixels();
			for (var i = 0; i < pixels.Length; ++i)
			{
				if (pixels[i].a < 0.05f)
				{
					pixels[i] = Color.clear;
				}
			}

			tex.SetPixels(pixels);
			tex.Apply();

			return tex;
		}

		private static void LanguageOverride()
		{
			On.RoR2.Language.TokenIsRegistered += (orig, self, token) =>
			{
				if (token != null)
				{
					if (TriggerSyzygyText())
					{
						if (SyzygyTokens.ContainsKey(token)) return true;
					}

					if (LangTokens.ContainsKey(token)) return true;
				}

				return orig(self, token);
			};

			On.RoR2.Language.GetString_string += (orig, token) =>
			{
				if (token != null)
				{
					if (TriggerSyzygyText())
					{
						if (SyzygyTokens.ContainsKey(token)) return SyzygyTokens[token];
					}
					if (Run.instance && Diluvifact.Enabled)
					{
						if (token == "ITEM_BEAR_DESC") return "<style=cIsHealing>15%</style> <style=cStack>(+15% per stack)</style> chance to <style=cIsHealing>block</style> incoming damage. <style=cDeath>Unlucky</style>.";
					}

					if (LangTokens.ContainsKey(token)) return LangTokens[token];
				}

				return orig(token);
			};
		}

		private static bool TriggerSyzygyText()
		{
			if (Run.instance)
			{
				if (Diluvifact.Enabled)
				{
					if (Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse8) return true;
					if (ZetEclifact.Enabled) return true;
				}
			}

			return false;
		}

		public static void RegisterLanguageToken(string token, string text)
		{
			if (!LangTokens.ContainsKey(token)) LangTokens.Add(token, text);
			else LangTokens[token] = text;
		}

		public static void RegisterSyzygyToken(string token, string text)
		{
			if (!SyzygyTokens.ContainsKey(token)) SyzygyTokens.Add(token, text);
			else SyzygyTokens[token] = text;
		}
	}
}
