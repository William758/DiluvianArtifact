using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace TPDespair.DiluvianArtifact
{
	public static class Diluvifact
	{
		private static int state = 0;
		private static float difficultyMult = 1f;

		public static bool Enabled
		{
			get
			{
				if (state < 1) return false;
				else if (state > 1) return true;
				else
				{
					if (RunArtifactManager.instance && RunArtifactManager.instance.IsArtifactEnabled(DiluvianArtifactContent.Artifacts.Diluvifact)) return true;

					return false;
				}
			}
		}



		internal static void Init()
		{
			state = DiluvianArtifactPlugin.DiluvifactEnable.Value;
			if (state < 1) return;

			difficultyMult = DiluvianArtifactPlugin.DiluvifactDifficulty.Value;

			DiluvianArtifactPlugin.RegisterLanguageToken("ARTIFACT_DILUVIFACT_NAME", "Artifact of Diluvian");
			DiluvianArtifactPlugin.RegisterLanguageToken("ARTIFACT_DILUVIFACT_DESC", "Enables all Diluvian modifiers.\n" + GetDifficultyMultText() + "\n<style=cStack>>Ally Healing: <style=cDeath>-20%</style>\n>Ally Block Chance: <style=cDeath>Unlucky</style>\n>Oneshot Protection: <style=cDeath>Disabled</style>\n>Elite Cost: <style=cDeath>-20%</style>\n>Enemies <style=cDeath>cannot be stunned</style> from damage taken.\n>Enemies <style=cDeath>regenerate 2% HP/s</style> outside of combat.\n>Blood Shrines <style=cDeath>disable healing</style> for <style=cDeath>8s</style>.</style>");

			SetupSyzygyTokens();

			if (difficultyMult > 1f) DifficultyHook();
			HealHook();
			BlockHook();
			OneshotHook();
			EliteCostHook();
			HitStunHook();
			MonsterRegenHook();
			BloodShrineHook();
		}

		private static string GetDifficultyMultText()
		{
			if (difficultyMult > 1f) return "\n<style=cStack>>Difficulty Multiplier: <style=cDeath>+" + ((difficultyMult - 1f) * 100f).ToString("0.##") + "%</style>";
			return "";
		}



		private static void SetupSyzygyTokens()
		{
			DiluvianArtifactPlugin.RegisterSyzygyToken("MSOBELISK_CONTEXT", "Escape the madness.");
			DiluvianArtifactPlugin.RegisterSyzygyToken("MSOBELISK_CONTEXT_CONFIRMATION", "Take the cowards way out.");

			DiluvianArtifactPlugin.RegisterSyzygyToken("COST_PERCENTHEALTH_FORMAT", "?");//hide the cost for bloodshrines.
			DiluvianArtifactPlugin.RegisterSyzygyToken("SHRINE_BLOOD_USE_MESSAGE_2P", "<color=#707>Look, it hurt itself.</color><style=cShrine> You have gained {1} gold.</color>");
			DiluvianArtifactPlugin.RegisterSyzygyToken("SHRINE_BLOOD_USE_MESSAGE", "<color=#401>{0} has shown us their blood. </color><style=cShrine> They gained {1} gold.</color>");
			DiluvianArtifactPlugin.RegisterSyzygyToken("SHRINE_HEALING_USE_MESSAGE_2P", "<color=#405>A beacon?</color><color=#840> To the woods. </color><color=#046>Primitive.</color> <color=#000>Effective.</color>");
			DiluvianArtifactPlugin.RegisterSyzygyToken("SHRINE_HEALING_USE_MESSAGE", "<color=#055>A haven for {0}.</color><color=#800> Curious.</color>");
			DiluvianArtifactPlugin.RegisterSyzygyToken("SHRINE_BOSS_BEGIN_TRIAL", "<color=#061>Time to see what their mettle is worth.</color>");
			DiluvianArtifactPlugin.RegisterSyzygyToken("SHRINE_BOSS_END_TRIAL", "<color=#204> And our test goes on.</color>");
			DiluvianArtifactPlugin.RegisterSyzygyToken("SHRINE_RESTACK_USE_MESSAGE_2P", "<color=#333>Refined.<color=#008> Sharpened.<color=#000> Aligned.");
			DiluvianArtifactPlugin.RegisterSyzygyToken("SHRINE_RESTACK_USE_MESSAGE", "<color=#248>{0} is one step closer.</color>");
			DiluvianArtifactPlugin.RegisterSyzygyToken("SHRINE_COMBAT_USE_MESSAGE_2P", "<color=#420>The threat widens. <color=#014>The struggle tightens.");
			DiluvianArtifactPlugin.RegisterSyzygyToken("SHRINE_COMBAT_USE_MESSAGE", "<color=#040>{0} is acting odd.<color=#804> Inviting {1}s, vexing.");

			DiluvianArtifactPlugin.RegisterSyzygyToken("PAUSE_RESUME", "Continue");
			DiluvianArtifactPlugin.RegisterSyzygyToken("PAUSE_SETTINGS", "Calibrate your senses");
			DiluvianArtifactPlugin.RegisterSyzygyToken("PAUSE_QUIT_TO_MENU", "Stop the simulation");
			DiluvianArtifactPlugin.RegisterSyzygyToken("PAUSE_QUIT_TO_DESKTOP", "End the universe");
			DiluvianArtifactPlugin.RegisterSyzygyToken("QUIT_RUN_CONFIRM_DIALOG_BODY_SINGLEPLAYER", "Boring.");
			DiluvianArtifactPlugin.RegisterSyzygyToken("QUIT_RUN_CONFIRM_DIALOG_BODY_CLIENT", "A figment of our imagination. It will continue without you.");
			DiluvianArtifactPlugin.RegisterSyzygyToken("QUIT_RUN_CONFIRM_DIALOG_BODY_HOST", "With you, this world ends. The others aren't real.");

			DiluvianArtifactPlugin.RegisterSyzygyToken("OBJECTIVE_FIND_TELEPORTER", "Continue the simulation.");
			DiluvianArtifactPlugin.RegisterSyzygyToken("OBJECTIVE_DEFEAT_BOSS", "Complete the Act!");

			DiluvianArtifactPlugin.RegisterSyzygyToken("STAT_KILLER_NAME_FORMAT", "Simulation ended by: <color=#FFFF7F>{0}</color>.");
			DiluvianArtifactPlugin.RegisterSyzygyToken("STAT_POINTS_FORMAT", "");//Delete points
			DiluvianArtifactPlugin.RegisterSyzygyToken("STAT_TOTAL", "");//delete more points

			DiluvianArtifactPlugin.RegisterSyzygyToken("STATNAME_TOTALTIMEALIVE", "Screentime");
			DiluvianArtifactPlugin.RegisterSyzygyToken("STATNAME_TOTALDEATHS", "Stepped out");
			DiluvianArtifactPlugin.RegisterSyzygyToken("STATNAME_HIGHESTLEVEL", "Steps taken");
			DiluvianArtifactPlugin.RegisterSyzygyToken("STATNAME_TOTALGOLDCOLLECTED", "Wealth gotten");
			DiluvianArtifactPlugin.RegisterSyzygyToken("STATNAME_TOTALITEMSCOLLECTED", "Props worn");
			DiluvianArtifactPlugin.RegisterSyzygyToken("STATNAME_TOTALSTAGESCOMPLETED", "Acts progressed");
			DiluvianArtifactPlugin.RegisterSyzygyToken("STATNAME_TOTALPURCHASES", "Assets acquired");

			DiluvianArtifactPlugin.RegisterSyzygyToken("GAME_RESULT_LOST", "DISAPPOINTING.");
			DiluvianArtifactPlugin.RegisterSyzygyToken("GAME_RESULT_WON", "TIME REPEATS.");
			DiluvianArtifactPlugin.RegisterSyzygyToken("GAME_RESULT_UNKNOWN", "WELCOME.");
		}



		private static void DifficultyHook()
		{
			IL.RoR2.Run.RecalculateDifficultyCoefficentInternal += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStloc(9)
				);

				if (found)
				{
					c.Index += 1;

					c.EmitDelegate<Func<float>>(() =>
					{
						if (Enabled) return difficultyMult;

						return 1f;
					});

					c.Emit(OpCodes.Dup);
					c.Emit(OpCodes.Ldloc, 7);
					c.Emit(OpCodes.Mul);
					c.Emit(OpCodes.Stloc, 7);
					c.Emit(OpCodes.Ldloc, 8);
					c.Emit(OpCodes.Mul);
					c.Emit(OpCodes.Stloc, 8);
				}
				else
				{
					Debug.LogWarning("DifficultyHook Failed");
				}
			};
		}

		private static void HealHook()
		{
			On.RoR2.HealthComponent.Heal += (orig, self, amount, procChainMask, nonRegen) =>
			{
				if (NetworkServer.active && Enabled)
				{
					if (self.body && self.body.teamComponent.teamIndex == TeamIndex.Player)
					{
						if (!(self.currentEquipmentIndex == RoR2Content.Equipment.LunarPotion.equipmentIndex && !procChainMask.HasProc(ProcType.LunarPotionActivation)))
						{
							amount *= 0.8f;
						}
					}
				}

				return orig(self, amount, procChainMask, nonRegen);
			};
		}

		private static void BlockHook()
		{
			IL.RoR2.HealthComponent.TakeDamage += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchCall("RoR2.Util", "ConvertAmplificationPercentageIntoReductionPercentage"),
					x => x.MatchLdcR4(0f),
					x => x.MatchLdnull(),
					x => x.MatchCall("RoR2.Util", "CheckRoll")
				);

				if (found)
				{
					c.Index += 2;

					c.Emit(OpCodes.Pop);
					c.Emit(OpCodes.Ldarg, 0);
					c.EmitDelegate<Func<HealthComponent, float>>((healthComponent) =>
					{
						if (Enabled)
						{
							if (healthComponent.body && healthComponent.body.teamComponent.teamIndex == TeamIndex.Player) return -1f;
						}

						return 0f;
					});
				}
				else
				{
					Debug.LogWarning("BlockHook Failed");
				}
			};
		}

		private static void OneshotHook()
		{
			On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
			{
				orig(self);

				if (Enabled)
				{
					self.hasOneShotProtection = false;
					self.oneShotProtectionFraction = 0f;
				}
			};
		}

		private static void EliteCostHook()
		{
			IL.RoR2.CombatDirector.AttemptSpawnOnTarget += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchConvR4(),
					x => x.MatchLdarg(0),
					x => x.MatchLdfld<CombatDirector>("currentActiveEliteTier"),
					x => x.MatchLdfld(typeof(CombatDirector.EliteTierDef).GetField("costMultiplier")),
					x => x.MatchMul(),
					x => x.MatchConvI4(),
					x => x.MatchStloc(1)
				);

				if (found)
				{
					c.Index += 4;

					c.EmitDelegate<Func<float, float>>((mult) =>
					{
						if (Run.instance)
						{
							if (Enabled)
							{
								float extraCost = mult - 1f;
								mult -= extraCost * 0.2f;
							}
						}

						return mult;
					});
				}
				else
				{
					Debug.LogWarning("EliteCostHook Failed");
				}
			};
		}

		private static void HitStunHook()
		{
			On.RoR2.SetStateOnHurt.Start += (orig, self) =>
			{
				orig(self);

				if (Enabled) self.canBeHitStunned = false;
			};
		}

		private static void MonsterRegenHook()
		{
			On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
			{
				orig(self);

				if (Enabled)
				{
					if (self.outOfDanger && self.teamComponent.teamIndex == TeamIndex.Monster)
					{
						if (!self.HasBuff(RoR2Content.Buffs.HiddenInvincibility) && !self.HasBuff(RoR2Content.Buffs.Immune))
						{
							if (self.baseNameToken != "ARTIFACTSHELL_BODY_NAME" && self.baseNameToken != "TITANGOLD_BODY_NAME")
							{
								self.regen += self.maxHealth * 0.02f;
							}
						}
					}
				}
			};
		}

		private static void BloodShrineHook()
		{
			IL.RoR2.ShrineBloodBehavior.AddShrineStack += (il) =>
			{
				ILCursor c = new ILCursor(il);

				bool found = c.TryGotoNext(
					x => x.MatchStloc(0)
				);

				if (found)
				{
					c.Index += 1;

					c.Emit(OpCodes.Ldloc, 0);
					c.EmitDelegate<Action<CharacterBody>>((self) =>
					{
						if (Enabled)
						{
							if (self) self.AddTimedBuff(RoR2Content.Buffs.HealingDisabled, 8f);
						}
					});
				}
				else
				{
					Debug.LogWarning("BloodShrineHook Failed");
				}
			};
		}
	}
}
