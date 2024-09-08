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
		public static float phaseInterval = 60f;

		public static float damageFactor = 1f;
		public static float timeFactor = 1f;



		private static List<Blocker> activeBlocker = new List<Blocker>();
		internal static bool clearBlockerList = false;

		private class Blocker
		{
			internal float timer = 5f;
			internal Vector3 position;
		}

		private static void BlockerFixedUpdate()
		{
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
				if (activeBlocker.Count < 40 && PlayerWithinRange(position, 120f) && SpaceAvailable(position))
				{
					activeBlocker.Add(new Blocker { position = position });

					if (ScorchState.activated) ScorchState.SpawnScorch(position);
					if (NovaState.activated) NovaState.ServerSendNovaEffect(position);
				}
			}
		}

		private static bool SpaceAvailable(Vector3 position)
		{
			float sqrRad;

			if (activeBlocker.Count < 21) sqrRad = 225f;
			else if (activeBlocker.Count < 31) sqrRad = 400f;
			else sqrRad = 625f;

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

		private static CharacterBody GetRandomControlledPlayer()
		{
			List<CharacterBody> controlledPlayers = new List<CharacterBody>();

			ReadOnlyCollection<TeamComponent> teamMembers = TeamComponent.GetTeamMembers(TeamIndex.Player);
			for (int i = 0; i < teamMembers.Count; i++)
			{
				CharacterBody body = teamMembers[i].body;
				if (body && body.isPlayerControlled)
				{
					controlledPlayers.Add(body);
				}
			}

			if (controlledPlayers.Count > 0)
			{
				int index = UnityEngine.Random.Range(0, controlledPlayers.Count - 1);

				return controlledPlayers[index];
			}

			return null;
		}

		private static Vector3 GetNearbyGroundNodePosition(Vector3 origin)
		{
			NodeGraph groundNodes = SceneInfo.instance.groundNodes;
			if (groundNodes)
			{
				List<NodeGraph.NodeIndex> nodes = groundNodes.FindNodesInRangeWithFlagConditions(origin, 10f, 45f, HullMask.Golem, NodeFlags.None, NodeFlags.None, false);
				if (nodes.Count > 0)
				{
					NodeGraph.NodeIndex nodeIndex = nodes[UnityEngine.Random.Range(0, nodes.Count - 1)];
					groundNodes.GetNodePosition(nodeIndex, out Vector3 position);

					return position;
				}
			}

			return Vector3.zero;
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
				Display.sendSyncTime = false;

				enabled = true;
				reset = false;
				timer = 5f;
				stopwatch = 3600f;

				SetupValues();

				Display.SetServerDifficulty(damageFactor);

				LunarState.activation2 = 3600f + (30f * timeFactor);
				LunarState.activation3 = 3600f + (60f * timeFactor);
				BleedState.activation2 = 3600f + (120f * timeFactor);

				//Debug.LogWarning("Artifact of Instability - BaseDamage : " + baseDamage);
			}
		}

		internal static void Reset()
		{
			Display.sendSyncTime = false;

			enabled = false;
			reset = true;
			timer = 5f;
			stopwatch = -99999f;

			MeteorState.activated = false;
			MeteorState.activated2 = false;
			MeteorState.activated3 = false;
			MeteorState.controller = null;
			FissureState.activated = false;
			FissureState.activated2 = false;
			FissureState.activated3 = false;
			ScorchState.activated = false;
			NovaState.activated = false;
			LunarState.activated = false;
			LunarState.activated2 = false;
			LunarState.activated3 = false;
			BleedState.activated = false;
			BleedState.activated2 = false;
		}

		internal static void Disable()
		{
			Display.sendSyncTime = false;

			enabled = false;
			reset = false;
			timer = 99999f;
			stopwatch = -99999f;
		}

		private static void SetupValues()
		{
			damageFactor = Mathf.Pow(Run.instance.difficultyCoefficient, 0.75f);
			timeFactor = (1f + 2f * Mathf.Pow(0.9f, Run.instance.loopClearCount)) / 3f;

			baseTime = DiluvianArtifactPlugin.UnstabifactBaseTimer.Value;
			phaseInterval = DiluvianArtifactPlugin.UnstabifactPhaseTimer.Value;

			baseDamage = 24f * damageFactor;
			baseTime *= timeFactor;
			phaseInterval *=  timeFactor;

			MeteorState.activation = GetPhaseTime(0);
			FissureState.activation = GetPhaseTime(1);
			ScorchState.activation = GetPhaseTime(2);
			NovaState.activation = GetPhaseTime(2);
			FissureState.activation2 = GetPhaseTime(3);
			LunarState.activation = GetPhaseTime(3);
			MeteorState.activation2 = GetPhaseTime(4);
			BleedState.activation = GetPhaseTime(4);
			FissureState.activation3 = GetPhaseTime(5);
			MeteorState.activation3 = GetPhaseTime(6);
			LunarState.activation2 = GetPhaseTime(6);
			LunarState.activation3 = GetPhaseTime(9);
			BleedState.activation2 = GetPhaseTime(9);
		}

		private static float GetPhaseTime(int phase)
		{
			return baseTime + (phase * phaseInterval);
		}

		private static void RecalcDamageValue()
		{
			float newFactor = Mathf.Pow(Run.instance.difficultyCoefficient, 0.75f);

			if (damageFactor != newFactor)
			{
				damageFactor = newFactor;
				baseDamage = 24f * damageFactor;

				MeteorState.UpdateDamage();
				Display.SetServerDifficulty(damageFactor);

				//Debug.LogWarning("Artifact of Instability - BaseDamage : " + baseDamage);
			}
		}



		internal static void OnFixedUpdate()
		{
			if (disableFixedUpdate || !Run.instance) return;

			CacheEffectData();

			if (NetworkServer.active)
			{
				ServerUpdateTimer();
				BlockerFixedUpdate();
				MeteorState.FixedUpdate();
				FissureState.FixedUpdate();
				LunarState.FixedUpdate();
			}

			NovaState.FixedUpdate();
		}

		private static void ServerUpdateTimer()
		{
			if (!Run.instance.isRunStopwatchPaused)
			{
				timer -= Time.fixedDeltaTime;
				stopwatch += Time.fixedDeltaTime;

				Display.SyncTimer();
			}

			if (timer < 0f)
			{
				timer = 0.333f;

				ResetState();

				if (enabled)
				{
					RecalcDamageValue();
					AdvanceState();
				}
			}
		}

		private static void ResetState()
		{
			if (!reset) return;

			reset = false;

			stopwatch = 0f;
			SetupValues();

			Display.sendSyncTime = false;

			if (ZetUnstabifact.Enabled)
			{
				if (TeleporterInteraction.instance)
				{
					enabled = true;
					Display.sendSyncTime = true;
					Display.SetServerSyncTime(MeteorState.activation);
					Display.SetServerDifficulty(damageFactor);
					//Debug.LogWarning("Artifact of Instability - BaseTimer : " + baseTime + " , BaseDamage : " + baseDamage);
				}
			}
		}

		private static void AdvanceState()
		{
			//if (!LunarState.activated && stopwatch < LunarState.activation - 10f) stopwatch = LunarState.activation - 5f;



			if (!MeteorState.activated && stopwatch > MeteorState.activation)
			{
				MeteorState.activated = true;
				MeteorState.ResetState();
				MeteorState.StartStorm();

				Display.SetServerSyncTime(FissureState.activation - stopwatch);
			}



			if (!FissureState.activated && stopwatch > FissureState.activation)
			{
				FissureState.activated = true;
				FissureState.ResetState();

				Display.SetServerSyncTime(ScorchState.activation - stopwatch);
			}



			if (!ScorchState.activated && stopwatch > ScorchState.activation)
			{
				ScorchState.activated = true;

				//CountdownDisplay.ServerSendSyncTime(NovaState.activation - stopwatch);
			}
			if (!NovaState.activated && stopwatch > NovaState.activation)
			{
				NovaState.activated = true;

				Display.SetServerSyncTime(LunarState.activation - stopwatch);
			}



			if (FissureState.activated && !FissureState.activated2 && stopwatch > FissureState.activation2)
			{
				FissureState.activated2 = true;
				// activate extra charges
			}
			if (!LunarState.activated && stopwatch > LunarState.activation)
			{
				LunarState.activated = true;
				LunarState.ResetState(1);

				Display.SetServerSyncTime(BleedState.activation - stopwatch);
			}



			if (MeteorState.activated && !MeteorState.activated2 && stopwatch > MeteorState.activation2)
			{
				MeteorState.activated2 = true;
				// activate extra charges
			}
			if (!BleedState.activated && stopwatch > BleedState.activation)
			{
				BleedState.activated = true;
				// allows lunar bleed application from cripple
			}



			if (FissureState.activated2 && !FissureState.activated3 && stopwatch > FissureState.activation3)
			{
				FissureState.activated3 = true;
				FissureState.Hasten();
			}



			if (MeteorState.activated2 && !MeteorState.activated3 && stopwatch > MeteorState.activation3)
			{
				MeteorState.activated3 = true;
				MeteorState.Hasten();
			}
			if (LunarState.activated && !LunarState.activated2 && stopwatch > LunarState.activation2)
			{
				LunarState.activated2 = true;
				LunarState.ResetState(2);
			}



			if (LunarState.activated2 && !LunarState.activated3 && stopwatch > LunarState.activation3)
			{
				LunarState.activated3 = true;
				LunarState.ResetState(3);
			}
			if (BleedState.activated && !BleedState.activated2 && stopwatch > BleedState.activation2)
			{
				BleedState.activated2 = true;
				// increase lunar bleed damage
			}
		}



		internal static class Display
		{
			private static GameObject displayPanel;
			private static GameObject displayTimeText;
			private static HGTextMeshProUGUI timeTextMesh;
			private static GameObject displayDiffText;
			private static HGTextMeshProUGUI diffTextMesh;

			internal static bool sendSyncTime = false;

			internal static float syncTime = 0f;
			internal static float difficulty = 0f;
			private static string currentTimeText = "";
			private static string currentDiffText = "";

			private static float timeSinceSync = 0f;

			internal static void ResetServerSyncTime()
			{
				float time = Run.instance.GetRunStopwatch();

				timeSinceSync = 0f;
				EffectManager.SpawnEffect((EffectIndex)1758000, new EffectData { genericUInt = 2u, genericFloat = time }, true);
			}

			internal static void SetServerSyncTime(float offset)
			{
				if (sendSyncTime)
				{
					float time = Run.instance.GetRunStopwatch() + offset;

					timeSinceSync = 0f;
					EffectManager.SpawnEffect((EffectIndex)1758000, new EffectData { genericUInt = 2u, genericFloat = time }, true);
				}
			}

			internal static void ServerSendSyncTime(float offset)
			{
				if (sendSyncTime)
				{
					float time = Run.instance.GetRunStopwatch() + offset;

					timeSinceSync = 0f;
					EffectManager.SpawnEffect((EffectIndex)1758000, new EffectData { genericUInt = 4u, genericFloat = time }, true);
				}
			}

			internal static void SetServerDifficulty(float value)
			{
				EffectManager.SpawnEffect((EffectIndex)1758000, new EffectData { genericUInt = 3u, genericFloat = value }, true);
			}

			internal static void InitializeUI(HUD hud)
			{
				displayPanel = new GameObject("UnstabifactPanel");
				RectTransform panelTransform = displayPanel.AddComponent<RectTransform>();

				displayPanel.transform.SetParent(hud.gameModeUiInstance.transform);
				displayPanel.transform.SetAsLastSibling();

				displayTimeText = new GameObject("UnstabifactTimeText");
				RectTransform timeTextTransform = displayTimeText.AddComponent<RectTransform>();
				timeTextMesh = displayTimeText.AddComponent<HGTextMeshProUGUI>();

				displayTimeText.transform.SetParent(displayPanel.transform);

				displayDiffText = new GameObject("UnstabifactDiffText");
				RectTransform diffTextTransform = displayDiffText.AddComponent<RectTransform>();
				diffTextMesh = displayDiffText.AddComponent<HGTextMeshProUGUI>();

				displayDiffText.transform.SetParent(displayPanel.transform);

				panelTransform.localPosition = new Vector3(0, 0, 0);
				panelTransform.anchorMin = new Vector2(0, 0);
				panelTransform.anchorMax = new Vector2(0, 0);
				panelTransform.localScale = Vector3.one;
				panelTransform.pivot = new Vector2(0, 1);
				panelTransform.sizeDelta = new Vector2(80, 40);
				panelTransform.anchoredPosition = new Vector2(32, 48);
				panelTransform.eulerAngles = new Vector3(0, 5f, 0);

				timeTextTransform.localPosition = Vector3.zero;
				timeTextTransform.anchorMin = Vector2.zero;
				timeTextTransform.anchorMax = Vector2.one;
				timeTextTransform.localScale = Vector3.one;
				timeTextTransform.sizeDelta = new Vector2(-12, -12);
				timeTextTransform.anchoredPosition = Vector2.zero;

				timeTextMesh.enableAutoSizing = false;
				timeTextMesh.fontSize = 12;
				timeTextMesh.faceColor = new Color(0.875f, 0.75f, 1f);
				timeTextMesh.alignment = TMPro.TextAlignmentOptions.MidlineRight;
				timeTextMesh.richText = true;

				timeTextMesh.SetText("");

				diffTextTransform.localPosition = Vector3.zero;
				diffTextTransform.anchorMin = Vector2.zero;
				diffTextTransform.anchorMax = Vector2.one;
				diffTextTransform.localScale = Vector3.one;
				diffTextTransform.sizeDelta = new Vector2(-12, -12);
				diffTextTransform.anchoredPosition = Vector2.zero;

				diffTextMesh.enableAutoSizing = false;
				diffTextMesh.fontSize = 10;
				diffTextMesh.faceColor = new Color(0.65f, 0.65f, 0.65f);
				diffTextMesh.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
				diffTextMesh.richText = true;

				diffTextMesh.SetText("");
			}

			internal static void SyncTimer()
			{
				if (syncTime > 0f && Run.instance)
				{
					timeSinceSync += Time.fixedDeltaTime;

					if (timeSinceSync >= 10f)
					{
						float runStopwatch = Run.instance.GetRunStopwatch();

						if (runStopwatch < syncTime)
						{
							float timeLeft = syncTime - runStopwatch;

							if (timeLeft >= 10f)
							{
								timeSinceSync = 0f;
								ServerSendSyncTime(timeLeft);
							}
						}
					}
				}
			}

			internal static void UpdateUI()
			{
				if (timeTextMesh != null)
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

					if (text != currentTimeText)
					{
						currentTimeText = text;

						timeTextMesh.SetText("<mspace=6.6>" + text + "</mspace>");
					}
				}

				if (diffTextMesh != null)
				{
					string text = "";

					float diffValue = DiluvianArtifactPlugin.UnstabifactDisplayDifficulty.Value ? difficulty : 0f;
					if (diffValue > 0f) text = FormatDifficulty(diffValue);

					if (text != currentDiffText)
					{
						currentDiffText = text;

						diffTextMesh.SetText("<mspace=5.5>" + text + "</mspace>");
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

			private static string FormatDifficulty(float value)
			{
				if (value < 10f) return value.ToString("0.00");
				if (value < 100f) return value.ToString("0.0");
				return Mathf.RoundToInt(difficulty).ToString();
			}

			internal static void SetSyncTime(float time)
			{
				syncTime = time;
			}

			internal static void SetDifficulty(float value)
			{
				difficulty = value;
			}
		}



		public static class MeteorState
		{
			public static bool activated = false;
			public static float activation = 360f;
			public static bool activated2 = false;
			public static float activation2 = 600f;
			public static bool activated3 = false;
			public static float activation3 = 720f;

			private static int extraCharges = 0;
			private static float extraChargeTimer = 0f;
			private static float extraChargeInterval = 1.0f;
			private static float extraChargeFireChance = 0.05f;

			internal static MeteorStormController controller;

			internal static void StartStorm()
			{
				GameObject MeteorStorm = UnityEngine.Object.Instantiate(LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/MeteorStorm"), Vector3.zero, Quaternion.identity);
				controller = MeteorStorm.GetComponent<MeteorStormController>();
				controller.ownerDamage = baseDamage;
				controller.waveCount = 9999999;
				NetworkServer.Spawn(controller.gameObject);
			}

			internal static void UpdateDamage()
			{
				if (controller) controller.ownerDamage = baseDamage;
			}

			internal static void ResetState()
			{
				extraCharges = 0;
				extraChargeTimer = 0f;

				extraChargeInterval = 1.0f;
				extraChargeFireChance = 0.05f;
			}

			internal static void Hasten()
			{
				extraChargeInterval = 0.8f;
				extraChargeFireChance = 0.075f;
			}

			internal static void FixedUpdate()
			{
				if (!enabled || !activated) return;

				if (controller && activated2)
				{
					extraChargeTimer -= Time.fixedDeltaTime;

					if (extraChargeTimer <= 0f)
					{
						extraChargeTimer += extraChargeInterval;
						extraCharges = Mathf.Min(extraCharges + 1, 10);
					}

					if (extraCharges > 0 && UnityEngine.Random.value < extraChargeFireChance)
					{
						extraCharges--;

						MeteorActivePlayers();
					}
				}
			}

			private static void MeteorActivePlayers()
			{
				List<CharacterBody> controlledPlayers = new List<CharacterBody>();

				ReadOnlyCollection<TeamComponent> teamMembers = TeamComponent.GetTeamMembers(TeamIndex.Player);
				for (int i = 0; i < teamMembers.Count; i++)
				{
					CharacterBody body = teamMembers[i].body;
					if (body && body.isPlayerControlled)
					{
						controlledPlayers.Add(body);
					}
				}

				if (controlledPlayers.Count > 0)
				{
					foreach (CharacterBody playerBody in controlledPlayers)
					{
						HealthComponent hc = playerBody.healthComponent;
						if (hc && hc.alive)
						{
							MeteorStormController.Meteor nextMeteor = CreateMeteor(playerBody);
							if (nextMeteor.valid)
							{
								//Debug.LogWarning("Artifact of Instability - ExtraMeteor : " + nextMeteor.impactPosition);

								controller.meteorList.Add(nextMeteor);
								EffectManager.SpawnEffect(controller.warningEffectPrefab, new EffectData
								{
									origin = nextMeteor.impactPosition,
									scale = controller.blastRadius
								}, true);
							}
						}
					}
				}
			}

			private static MeteorStormController.Meteor CreateMeteor(CharacterBody body)
			{
				MeteorStormController.Meteor meteor = new MeteorStormController.Meteor();

				if (body)
				{
					Vector3 nearby = GetNearbyGroundNodePosition(body.corePosition);

					if (nearby.sqrMagnitude > 1f) meteor.impactPosition = nearby;
					else meteor.impactPosition = body.corePosition;

					Vector3 origin = meteor.impactPosition + Vector3.up * 6f;
					Vector3 onUnitSphere = UnityEngine.Random.onUnitSphere;
					onUnitSphere.y = -1f;
					RaycastHit raycastHit;
					if (Physics.Raycast(origin, onUnitSphere, out raycastHit, 12f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
					{
						meteor.impactPosition = raycastHit.point;
					}
					else if (Physics.Raycast(meteor.impactPosition, Vector3.down, out raycastHit, float.PositiveInfinity, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
					{
						meteor.impactPosition = raycastHit.point;
					}
				}
				else
				{
					meteor.valid = false;
				}

				meteor.startTime = Run.instance.time;

				return meteor;
			}
		}

		public static class FissureState
		{
			public static bool activated = false;
			public static float activation = 420f;
			public static bool activated2 = false;
			public static float activation2 = 540f;
			public static bool activated3 = false;
			public static float activation3 = 660f;

			private static int charges = 0;
			private static float chargeTimer = 0f;
			private static float chargeInterval = 0.125f;
			private static float chargeFireChance = 0.1f;

			private static int extraCharges = 0;
			private static float extraChargeTimer = 0f;
			private static float extraChargeInterval = 1.0f;
			private static float extraChargeFireChance = 0.05f;

			internal static void ResetState()
			{
				charges = 0;
				chargeTimer = 0f;

				extraCharges = 0;
				extraChargeTimer = 0f;

				extraChargeInterval = 1.0f;
				extraChargeFireChance = 0.05f;
			}

			internal static void Hasten()
			{
				extraChargeInterval = 0.8f;
				extraChargeFireChance = 0.075f;
			}

			internal static void FixedUpdate()
			{
				if (!enabled || !activated) return;

				chargeTimer -= Time.fixedDeltaTime;

				if (chargeTimer <= 0f)
				{
					chargeTimer += chargeInterval;
					charges = Mathf.Min(charges + 1, 40);
				}

				if (charges > 0 && UnityEngine.Random.value < chargeFireChance)
				{
					Vector3 position = FindFissureTarget();
					if (position.sqrMagnitude > 1f)
					{
						charges--;
						FireFissure(position);
					}
				}

				if (activated2)
				{
					extraChargeTimer -= Time.fixedDeltaTime;

					if (extraChargeTimer <= 0f)
					{
						extraChargeTimer += extraChargeInterval;
						extraCharges = Mathf.Min(extraCharges + 1, 10);
					}

					if (extraCharges > 0 && UnityEngine.Random.value < extraChargeFireChance)
					{
						extraCharges--;

						FissureActivePlayers();
					}
				}
			}

			private static Vector3 FindFissureTarget()
			{
				NodeGraph groundNodes = SceneInfo.instance.groundNodes;
				if (groundNodes)
				{
					List<NodeGraph.NodeIndex> nodes = groundNodes.GetActiveNodesForHullMaskWithFlagConditions(HullMask.Golem, NodeFlags.None, NodeFlags.NoCharacterSpawn);
					if (nodes.Count > 0)
					{
						NodeGraph.NodeIndex nodeIndex = nodes[UnityEngine.Random.Range(0, nodes.Count - 1)];
						groundNodes.GetNodePosition(nodeIndex, out Vector3 position);

						return position;
					}
				}

				return Vector3.zero;
			}

			private static void FireFissure(Vector3 position)
			{
				ProjectileManager.instance.FireProjectile(new FireProjectileInfo
				{
					projectilePrefab = fissurePrefab,
					owner = null,
					damage = baseDamage * 2f,
					position = position + Vector3.up * 3f,
					rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f)
				});
			}

			private static void FissureActivePlayers()
			{
				List<CharacterBody> controlledPlayers = new List<CharacterBody>();

				ReadOnlyCollection<TeamComponent> teamMembers = TeamComponent.GetTeamMembers(TeamIndex.Player);
				for (int i = 0; i < teamMembers.Count; i++)
				{
					CharacterBody body = teamMembers[i].body;
					if (body && body.isPlayerControlled)
					{
						controlledPlayers.Add(body);
					}
				}

				if (controlledPlayers.Count > 0)
				{
					foreach (CharacterBody playerBody in controlledPlayers)
					{
						HealthComponent hc = playerBody.healthComponent;
						if (hc && hc.alive)
						{
							Vector3 position = GetNearbyGroundNodePosition(playerBody.corePosition);

							if (position.sqrMagnitude > 1f) FireFissure(position);
							else FireFissure(playerBody.corePosition);

							//Debug.LogWarning("Artifact of Instability - ExtraFissure : " + position);
						}
					}
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
				fireProjectileInfo.damage = baseDamage * 1.5f;
				fireProjectileInfo.crit = false;
				ProjectileManager.instance.FireProjectile(fireProjectileInfo);

				if (deathExplosionEffect) EffectManager.SpawnEffect(deathExplosionEffect, new EffectData { origin = position, scale = 12.5f }, true);
			}
		}

		public static class NovaState
		{
			public static bool activated = false;
			public static float activation = 480f;

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
					baseDamage = baseDamage * 3f,
					crit = false,
					baseForce = 0f,
					bonusForce = Vector3.zero,
					attackerFiltering = AttackerFiltering.NeverHitSelf,
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

		public static class LunarState
		{
			public static bool activated = false;
			public static float activation = 540f;
			public static bool activated2 = false;
			public static float activation2 = 720f;
			public static bool activated3 = false;
			public static float activation3 = 900f;

			private static int chimeraCount = 0;
			private static int chimeraLimit = 1;
			private static int failedSpawnAttempts = 0;
			private static float spawnInterval = 30f;
			private static float countStopwatch = 0f;
			private static float spawnStopwatch = 0f;
			private static float leashStopwatch = 0f;
			private static bool uberChimera = false;
			private static bool updateDelayed = false;

			internal static void ResetState(int count)
			{
				chimeraCount = GetActiveUberChimeraCount();
				chimeraLimit = RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.Swarms) ? count * 2 : count;
				failedSpawnAttempts = 0;
				spawnInterval = 30f * timeFactor;
				countStopwatch = 0f;
				spawnStopwatch = (chimeraCount >= chimeraLimit) ? 0f : spawnInterval;
				leashStopwatch = 0f;
				uberChimera = false;
			}

			internal static void DelayUpdate()
			{
				countStopwatch = 0f;
				leashStopwatch = 0f;
				updateDelayed = true;
			}

			internal static void FixedUpdate()
			{
				if (!enabled || !activated) return;

				//Debug.LogWarning("Artifact of Instability - [ " + chimeraCount + " / " + chimeraLimit + " ] - Delay : " + updateDelayed + " - Count " + $"{countStopwatch:0.00} / 2.5" + " - Spawn " + $"{spawnStopwatch:0.00} / " + $"{spawnInterval:0.###}");

				if (updateDelayed || chimeraCount >= chimeraLimit)
				{
					countStopwatch += Time.fixedDeltaTime;

					if (countStopwatch >= 2.5f)
					{
						updateDelayed = false;
						countStopwatch = 0f;

						chimeraCount = GetActiveUberChimeraCount();
					}
				}
				else
				{
					spawnStopwatch += Time.fixedDeltaTime;

					if (spawnStopwatch >= spawnInterval)
					{
						DelayUpdate();
						spawnStopwatch = 0f;

						SummonUberChimera();
					}
				}

				if (!updateDelayed && chimeraCount > 0)
				{
					leashStopwatch += Time.fixedDeltaTime;

					if (leashStopwatch >= 10f)
					{
						leashStopwatch = 0f;

						LeashUberChimera();
					}
				}
				else
				{
					leashStopwatch = 0f;
				}
			}

			private static int GetActiveUberChimeraCount()
			{
				int count = 0;

				ReadOnlyCollection<TeamComponent> teamMembers = TeamComponent.GetTeamMembers(TeamIndex.Monster);
				for (int i = 0; i < teamMembers.Count; i++)
				{
					CharacterBody body = teamMembers[i].body;
					if (body && IsUberChimera(body)) count++;
				}

				//Debug.LogWarning("Artifact of Instability - ActiveUberChimera : " + count + " / " + chimeraLimit);

				return count;
			}

			internal static bool IsUberChimera(CharacterBody body)
			{
				if (body.bodyIndex != lunarChimeraBodyIndex) return false;

				Inventory inventory = body.inventory;
				if (inventory)
				{
					if (inventory.currentEquipmentIndex != RoR2Content.Equipment.AffixLunar.equipmentIndex) return false;
					if (inventory.GetItemCount(RoR2Content.Items.ShieldOnly) > 0 && inventory.GetItemCount(RoR2Content.Items.Knurl) > 0) return true;
				}

				return false;
			}

			private static void SummonUberChimera()
			{
				if (lunarChimeraSpawnCard)
				{
					CharacterBody target = GetRandomControlledPlayer();
					if (target)
					{
						DirectorPlacementRule placeRule = new DirectorPlacementRule
						{
							placementMode = DirectorPlacementRule.PlacementMode.Approximate,
							minDistance = 45f,
							maxDistance = 90f + Mathf.Min(30f * failedSpawnAttempts, 90f),
							spawnOnTarget = target.transform
						};

						DirectorSpawnRequest directorSpawnRequest = new DirectorSpawnRequest(lunarChimeraSpawnCard, placeRule, RoR2Application.rng)
						{
							teamIndexOverride = TeamIndex.Monster,
							ignoreTeamMemberLimit = true
						};

						uberChimera = true;

						GameObject spawnResult = DirectorCore.instance.TrySpawnObject(directorSpawnRequest);
						if (spawnResult)
						{
							failedSpawnAttempts = 0;
						}
						else
						{
							failedSpawnAttempts++;

							if (failedSpawnAttempts < 4) spawnStopwatch = spawnInterval - 2.5f;
						}

						uberChimera = false;
					}
				}
			}

			internal static void UberifyChimera(CharacterMaster master)
			{
				if (!enabled || !activated || !uberChimera) return;

				Inventory inventory = master.inventory;

				float factor = damageFactor / 2f;

				inventory.SetEquipmentIndex(RoR2Content.Equipment.AffixLunar.equipmentIndex);
				inventory.GiveItem(RoR2Content.Items.ShieldOnly, 1);
				inventory.GiveItem(RoR2Content.Items.Knurl, 1);

				inventory.GiveItem(RoR2Content.Items.LunarBadLuck, 1);
				inventory.GiveItem(RoR2Content.Items.BoostAttackSpeed, Mathf.RoundToInt(factor));
				inventory.GiveItem(RoR2Content.Items.BoostDamage, Mathf.RoundToInt(3f * factor));
				inventory.GiveItem(RoR2Content.Items.BoostHp, Mathf.RoundToInt(5f * factor));
			}

			private static void LeashUberChimera()
			{
				List<CharacterBody> uberChimera = new List<CharacterBody>();

				ReadOnlyCollection<TeamComponent> teamMembers = TeamComponent.GetTeamMembers(TeamIndex.Monster);
				for (int i = 0; i < teamMembers.Count; i++)
				{
					CharacterBody body = teamMembers[i].body;
					if (body && IsUberChimera(body) && !PlayerWithinRange(body.corePosition, 180f)) uberChimera.Add(body);
				}

				if (uberChimera.Count > 0)
				{
					SpawnCard spawnCard = ScriptableObject.CreateInstance<SpawnCard>();
					spawnCard.hullSize = HullClassification.Human;
					spawnCard.nodeGraphType = MapNodeGroup.GraphType.Ground;
					spawnCard.prefab = LegacyResourcesAPI.Load<GameObject>("SpawnCards/HelperPrefab");

					foreach (CharacterBody leashBody in uberChimera)
					{
						CharacterBody target = GetRandomControlledPlayer();
						if (target)
						{
							DirectorPlacementRule placeRule = new DirectorPlacementRule
							{
								placementMode = DirectorPlacementRule.PlacementMode.Approximate,
								minDistance = 45f,
								maxDistance = 90f,
								spawnOnTarget = target.transform
							};

							GameObject gameObject = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(spawnCard, placeRule, RoR2Application.rng));
							if (gameObject)
							{
								Vector3 position = gameObject.transform.position;
								if ((position - target.corePosition).sqrMagnitude < 14400f)
								{
									//Debug.LogWarning("Artifact of Instability - Leashing UberChimera");
									TeleportHelper.TeleportBody(leashBody, position);
								}
								UnityEngine.Object.Destroy(gameObject);
							}
						}
					}

					UnityEngine.Object.Destroy(spawnCard);
				}
			}
		}

		public static class BleedState
		{
			public static bool activated = false;
			public static float activation = 600f;
			public static bool activated2 = false;
			public static float activation2 = 900f;
		}



		public static bool cachedEffectData = false;
		public static GameObject fissurePrefab;
		public static GameObject scorchPrefab;
		public static GameObject deathExplosionEffect;
		public static GameObject chargingEffectPrefab;
		public static GameObject areaIndicatorPrefab;
		public static BodyIndex lunarChimeraBodyIndex = BodyIndex.None;
		public static SpawnCard lunarChimeraSpawnCard;

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

				lunarChimeraBodyIndex = BodyCatalog.FindBodyIndex("LunarGolemBody");
				lunarChimeraSpawnCard = LegacyResourcesAPI.Load<SpawnCard>("SpawnCards/CharacterSpawnCards/cscLunarGolem");
			}
		}
	}
}
