using RoR2;
using RoR2.ContentManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TPDespair.DiluvianArtifact
{
	public class DiluvianArtifactContent : IContentPackProvider
	{
		public ContentPack contentPack = new ContentPack();

		public string identifier
		{
			get { return "DiluvianArtifactContent"; }
		}

		public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
		{
			Artifacts.Create();
			Buffs.Create();

			contentPack.artifactDefs.Add(Artifacts.artifactDefs.ToArray());
			contentPack.buffDefs.Add(Buffs.buffDefs.ToArray());

			args.ReportProgress(1f);
			yield break;
		}

		public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
		{
			ContentPack.Copy(contentPack, args.output);
			args.ReportProgress(1f);
			yield break;
		}

		public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
		{
			args.ReportProgress(1f);
			yield break;
		}



		public static class Artifacts
		{
			public static ArtifactDef Diluvifact;
			public static ArtifactDef ZetEclifact;
			public static ArtifactDef ZetUnstabifact;

			public static List<ArtifactDef> artifactDefs = new List<ArtifactDef>();

			public static void Create()
			{
				if (DiluvianArtifactPlugin.DiluvifactEnable.Value == 1)
				{
					Diluvifact = ScriptableObject.CreateInstance<ArtifactDef>();
					Diluvifact.cachedName = "ARTIFACT_DILUVIFACT";
					Diluvifact.nameToken = "ARTIFACT_DILUVIFACT_NAME";
					Diluvifact.descriptionToken = "ARTIFACT_DILUVIFACT_DESC";
					Diluvifact.smallIconSelectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zetdiluvianabstract_selected, Color.magenta);
					Diluvifact.smallIconDeselectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zetdiluvianabstract_deselected, Color.gray);

					artifactDefs.Add(Diluvifact);
				}

				if (DiluvianArtifactPlugin.UnstabifactEnable.Value == 1)
				{
					ZetUnstabifact = ScriptableObject.CreateInstance<ArtifactDef>();
					ZetUnstabifact.cachedName = "ARTIFACT_ZETUNSTABIFACT";
					ZetUnstabifact.nameToken = "ARTIFACT_ZETUNSTABIFACT_NAME";
					ZetUnstabifact.descriptionToken = "ARTIFACT_ZETUNSTABIFACT_DESC";
					ZetUnstabifact.smallIconSelectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zetunstable_selected, Color.magenta);
					ZetUnstabifact.smallIconDeselectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zetunstable_deselected, Color.gray);

					artifactDefs.Add(ZetUnstabifact);
				}

				if (DiluvianArtifactPlugin.EclifactEnable.Value == 1)
				{
					ZetEclifact = ScriptableObject.CreateInstance<ArtifactDef>();
					ZetEclifact.cachedName = "ARTIFACT_ZETECLIFACT";
					ZetEclifact.nameToken = "ARTIFACT_ZETECLIFACT_NAME";
					ZetEclifact.descriptionToken = "ARTIFACT_ZETECLIFACT_DESC";
					ZetEclifact.smallIconSelectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zeteclipse_selected, Color.magenta);
					ZetEclifact.smallIconDeselectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zeteclipse_deselected, Color.gray);

					artifactDefs.Add(ZetEclifact);
				}
			}
		}

		public static class Buffs
		{
			public static BuffDef ZetLunarBleed;

			public static List<BuffDef> buffDefs = new List<BuffDef>();


			public static void Create()
			{
				if (DiluvianArtifactPlugin.UnstabifactEnable.Value > 0)
				{
					ZetLunarBleed = ScriptableObject.CreateInstance<BuffDef>();
					ZetLunarBleed.name = "ZetLunarBleed";
					ZetLunarBleed.buffColor = new Color(0.3f, 0.6f, 1f);
					ZetLunarBleed.canStack = true;
					ZetLunarBleed.isDebuff = true;
					ZetLunarBleed.iconSprite = Resources.Load<Sprite>("Textures/BuffIcons/texBuffBleedingIcon");

					buffDefs.Add(ZetLunarBleed);
				}
			}
		}
	}
}
