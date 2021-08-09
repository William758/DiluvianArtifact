using RoR2;
using System;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace TPDespair.DiluvianArtifact
{
	public static class ZetUnstabifact
	{
		private static bool customHaunt = false;



		internal static void Init()
		{
			DiluvianArtifactPlugin.RegisterLanguageToken("ARTIFACT_ZETUNSTABIFACT_NAME", "Artifact of Instability");
			DiluvianArtifactPlugin.RegisterLanguageToken("ARTIFACT_ZETUNSTABIFACT_DESC", "Spending too long in a stage causes an endless stream of meteors to rain down from the sky.");

			// server only
			SceneDirector.onPostPopulateSceneServer += ResetInstabilityController;
			Stage.onServerStageComplete += DisableInstabilityController;

			// client and server
			Run.onRunDestroyGlobal += OnRunDestroyed;
			Stage.onStageStartGlobal += OnStageStarted;

			MeteorImpactHook();
			EffectManagerNetworkingHook();

			BrotherHauntEnterHook();
			BrotherHauntUpdateHook();

			HUDAwakeHook();
			HUDUpdateHook();
		}



		private static void ResetInstabilityController(SceneDirector sceneDirector)
		{
			InstabilityController.CountdownDisplay.ServerSendSyncTime(0f, true);
			InstabilityController.Reset();
			customHaunt = false;
		}
		private static void DisableInstabilityController(Stage stage)
		{
			InstabilityController.CountdownDisplay.ServerSendSyncTime(0f, true);
			InstabilityController.Disable();
			customHaunt = false;
		}

		private static void OnRunDestroyed(Run run)
		{
			InstabilityController.CountdownDisplay.SetSyncTime(0f);
			InstabilityController.Disable();
			customHaunt = false;
		}
		private static void OnStageStarted(Stage stage) 
		{
			InstabilityController.CountdownDisplay.SetSyncTime(0f);
			InstabilityController.MarkListsForClearing();
			customHaunt = false;
		}



		private static void MeteorImpactHook()
		{
			IL.RoR2.MeteorStormController.DetonateMeteor += (il) =>
			{
				ILCursor c = new ILCursor(il);

				c.GotoNext(
					x => x.MatchDup(),
					x => x.MatchLdarg(1)
				);

				c.Index += 3;

				c.Emit(OpCodes.Dup);
				c.EmitDelegate<Action<Vector3>>((position) =>
				{
					InstabilityController.OnMeteorImpact(position);
				});
			};
		}

		private static void EffectManagerNetworkingHook()
		{
			On.RoR2.EffectManager.SpawnEffect_EffectIndex_EffectData_bool += (orig, index, data, transmit) =>
			{
				if ((int)index == 1758000 && !transmit)
				{
					if (data.genericUInt == 1u)
					{
						InstabilityController.NovaState.CreateNova(data.origin);
					}
					else if (data.genericUInt == 2u)
					{
						InstabilityController.CountdownDisplay.SetSyncTime(data.genericFloat);
					}
					else
					{
						Debug.LogWarning("Artifact of Instability - Unknown SpawnEffect : " + data.genericUInt);
					}

					return;
				}

				orig(index, data, transmit);
			};
		}

		private static void BrotherHauntEnterHook()
		{
			On.EntityStates.BrotherHaunt.FireRandomProjectiles.OnEnter += (orig, self) =>
			{
				orig(self);

				customHaunt = Run.instance && RunArtifactManager.instance.IsArtifactEnabled(DiluvianArtifactContent.Artifacts.ZetUnstabifact);

				if (customHaunt) InstabilityController.MoonActivation();
			};
		}

		private static void BrotherHauntUpdateHook()
		{
			On.EntityStates.BrotherHaunt.FireRandomProjectiles.FixedUpdate += (orig, self) =>
			{
				if (customHaunt) self.chargeTimer = 1f;

				orig(self);
			};
		}

		private static void HUDAwakeHook()
		{
			On.RoR2.UI.HUD.Awake += (orig, self) =>
			{
				orig(self);

				InstabilityController.CountdownDisplay.InitializeUI(self);
			};
		}

		private static void HUDUpdateHook()
		{
			On.RoR2.UI.HUD.Update += (orig, self) =>
			{
				orig(self);

				InstabilityController.CountdownDisplay.UpdateUI();
			};
		}
	}
}
