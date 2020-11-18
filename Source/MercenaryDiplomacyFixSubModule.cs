using System;
using System.Collections.Generic;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Election;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace MercenaryDiplomacyFix
{
    public class MercenaryDiplomacyFixSubModule : MBSubModuleBase
    {
        private static Harmony harmonyInstance = new Harmony("MercenaryDiplomacyFixSubModule.Harmony");

        public override void OnCampaignStart(Game game, object starterObject)
        {
            base.OnCampaignStart(game, starterObject);

            Debug.Print("MercenaryDiplomacyFixSubModule.OnCampaignStart");
        }

        public override void OnGameEnd(Game game)
        {
            base.OnGameEnd(game);
            Debug.Print("MercenaryDiplomacyFixSubModule.OnGameEnd");
        }

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            Debug.Print("MercenaryDiplomacyFixSubModule.OnSubModuleLoad");

            harmonyInstance.PatchAll();
        }
    }


    // Fix the issue where if the player is a mercenary for a kingdom, the kingdom tries to involve the player in decisions instead of immediately deciding as usual
    [HarmonyPatch(typeof(Kingdom), "AddDecision")]
    public class MercenaryDecisionsFixPatch
    {
        // Used to check if a decision was added
        static int PreviousCount;

        static void Prefix(Kingdom __instance, List<KingdomDecision> ____unresolvedDecisions)
        {
            PreviousCount = ____unresolvedDecisions.Count;
        }

        static void Postfix(Kingdom __instance, ref List<KingdomDecision> ____unresolvedDecisions)
        {
            if (PreviousCount == ____unresolvedDecisions.Count)
            {
                // no change
                return;
            }

            KingdomDecision newKingdomDecision = ____unresolvedDecisions[____unresolvedDecisions.Count - 1];

            // If the player is a mercenary of the kingdom, then immediately resolve without player input.
            // Note that the original code uses "IsClanTypeMercenary" which is never true for the player. The player tracks this through "IsUnderMercenaryService."
            if (newKingdomDecision.Kingdom == Clan.PlayerClan.Kingdom && Clan.PlayerClan.IsUnderMercenaryService)
            {
                Debug.Print("MercenaryDiplomacyFix.MercenaryDecisionsFixPatch.Kingdom.AddDecision.Postfix: Forced decision to complete without player, who is a mercenary.");

                new KingdomElection(newKingdomDecision).StartElectionWithoutPlayer();

                // ApplyChosenOutcome will remove decision for us so no need for manual removal
            }
        }
    }

    // Fix the issue where if the player allows a kingdom decision to time out where their input isn't required, it is not automatically resolved without the player, but instead canceled
    [HarmonyPatch(typeof(KingdomDecisionProposalBehavior), "UpdateKingdomDecisions")]
    public class TimeOutDecisionsFixPatch
    {
        static void Prefix(Kingdom kingdom)
        {
            if (kingdom != Clan.PlayerClan.Kingdom)
            {
                // Ignore non-player kingdom
                return;
            }

            List<KingdomDecision> timedOutKingdomDecisionList = new List<KingdomDecision>();

            foreach (KingdomDecision unresolvedDecision in (IEnumerable<KingdomDecision>)kingdom.UnresolvedDecisions)
            {
                if (unresolvedDecision.ShouldBeCancelled())
                {
                    // Original function will do the cancel
                    continue;
                }
                else if (unresolvedDecision.TriggerTime.IsPast && !unresolvedDecision.NeedsPlayerResolution)
                {
                    // Decision has timed out
                    timedOutKingdomDecisionList.Add(unresolvedDecision);
                }
            }

            foreach (KingdomDecision kingdomDecision in timedOutKingdomDecisionList)
            {
                Debug.Print("MercenaryDiplomacyFix.TimeOutDecisionsFixPatch.KingdomDecisionProposalBehavior.UpdateKingdomDecisions.Prefix: Timed out decision election started without player.");

                // Compete the decision without the player
                new KingdomElection(kingdomDecision).StartElectionWithoutPlayer();

                // ApplyChosenOutcome will remove decision for us so no need for manual removal
            }
        }
    }
}


