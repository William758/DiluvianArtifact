using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using RoR2.Projectile;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace TPDespair.DiluvianArtifact
{
	public static class ZetUnstabifact
	{
		public static bool armed = false;
		public static bool meteorActivated = false;
		public static bool scorchActivated = false;
		public static bool novaActivated = false;

		public static bool scenePopulated = false;

		public static float timer = 99999f;
		public static float stopwatch = -99999f;
		public static float meteorActivation = 360f;
		public static float scorchActivation = 450f;
		public static float novaActivation = 540f;

		public static float baseDamage = 20f;

		public static bool cachedEffectData = false;
		public static GameObject projectilePrefab;
		public static GameObject deathExplosionEffect;
		public static GameObject chargingEffectPrefab;
		public static GameObject areaIndicatorPrefab;

		private static List<Nova> activeNovas = new List<Nova>();
		public static bool clearNovaList = false;

		public class Nova
		{
			public float stopwatch = 0f;

			public bool charge = false;
			public bool fired = false;

			public Vector3 position;
			public Quaternion rotation = Quaternion.identity;

			public GameObject chargeVfxInstance;
			public GameObject areaIndicatorVfxInstance;

			public void Update()
			{
				if (fired) return;

				stopwatch += Time.fixedDeltaTime;

				bool armedAndActive = armed && novaActivated;

				if (clearNovaList || stopwatch >= 7f)
				{
					if (chargeVfxInstance)
					{
						UnityEngine.Object.Destroy(chargeVfxInstance);
						chargeVfxInstance = null;
					}
					if (areaIndicatorVfxInstance)
					{
						UnityEngine.Object.Destroy(areaIndicatorVfxInstance);
						areaIndicatorVfxInstance = null;
					}

					fired = true;

					if (NetworkServer.active && armedAndActive && !clearNovaList) FireNovaBlast(position);

					return;
				}

				if (stopwatch >= 5f && !charge)
				{
					if (!chargeVfxInstance)
					{
						chargeVfxInstance = UnityEngine.Object.Instantiate(chargingEffectPrefab, position, rotation);
						chargeVfxInstance.transform.localScale = Vector3.one * 0.125f;
					}
					if (!areaIndicatorVfxInstance)
					{
						areaIndicatorVfxInstance = UnityEngine.Object.Instantiate(areaIndicatorPrefab, position, rotation);
						ObjectScaleCurve component = areaIndicatorVfxInstance.GetComponent<ObjectScaleCurve>();
						component.timeMax = 2f;
						component.baseScale = Vector3.one * 25f;
						areaIndicatorVfxInstance.GetComponent<AnimateShaderAlpha>().timeMax = 2f;
					}

					charge = true;
				}
			}
		}



		internal static void Init()
		{
			DiluvianArtifactPlugin.RegisterLanguageToken("ARTIFACT_ZETUNSTABIFACT_NAME", "Artifact of Instability");
			DiluvianArtifactPlugin.RegisterLanguageToken("ARTIFACT_ZETUNSTABIFACT_DESC", "Spending too long in a stage causes an endless stream of meteors to rain down from the sky.");

			SceneDirector.onPostPopulateSceneServer += SetScenePopulated;
			Stage.onServerStageComplete += ResetState;
			Run.onRunDestroyGlobal += ResetState;
			Stage.onStageStartGlobal += ClearNovaList;

			MeteorImpactHook();
			EffectManagerNetworkingHook();
		}

		internal static void OnFixedUpdate()
		{
			if (!Run.instance) return;

			CacheEffectData();
			UpdateTimer();
			UpdateActiveNovas();
		}



		private static void SetScenePopulated(SceneDirector sceneDirector)
		{
			armed = false;

			scenePopulated = true;

			timer = 3f;
			stopwatch = -99999f;
		}

		private static void ResetState(Stage stage)
		{
			armed = false;

			timer = 99999f;
			stopwatch = -99999f;
		}

		private static void ResetState(Run run)
		{
			armed = false;

			timer = 99999f;
			stopwatch = -99999f;
		}

		private static void ClearNovaList(Stage stage)
		{
			clearNovaList = true;
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
					if (scorchActivated) SpawnScorch(position);
					if (novaActivated) EffectManager.SpawnEffect((EffectIndex)1758000, new EffectData { origin = position }, true);
				});
			};
		}

		private static void EffectManagerNetworkingHook()
		{
			On.RoR2.EffectManager.SpawnEffect_EffectIndex_EffectData_bool += (orig, index, data, transmit) =>
			{
				if ((int)index == 1758000 && !transmit)
				{
					activeNovas.Add(new Nova { position = data.origin });

					return;
				}

				orig(index, data, transmit);
			};
		}



		internal static void CacheEffectData()
		{
			if (!cachedEffectData)
			{
				cachedEffectData = true;

				projectilePrefab = EntityStates.LunarExploderMonster.DeathState.projectilePrefab;
				deathExplosionEffect = EntityStates.LunarExploderMonster.DeathState.deathExplosionEffect;

				chargingEffectPrefab = EntityStates.VagrantMonster.ChargeMegaNova.chargingEffectPrefab;
				areaIndicatorPrefab = EntityStates.VagrantMonster.ChargeMegaNova.areaIndicatorPrefab;
			}
		}

		internal static void UpdateTimer()
		{
			if (!NetworkServer.active) return;

			if (!Run.instance.isRunStopwatchPaused)
			{
				timer -= Time.fixedDeltaTime;
				stopwatch += Time.fixedDeltaTime;
			}

			if (timer < 0f)
			{
				timer = 0.25f;
				Tick();
			}
		}

		private static void Tick()
		{
			if (scenePopulated)
			{
				armed = false;
				meteorActivated = false;
				scorchActivated = false;
				novaActivated = false;

				scenePopulated = false;

				stopwatch = 0f;
				meteorActivation = 120 + 240 * Mathf.Pow(0.9f, Run.instance.loopClearCount);
				//meteorActivation *= 0.01f;
				scorchActivation = meteorActivation * 1.25f;
				novaActivation = meteorActivation * 1.5f;

				if (RunArtifactManager.instance.IsArtifactEnabled(DiluvianArtifactContent.Artifacts.ZetUnstabifact))
				{
					if (TeleporterInteraction.instance)
					{
						armed = true;
						Debug.LogWarning("Artifact of Instability - Timer set to : " + meteorActivation);
					}
					else
					{
						Debug.LogWarning("Artifact of Instability - TeleporterInteraction instance not found!");
					}
				}
			}

			//Debug.LogWarning(armed + " - " + stopwatch + " / " + activation);

			if (armed)
			{
				if (!meteorActivated && stopwatch > meteorActivation)
				{
					meteorActivated = true;

					baseDamage = 20f * Mathf.Sqrt(Run.instance.difficultyCoefficient);
					Debug.LogWarning("Artifact of Instability - BaseDamage : " + baseDamage);

					StartMeteorStorm();
				}
				if (!scorchActivated && stopwatch > scorchActivation)
				{
					scorchActivated = true;
				}
				if (!novaActivated && stopwatch > novaActivation)
				{
					novaActivated = true;
				}
			}
		}

		internal static void UpdateActiveNovas()
		{
			for (int i = 0; i < activeNovas.Count; i++)
			{
				Nova nova = activeNovas[i];

				if (nova != null)
				{
					nova.Update();

					if (nova.fired)
					{
						activeNovas.RemoveAt(i);
						i--;
						continue;
					}
				}
			}

			clearNovaList = false;
		}





		private static void StartMeteorStorm()
		{
			MeteorStormController component = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Prefabs/NetworkedObjects/MeteorStorm"), Vector3.zero, Quaternion.identity).GetComponent<MeteorStormController>();
			component.ownerDamage = baseDamage;
			component.waveCount = 9999999;
			NetworkServer.Spawn(component.gameObject);
		}

		private static void SpawnScorch(Vector3 position)
		{
			Vector3 forward = Quaternion.identity * Util.ApplySpread(Vector3.forward, 0f, 0f, 1f, 1f, 0, 0f);
			FireProjectileInfo fireProjectileInfo = default(FireProjectileInfo);
			fireProjectileInfo.projectilePrefab = projectilePrefab;
			fireProjectileInfo.position = position;
			fireProjectileInfo.rotation = Util.QuaternionSafeLookRotation(forward);
			fireProjectileInfo.owner = null;
			fireProjectileInfo.damage = baseDamage;
			fireProjectileInfo.crit = false;
			ProjectileManager.instance.FireProjectile(fireProjectileInfo);

			if (deathExplosionEffect) EffectManager.SpawnEffect(deathExplosionEffect, new EffectData { origin = position, scale = 12.5f }, true);
		}

		private static void FireNovaBlast(Vector3 position)
		{
			new BlastAttack
			{
				attacker = null,
				baseDamage = baseDamage * 4f,
				baseForce = 0f,
				bonusForce = Vector3.zero,
				attackerFiltering = AttackerFiltering.NeverHit,
				crit = false,
				damageColorIndex = DamageColorIndex.Default,
				damageType = DamageType.Generic,
				falloffModel = BlastAttack.FalloffModel.None,
				inflictor = null,
				position = position,
				procChainMask = default(ProcChainMask),
				procCoefficient = 0f,
				radius = 12.5f,
				losType = BlastAttack.LoSType.NearestHit,
				teamIndex = TeamIndex.None
			}.Fire();
		}
	}
}
