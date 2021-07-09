using RoR2;
using System;
using UnityEngine;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace TPDespair.DiluvianArtifact
{
	public static class ZetUnstabifact
	{
		internal static void Init()
		{
			DiluvianArtifactPlugin.RegisterLanguageToken("ARTIFACT_ZETUNSTABIFACT_NAME", "Artifact of Instability");
			DiluvianArtifactPlugin.RegisterLanguageToken("ARTIFACT_ZETUNSTABIFACT_DESC", "Spending too long in a stage causes an endless stream of meteors to rain down from the sky.");

			SceneDirector.onPostPopulateSceneServer += ResetInstabilityController;
			Stage.onServerStageComplete += DisableInstabilityController;
			Run.onRunDestroyGlobal += DisableInstabilityController;
			Stage.onStageStartGlobal += ClearLists;

			MeteorImpactHook();
			EffectManagerNetworkingHook();
			BrotherHauntHook();
		}



		private static void ResetInstabilityController(SceneDirector sceneDirector) { InstabilityController.Reset(); }
		private static void DisableInstabilityController(Stage stage) { InstabilityController.Disable(); }
		private static void DisableInstabilityController(Run run) { InstabilityController.Disable(); }
		private static void ClearLists(Stage stage) 
		{
			InstabilityController.clearBlockerList = true;
			InstabilityController.NovaState.clearNovaList = true;
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
					InstabilityController.NovaState.CreateNova(data.origin);

					return;
				}

				orig(index, data, transmit);
			};
		}

		private static void BrotherHauntHook()
		{
			On.EntityStates.BrotherHaunt.FireRandomProjectiles.OnEnter += (orig, self) =>
			{
				orig(self);

				InstabilityController.MoonActivation();
			};
		}
	}
}
