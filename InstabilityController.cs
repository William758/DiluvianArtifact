using RoR2;
using RoR2.Navigation;
using RoR2.Projectile;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Networking;

namespace TPDespair.DiluvianArtifact
{
	public static class InstabilityController
	{
		public static bool enabled = false;
		public static bool reset = false;

		public static float timer = 99999f;
		public static float stopwatch = -99999f;

		public static float baseDamage = 24f;
		public static float baseTime = 360f;



		private static List<Blocker> activeBlocker = new List<Blocker>();
		internal static bool clearBlockerList = false;

		private class Blocker
		{
			internal float timer = 5f;
			internal Vector3 position;
		}

		private static void BlockerFixedUpdate()
		{
			if (!NetworkServer.active) return;

			for (int i = 0; i < activeBlocker.Count; i++)
			{
				Blocker blocker = activeBlocker[i];

				if (blocker != null)
				{
					blocker.timer -= Time.fixedDeltaTime;

					if (blocker.timer <= 0f || clearBlockerList)
					{
						activeBlocker.RemoveAt(i);
						i--;
						continue;
					}
				}
			}

			clearBlockerList = false;
		}

		internal static void OnMeteorImpact(Vector3 position)
		{
			if (ScorchState.activated || NovaState.activated)
			{
				if (activeBlocker.Count < 40 && PlayerWithinRange(position, 120f) && SpaceAvailable(position, 15f))
				{
					activeBlocker.Add(new Blocker { position = position });

					if (ScorchState.activated) ScorchState.SpawnScorch(position);
					if (NovaState.activated) NovaState.ServerSendNovaEffect(position);
				}
			}
		}

		private static bool SpaceAvailable(Vector3 position, float radius)
		{
			float sqrRad = radius * radius;

			for (int i = 0; i < activeBlocker.Count; i++)
			{
				Blocker blocker = activeBlocker[i];

				if (blocker != null)
				{
					float dist = (position - blocker.position).sqrMagnitude;
					if (dist <= sqrRad) return false;
				}
			}

			return true;
		}

		private static bool PlayerWithinRange(Vector3 position, float radius)
		{
			float sqrRad = radius * radius;

			ReadOnlyCollection<TeamComponent> teamMembers = TeamComponent.GetTeamMembers(TeamIndex.Player);
			for (int i = 0; i < teamMembers.Count; i++)
			{
				CharacterBody body = teamMembers[i].body;
				if (body && body.isPlayerControlled)
				{
					float dist = (position - body.corePosition).sqrMagnitude;
					if (dist <= sqrRad) return true;
				}
			}

			return false;
		}



		internal static void MoonActivation()
		{
			if (Run.instance && !enabled && RunArtifactManager.instance.IsArtifactEnabled(DiluvianArtifactContent.Artifacts.ZetUnstabifact))
			{
				reset = false;
				timer = 3f;
				stopwatch = 1200f;
				SetupValues();
				FissureState.activation = 99999f;
				enabled = true;
				Debug.LogWarning("Artifact of Instability - BaseDamage : " + baseDamage);
			}
		}

		internal static void Reset()
		{
			enabled = false;
			reset = true;
			timer = 3f;
			stopwatch = -99999f;

			MeteorState.activated = false;
			FissureState.activated = false;
			ScorchState.activated = false;
			NovaState.activated = false;
		}

		internal static void Disable()
		{
			enabled = false;
			reset = false;
			timer = 99999f;
			stopwatch = -99999f;
		}



		private static void SetupValues()
		{
			baseDamage = 24f * Mathf.Pow(Run.instance.difficultyCoefficient, 0.75f);
			//baseDamage *= 0.01f;
			baseTime = 120f + 240f * Mathf.Pow(0.9f, Run.instance.loopClearCount);
			//baseTime *= 0.01f;
			MeteorState.activation = baseTime;
			FissureState.activation = baseTime * 1.166667f;
			ScorchState.activation = baseTime * 1.333333f;
			NovaState.activation = baseTime * 1.5f;
		}



		internal static void OnFixedUpdate()
		{
			if (!Run.instance) return;

			CacheEffectData();

			ServerUpdateTimer();

			BlockerFixedUpdate();
			FissureState.FixedUpdate();
			NovaState.FixedUpdate();
		}

