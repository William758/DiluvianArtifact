using RoR2;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Networking;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace TPDespair.DiluvianArtifact
{
	public static class ZetUnstabifact
	{
		private static bool customHaunt = false;
		private static FieldInfo BlastAttackDamageTypeField;

		private static BuffIndex reactorInvuln = BuffIndex.None;
		private static BuffIndex waterInvuln = BuffIndex.None;



		private static int state = 0;

		public static bool Enabled
		{
			get
			{
				if (state < 1) return false;
				else if (state > 1) return true;
				else
				{
					if (RunArtifactManager.instance && RunArtifactManager.instance.IsArtifactEnabled(DiluvianArtifactContent.Artifacts.ZetUnstabifact)) return true;

					return false;
				}
			}
		}



		internal static void Init()
		{
			state = DiluvianArtifactPlugin.UnstabifactEnable.Value;
			if (state < 1) return;

			InstabilityController.disableFixedUpdate = false;

			DiluvianArtifactPlugin.RegisterLanguageToken("ARTIFACT_ZETUNSTABIFACT_NAME", "Artifact of Instability");
			DiluvianArtifactPlugin.RegisterLanguageToken("ARTIFACT_ZETUNSTABIFACT_DESC", "Spending too long in a stage causes a lunar storm.");

			DamageTakenHook();
			DegenHook();

			// server only
			SceneDirector.onPostPopulateSceneServer += ResetInstabilityController;
			Stage.onServerStageComplete += DisableInstabilityController;

			SpawnCard.onSpawnedServerGlobal += OnCardSpawned;
			FallenChimeraHook();
			DamageResistHook();

			// client and server
			Run.onRunDestroyGlobal += OnRunDestroyed;
			Stage.onStageStartGlobal += OnStageStarted;

			BlastAttackDamageTypeField = typeof(BlastAttack).GetField("damageType");
			MeteorImpactHook();
			MeteorDamageTypeHook();
			EffectManagerNetworkingHook();

			BrotherHauntEnterHook();
			BrotherHauntUpdateHook();

			HUDAwakeHook();
			HUDUpdateHook();
		}

		internal static void LateSetup()
		{
			if (PluginLoaded("com.TeamMoonstorm.Starstorm2-Nightly"))
			{
				reactorInvuln = BuffCatalog.FindBuffIndex("BuffReactor");
			}

			if (PluginLoaded("com.themysticsword.risingtides"))
			{
				waterInvuln = BuffCatalog.FindBuffIndex("RisingTides_WaterInvincibility");
			}
		}

		public static bool PluginLoaded(string key)
		{
			return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(key);
		}



		private static void DamageTakenHook()
		{
			On.RoR2.HealthComponent.TakeDamageProcess += (orig, self, damageInfo) =>
			{
				orig(self, damageInfo);

				if (NetworkServer.active && Enabled && InstabilityController.BleedState.activated)
				{
					if (!damageInfo.rejected && self.alive && !self.godMode && self.ospTimer <= 0f)
					{
						CharacterBody body = self.body;
						if (body && body.teamComponent.teamIndex == TeamIndex.Player)
						{
							if (!HasInvulnBuff(body))
							{
								if (damageInfo.attacker)
								{
									CharacterBody attackerBody = damageInfo.attacker.GetComponent<CharacterBody>();
									if (attackerBody && attackerBody.HasBuff(RoR2Content.Buffs.AffixLunar))
									{
										ApplyLunarBleed(body, 4f);
										return;
									}
								}

								if (damageInfo.inflictor)
								{
									CharacterBody inflictorBody = damageInfo.inflictor.GetComponent<CharacterBody>();
									if (inflictorBody) return;
								}

								if ((damageInfo.damageType & DamageType.CrippleOnHit) != DamageType.Generic) ApplyLunarBleed(body, 4f);
							}
						}
					}
				}
			};
		}

		private static void DegenHook()
		{
			On.RoR2.HealthComponent.ServerFixedUpdate += (orig, self, delta) =>
			{
				orig(self, delta);

				if (self.alive && !self.godMode)
				{
					CharacterBody body = self.body;
					if (body)
					{
						int buffCount = body.GetBuffCount(DiluvianArtifactContent.Buffs.ZetLunarBleed);
						bool bodyImmune = HasInvulnBuff(body);

						if (buffCount > 0 && !bodyImmune)
						{
							body.outOfDangerStopwatch = 0f;

							float damage = Mathf.Sqrt(buffCount) * 0.025f * self.combinedHealth * delta;
							if (Enabled && InstabilityController.BleedState.activated2) damage *= 1.5f;
							DirectDamage(self, damage);
						}
					}
				}
			};
		}

		internal static bool HasInvulnBuff(CharacterBody body)
		{
			if (body.HasBuff(RoR2Content.Buffs.HiddenInvincibility)) return true;
			if (body.HasBuff(RoR2Content.Buffs.Immune)) return true;
			if (body.HasBuff(reactorInvuln)) return true;
			if (body.HasBuff(waterInvuln)) return true;

			return false;
		}

		private static void ApplyLunarBleed(CharacterBody body, float duration)
		{
			body.AddTimedBuff(RoR2Content.Buffs.Cripple, duration);
			BuffDef lunarBleedBuffDef = DiluvianArtifactContent.Buffs.ZetLunarBleed;
			body.AddTimedBuff(lunarBleedBuffDef, duration);
			RefreshBuffDuration(body, lunarBleedBuffDef, duration);
		}

		private static void RefreshBuffDuration(CharacterBody self, BuffDef buffDef, float duration)
		{
			int count = 0;
			float extraTime = 0f;
			BuffIndex buffIndex = buffDef.buffIndex;

			for (int i = 0; i < self.timedBuffs.Count; i++)
			{
				CharacterBody.TimedBuff timedBuff = self.timedBuffs[i];
				if (timedBuff.buffIndex == buffIndex)
				{
					if (timedBuff.timer > 0.05f)
					{
						timedBuff.timer = duration + extraTime;

						if (count < 8) extraTime += 0.25f;
						else extraTime += 0.125f;

						count++;
					}
				}
			}
		}

		private static void DirectDamage(HealthComponent self, float damage)
		{
			float removeAmount;

			if (self.barrier > 0f)
			{
				removeAmount = (self.barrier < damage) ? self.barrier : damage;
				self.Networkbarrier = Mathf.Max(self.barrier - removeAmount, 0f);
				damage -= removeAmount;
			}

			if (damage > 0 && self.shield > 0f)
			{
				removeAmount = (self.shield < damage) ? self.shield : damage;
				self.Networkshield = Mathf.Max(self.shield - removeAmount, 0f);
				damage -= removeAmount;
			}

			if (damage > 0)
			{
				self.Networkhealth = Mathf.Max(self.health - damage, 1f);
			}
		}



		private static void ResetInstabilityController(SceneDirector sceneDirector)
		{
			InstabilityController.Display.ResetServerSyncTime();
			InstabilityController.Display.SetServerDifficulty(0f);
			InstabilityController.Reset();
			customHaunt = false;
		}
		private static void DisableInstabilityController(Stage stage)
		{
			InstabilityController.Display.ResetServerSyncTime();
			InstabilityController.Display.SetServerDifficulty(0f);
			InstabilityController.Disable();
			customHaunt = false;
		}

		private static void OnRunDestroyed(Run run)
		{
			InstabilityController.Display.SetSyncTime(0f);
			InstabilityController.Display.SetDifficulty(0f);
			InstabilityController.Disable();
			customHaunt = false;
		}
		private static void OnStageStarted(Stage stage)
		{
			InstabilityController.Display.SetSyncTime(0f);
			InstabilityController.Display.SetDifficulty(0f);
			InstabilityController.MarkListsForClearing();
			customHaunt = false;
		}



		private static void OnCardSpawned(SpawnCard.SpawnResult result)
		{
			if (!result.success) return;

			CharacterMaster master = result.spawnedInstance ? result.spawnedInstance.GetComponent<CharacterMaster>() : null;
			if (!master) return;

			CharacterBody body = master.GetBody();
			if (body && body.bodyIndex == InstabilityController.lunarChimeraBodyIndex)
			{
				if (body.teamComponent.teamIndex == TeamIndex.Monster)
				{
					InstabilityController.LunarState.UberifyChimera(master);
				}
			}
		}

		private static void FallenChimeraHook()
		{
			On.RoR2.MapZone.TryZoneStart += (orig, self, collider) =>
			{
				CharacterBody body = collider.GetComponent<CharacterBody>();
				if (body && InstabilityController.LunarState.IsUberChimera(body))
				{
					InstabilityController.LunarState.DelayUpdate();
					body.teamComponent.teamIndex = TeamIndex.Player;
					orig(self, collider);
					body.teamComponent.teamIndex = TeamIndex.Monster;
					return;
				}

				orig(self, collider);
			};
		}

		private static void DamageResistHook()
		{
			IL.RoR2.HealthComponent.TakeDamageProcess += (il) =>
			{
				ILCursor c = new ILCursor(il);

				// find : store damageinfo.damage into variable
				bool found = c.TryGotoNext(
					x => x.MatchLdarg(1),
					x => x.MatchLdfld<DamageInfo>("damage"),
					x => x.MatchStloc(7)
				);

				if (found)
				{
					c.Index += 3;

					c.Emit(OpCodes.Ldloc, 7);
					c.Emit(OpCodes.Ldloc, 2);
					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Func<float, TeamIndex, HealthComponent, float>>((damage, atkTeam, healthComponent) =>
					{
						if (Enabled && atkTeam != TeamIndex.Player)
						{
							CharacterBody self = healthComponent.body;

							if (self && InstabilityController.LunarState.IsUberChimera(self))
							{
								damage *= 0.125f;
							}
							else if (InstabilityController.MeteorState.activated)
							{
								damage *= 0.8f;
							}
						}

						return damage;
					});
					c.Emit(OpCodes.Stloc, 7);
				}
				else
				{
					Debug.LogWarning("ChimeraDamageResistHook Failed");
				}
			};
		}



		private static void MeteorImpactHook()
		{
			IL.RoR2.MeteorStormController.DetonateMeteor += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchDup(),
					x => x.MatchLdarg(1)
				);

				if (found)
				{
					c.Index += 3;

					c.Emit(OpCodes.Dup);
					c.EmitDelegate<Action<Vector3>>((position) =>
					{
						InstabilityController.OnMeteorImpact(position);
					});
				}
				else
				{
					Debug.LogWarning("MeteorImpactHook Failed");
				}
			};
		}

		private static void MeteorDamageTypeHook()
		{
			IL.RoR2.MeteorStormController.DetonateMeteor += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchLdfld<MeteorStormController>("blastDamageType")
				);

				if (found)
				{
					c.Index += 1;

					c.EmitDelegate<Func<DamageTypeCombo, DamageTypeCombo>>((damageType) =>
					{
						if (Enabled && InstabilityController.NovaState.activated) return damageType | DamageType.CrippleOnHit;
						return damageType;
					});
				}
				else
				{
					Debug.LogWarning("MeteorDamageTypeHook Failed");
				}
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
						InstabilityController.Display.SetSyncTime(data.genericFloat);
					}
					else if (data.genericUInt == 3u)
					{
						InstabilityController.Display.SetDifficulty(data.genericFloat);
					}
					else if (data.genericUInt == 4u)
					{
						if (!NetworkServer.active)
						{
							InstabilityController.Display.SetSyncTime(data.genericFloat);
						}
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

				SceneDef sceneDef = SceneCatalog.GetSceneDefForCurrentScene();
				string sceneName = sceneDef ? sceneDef.baseSceneName : "";

				if (sceneName == "moon2")
				{
					customHaunt = Run.instance && Enabled;

					if (customHaunt) InstabilityController.MoonActivation();
				}
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

				InstabilityController.Display.InitializeUI(self);
			};
		}

		private static void HUDUpdateHook()
		{
			On.RoR2.UI.HUD.Update += (orig, self) =>
			{
				orig(self);

				InstabilityController.Display.UpdateUI();
			};
		}
	}
}
