using RoR2;
using RoR2.ContentManagement;
using System.Collections;
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

			contentPack.artifactDefs.Add(Artifacts.artifactDefs);
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

			public static ArtifactDef[] artifactDefs;

			public static void Create()
			{
				Diluvifact = ScriptableObject.CreateInstance<ArtifactDef>();
				Diluvifact.cachedName = "ARTIFACT_DILUVIFACT";
				Diluvifact.nameToken = "ARTIFACT_DILUVIFACT_NAME";
				Diluvifact.descriptionToken = "ARTIFACT_DILUVIFACT_DESC";
				Diluvifact.smallIconSelectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zetdiluvianabstract_selected, Color.magenta);
				Diluvifact.smallIconDeselectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zetdiluvianabstract_deselected, Color.gray);

				ZetEclifact = ScriptableObject.CreateInstance<ArtifactDef>();
				ZetEclifact.cachedName = "ARTIFACT_ZETECLIFACT";
				ZetEclifact.nameToken = "ARTIFACT_ZETECLIFACT_NAME";
				ZetEclifact.descriptionToken = "ARTIFACT_ZETECLIFACT_DESC";
				ZetEclifact.smallIconSelectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zeteclipse_selected, Color.magenta);
				ZetEclifact.smallIconDeselectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zeteclipse_deselected, Color.gray);

				ZetUnstabifact = ScriptableObject.CreateInstance<ArtifactDef>();
				ZetUnstabifact.cachedName = "ARTIFACT_ZETUNSTABIFACT";
				ZetUnstabifact.nameToken = "ARTIFACT_ZETUNSTABIFACT_NAME";
				ZetUnstabifact.descriptionToken = "ARTIFACT_ZETUNSTABIFACT_DESC";
				ZetUnstabifact.smallIconSelectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zetunstable_selected, Color.magenta);
				ZetUnstabifact.smallIconDeselectedSprite = DiluvianArtifactPlugin.CreateSprite(Properties.Resources.zetunstable_deselected, Color.gray);

				artifactDefs = new ArtifactDef[] { Diluvifact, ZetEclifact, ZetUnstabifact };
			}
		}
	}
}
