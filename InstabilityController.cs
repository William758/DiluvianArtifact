using RoR2;
using RoR2.Navigation;
using RoR2.Projectile;
using RoR2.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.Networking;

namespace TPDespair.DiluvianArtifact
{
	public static class InstabilityController
	{
		internal static bool disableFixedUpdate = true;

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



		internal static void MarkListsForClearing()
		{
			clearBlockerList = true;
			NovaState.clearNovaList = true;
		}

		internal static void MoonActivation()
		{
			if (!enabled && NetworkServer.active)
			{
				CountdownDisplay.sendSyncTime = false;

				enabled = true;
				reset = false;
				timer = 3f;
				stopwatch = 3600f;

				SetupValues();

				Debug.LogWarning("Artifact of Instability - BaseDamage : " + baseDamage);
			}
		}

		internal static void Reset()
		{
			CountdownDisplay.sendSyncTime = false;

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
			CountdownDisplay.sendSyncTime = false;

			enabled = false;
			reset = false;
			timer = 99999f;
			stopwatch = -99999f;
		}

		private static void SetupValues()
		{
			baseDamage = 24f * Mathf.Pow(Run.instance.difficultyCoefficient, 0.75f);
			//baseDamage *= 0.05f;
			baseTime = 120f + 240f * Mathf.Pow(0.9f, Run.instance.loopClearCount);
			//baseTime *= 0.0833f;

			MeteorState.activation = baseTime;
			FissureState.activation = baseTime * 1.166667f;
			ScorchState.activation = baseTime * 1.333333f;
			NovaState.activation = baseTime * 1.5f;
		}



		internal static void OnFixedUpdate()
		{
			if (disableFixedUpdate || !Run.instance) return;

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

			CountdownDisplay.sendSyncTime = false;

			if (ZetUnstabifact.Enabled)
			{
				if (TeleporterInteraction.instance)
				{
					enabled = true;
					CountdownDisplay.sendSyncTime = true;
					CountdownDisplay.ServerSendSyncTime(MeteorState.activation);
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

				CountdownDisplay.ServerSendSyncTime(FissureState.activation - stopwatch);
			}
			if (!FissureState.activated && stopwatch > FissureState.activation)
			{
				FissureState.activated = true;
				FissureState.ResetCharges();

				CountdownDisplay.ServerSendSyncTime(ScorchState.activation - stopwatch);
			}
			if (!ScorchState.activated && stopwatch > ScorchState.activation)
			{
				ScorchState.activated = true;

				CountdownDisplay.ServerSendSyncTime(NovaState.activation - stopwatch);
			}
			if (!NovaState.activated && stopwatch > NovaState.activation)
			{
				NovaState.activated = true;
			}
		}



		internal static class CountdownDisplay
		{
			private static GameObject displayPanel;
			private static GameObject displayText;
			private static HGTextMeshProUGUI textMesh;

			internal static bool sendSyncTime = false;
			internal static float syncTime = 0f;
			private static string currentText = "";

			internal static void ServerSendSyncTime(float offset, bool force = false)
			{
				if (sendSyncTime || force)
				{
					float time = Run.instance.GetRunStopwatch() + offset;

					EffectManager.SpawnEffect((EffectIndex)1758000, new EffectData { genericUInt = 2u, genericFloat = time }, true);
				}
			}

			internal static void InitializeUI(HUD hud)
			{
				displayPanel = new GameObject("UnstabifactPanel");
				RectTransform panelTransform = displayPanel.AddComponent<RectTransform>();

				displayPanel.transform.SetParent(hud.runStopwatchTimerTextController.transform);
				displayPanel.transform.SetAsLastSibling();

				displayText = new GameObject("UnstabifactText");
				RectTransform textTransform = displayText.AddComponent<RectTransform>();
				textMesh = displayText.AddComponent<HGTextMeshProUGUI>();

				displayText.transform.SetParent(displayPanel.transform);

				panelTransform.localPosition = new Vector3(0, 0, 0);
				panelTransform.anchorMin = new Vector2(0, 0);
				panelTransform.anchorMax = new Vector2(0, 0);
				panelTransform.localScale = Vector3.one;
				panelTransform.pivot = new Vector2(0, 1);
				panelTransform.sizeDelta = new Vector2(80, 40);
				panelTransform.anchoredPosition = new Vector2(80, 64);
				panelTransform.eulerAngles = new Vector3(0, 5f, 0);

				textTransform.localPosition = Vector3.zero;
				textTransform.anchorMin = Vector2.zero;
				textTransform.anchorMax = Vector2.one;
				textTransform.localScale = Vector3.one;
				textTransform.sizeDelta = new Vector2(-12, -12);
				textTransform.anchoredPosition = Vector2.zero;

				textMesh.enableAutoSizing = false;
				textMesh.fontSize = 10;
				textMesh.faceColor = new Color(0.875f,0.75f,1f);
				textMesh.alignment = TMPro.TextAlignmentOptions.MidlineRight;
				textMesh.richText = true;

				textMesh.SetText("");
			}

			internal static void UpdateUI()
			{
				if (textMesh != null)
				{
					string text = "";

					if (syncTime > 0f && Run.instance)
					{
						float runStopwatch = Run.instance.GetRunStopwatch();

						if (runStopwatch < syncTime)
						{
							float timeLeft = syncTime - runStopwatch;

							text = FormatTimer(timeLeft);
						}
					}

					if (text != currentText)
					{
						currentText = text;

						textMesh.SetText("<mspace=6>" + text + "</mspace>");
					}
				}
			}

			private static string FormatTimer(float time)
			{
				time = Mathf.Ceil(time * 100f);

				float a, b;

				if (time >= 6000)
				{
					a = Mathf.Floor(time / 6000f);
					b = Mathf.Floor((time % 6000f) / 100f);

					return a + ":" + b.ToString("00");
				}
				else
				{
					a = Mathf.Floor(time / 100f);
					b = Mathf.Floor(time % 100f);

					return a + "." + b.ToString("00");
				}
			}

			internal static void SetSyncTime(float time)
			{
				syncTime = time;
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

			internal static void ServerSendNovaEffect(Vector3 position)
			{
				EffectManager.SpawnEffect((EffectIndex)1758000, new EffectData { genericUInt = 1u, origin = position }, true);
			}

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