		private static void ServerUpdateTimer()
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

				ResetState();
				AdvanceState();
			}
		}

		private static void ResetState()
		{
			if (!reset) return;

			reset = false;

			stopwatch = 0f;
			SetupValues();

			if (RunArtifactManager.instance.IsArtifactEnabled(DiluvianArtifactContent.Artifacts.ZetUnstabifact))
			{
				if (TeleporterInteraction.instance)
				{
					enabled = true;
					Debug.LogWarning("Artifact of Instability - BaseTimer : " + baseTime + " , BaseDamage : " + baseDamage);
				}
				else
				{
					Debug.LogWarning("Artifact of Instability - TeleporterInteraction instance not found!");
				}
			}
		}

		private static void AdvanceState()
		{
			if (!enabled) return;

			if (!MeteorState.activated && stopwatch > MeteorState.activation)
			{
				MeteorState.activated = true;
				MeteorState.StartStorm();
			}
			if (!FissureState.activated && stopwatch > FissureState.activation)
			{
				FissureState.activated = true;
				FissureState.ResetCharges();
			}
			if (!ScorchState.activated && stopwatch > ScorchState.activation)
			{
				ScorchState.activated = true;
			}
			if (!NovaState.activated && stopwatch > NovaState.activation)
			{
				NovaState.activated = true;
			}
		}



		public static class MeteorState
		{
			public static bool activated = false;
			public static float activation = 360f;

			internal static void StartStorm()
			{
				MeteorStormController component = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Prefabs/NetworkedObjects/MeteorStorm"), Vector3.zero, Quaternion.identity).GetComponent<MeteorStormController>();
				component.ownerDamage = baseDamage;
				component.waveCount = 9999999;
				NetworkServer.Spawn(component.gameObject);
			}
		}

		public static class FissureState
		{
			public static bool activated = false;
			public static float activation = 420f;

			private static int charges = 0;
			private static float chargeTimer = 0f;

			internal static void ResetCharges()
			{
				charges = 0;
				chargeTimer = 0f;
			}

			internal static void FixedUpdate()
			{
				if (NetworkServer.active)
				{
					if (!enabled || !activated) return;

					chargeTimer -= Time.fixedDeltaTime;
					if (chargeTimer <= 0f)
					{
						chargeTimer += 0.125f;
						charges = Mathf.Min(charges + 1, 40);
					}
					if (UnityEngine.Random.value < 0.1f && charges > 0)
					{
						FireFissure();
					}
				}
			}

			private static void FireFissure()
			{
				NodeGraph groundNodes = SceneInfo.instance.groundNodes;
				if (groundNodes)
				{
					List<NodeGraph.NodeIndex> activeNodesForHullMaskWithFlagConditions = groundNodes.GetActiveNodesForHullMaskWithFlagConditions(HullMask.Golem, NodeFlags.None, NodeFlags.NoCharacterSpawn);
					NodeGraph.NodeIndex nodeIndex = activeNodesForHullMaskWithFlagConditions[UnityEngine.Random.Range(0, activeNodesForHullMaskWithFlagConditions.Count)];
					charges--;
					Vector3 a;
					groundNodes.GetNodePosition(nodeIndex, out a);
					ProjectileManager.instance.FireProjectile(new FireProjectileInfo
					{
						projectilePrefab = fissurePrefab,
						owner = null,
						damage = baseDamage * 2f,
						position = a + Vector3.up * 3f,
						rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f)
					});
				}
			}
		}

		public static class ScorchState
		{
			public static bool activated = false;
			public static float activation = 480f;

			internal static void SpawnScorch(Vector3 position)
			{
				Vector3 forward = Quaternion.identity * Util.ApplySpread(Vector3.forward, 0f, 0f, 1f, 1f, 0, 0f);
				FireProjectileInfo fireProjectileInfo = default(FireProjectileInfo);
				fireProjectileInfo.projectilePrefab = scorchPrefab;
				fireProjectileInfo.position = position;
				fireProjectileInfo.rotation = Util.QuaternionSafeLookRotation(forward);
				fireProjectileInfo.owner = null;
				fireProjectileInfo.damage = baseDamage * 2f;
				fireProjectileInfo.crit = false;
				ProjectileManager.instance.FireProjectile(fireProjectileInfo);

				if (deathExplosionEffect) EffectManager.SpawnEffect(deathExplosionEffect, new EffectData { origin = position, scale = 12.5f }, true);
			}
		}

		public static class NovaState
		{
			public static bool activated = false;
			public static float activation = 540f;

			private static List<Nova> activeNova = new List<Nova>();
			internal static bool clearNovaList = false;

			internal static void FixedUpdate()
			{
				for (int i = 0; i < activeNova.Count; i++)
				{
					Nova nova = activeNova[i];

					if (nova != null)
					{
						nova.Update();

						if (nova.fired)
						{
							activeNova.RemoveAt(i);
							i--;
							continue;
						}
					}
				}

				clearNovaList = false;
			}

			internal static void ServerSendNovaEffect(Vector3 position)
			{
				EffectManager.SpawnEffect((EffectIndex)1758000, new EffectData { origin = position }, true);
			}

			internal static void CreateNova(Vector3 position)
			{
				activeNova.Add(new Nova { position = position });
			}

			private static void FireNovaBlast(Vector3 position)
			{
				new BlastAttack
				{
					attacker = null,
					inflictor = null,
					teamIndex = TeamIndex.None,
					baseDamage = baseDamage * 4f,
					crit = false,
					baseForce = 0f,
					attackerFiltering = AttackerFiltering.NeverHit,
					damageColorIndex = DamageColorIndex.Default,
					damageType = DamageType.CrippleOnHit,
					procCoefficient = 0f,
					position = position,
					radius = 12.5f,
				}.Fire();
			}

			private class Nova
			{
				private float stopwatch = 0f;

				private bool charge = false;
				internal bool fired = false;

				internal Vector3 position;

				private GameObject chargeVfxInstance;
				private GameObject areaIndicatorVfxInstance;

				internal void Update()
				{
					if (fired) return;

					stopwatch += Time.fixedDeltaTime;

					if (clearNovaList)
					{
						DestroyNovaVfx();
						fired = true;
						return;
					}

					if (stopwatch >= 7f)
					{
						DestroyNovaVfx();
						fired = true;
						if (NetworkServer.active && enabled && activated) FireNovaBlast(position);
						return;
					}

					if (stopwatch >= 5f && !charge) SetupNovaVfx();
				}

				private void DestroyNovaVfx()
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
				}

				private void SetupNovaVfx()
				{
					charge = true;

					if (!chargeVfxInstance)
					{
						chargeVfxInstance = UnityEngine.Object.Instantiate(chargingEffectPrefab, position, Quaternion.identity);
						chargeVfxInstance.transform.localScale = Vector3.one * 0.125f;
					}
					if (!areaIndicatorVfxInstance)
					{
						areaIndicatorVfxInstance = UnityEngine.Object.Instantiate(areaIndicatorPrefab, position, Quaternion.identity);
						ObjectScaleCurve component = areaIndicatorVfxInstance.GetComponent<ObjectScaleCurve>();
						component.timeMax = 2f;
						component.baseScale = Vector3.one * 25f;
						areaIndicatorVfxInstance.GetComponent<AnimateShaderAlpha>().timeMax = 2f;
					}
				}
			}
		}



		public static bool cachedEffectData = false;
		public static GameObject fissurePrefab;
		public static GameObject scorchPrefab;
		public static GameObject deathExplosionEffect;
		public static GameObject chargingEffectPrefab;
		public static GameObject areaIndicatorPrefab;

		private static void CacheEffectData()
		{
			if (!cachedEffectData)
			{
				cachedEffectData = true;

				fissurePrefab = EntityStates.BrotherHaunt.FireRandomProjectiles.projectilePrefab;

				scorchPrefab = EntityStates.LunarExploderMonster.DeathState.projectilePrefab;
				deathExplosionEffect = EntityStates.LunarExploderMonster.DeathState.deathExplosionEffect;

				chargingEffectPrefab = EntityStates.VagrantMonster.ChargeMegaNova.chargingEffectPrefab;
				areaIndicatorPrefab = EntityStates.VagrantMonster.ChargeMegaNova.areaIndicatorPrefab;
			}
		}
	}
}
