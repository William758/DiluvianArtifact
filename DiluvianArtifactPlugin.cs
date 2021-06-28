using BepInEx;
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
		public const string ModVer = "1.0.0";
		public const string ModName = "DiluvianArtifact";
		public const string ModGuid = "com.TPDespair.DiluvianArtifact";



		public static Dictionary<string, string> LangTokens = new Dictionary<string, string>();
		public static Dictionary<string, string> SyzygyTokens = new Dictionary<string, string>();



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



		public void Awake()
		{
			RoR2Application.isModded = true;
			NetworkModCompatibilityHelper.networkModList = NetworkModCompatibilityHelper.networkModList.Append(ModGuid + ":" + ModVer);

			ContentManager.collectContentPackProviders += ContentManager_collectContentPackProviders;

			LanguageOverride();

			Diluvifact.Init();
			ZetEclifact.Init();
			ZetUnstabifact.Init();

			//On.RoR2.Networking.GameNetworkManager.OnClientConnect += (self, user, t) => { };
		}

		public void FixedUpdate()
		{
			ZetUnstabifact.OnFixedUpdate();
		}



		private void ContentManager_collectContentPackProviders(ContentManager.AddContentPackProviderDelegate addContentPackProvider)
		{
			addContentPackProvider(new DiluvianArtifactContent());
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
					if (Run.instance && RunArtifactManager.instance.IsArtifactEnabled(DiluvianArtifactContent.Artifacts.Diluvifact)) {
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
				if (RunArtifactManager.instance.IsArtifactEnabled(DiluvianArtifactContent.Artifacts.Diluvifact))
				{
					if (Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse8) return true;
					if (RunArtifactManager.instance.IsArtifactEnabled(DiluvianArtifactContent.Artifacts.ZetEclifact)) return true;
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
