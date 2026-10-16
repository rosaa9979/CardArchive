using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine.Client
{
    public partial class GameClient
    {
        private readonly HashSet<string> preparedSpawns = new HashSet<string>();
        private readonly HashSet<string> preparedDeaths = new HashSet<string>();

        private readonly OngoingSpawnPresentation ongoingSpawns = new OngoingSpawnPresentation();

        public bool CanShowOngoingFX(Card card) => ongoingSpawns.CanShow(card);

        public void ScheduleBoardSpawnFX(Card card, GameObject prefab, Vector3 position)
        {
            var release = ongoingSpawns.Begin(card);
            FXTool.ScheduleStateFX(prefab, position, ownerPlayerId: card.player_id,
                onPlaybackReady: playback => {
                    if (playback != null) playback.BindOngoingStart(release);
                    else release();
                });
        }

        public bool ConsumeSpawnPresentation(string uid) => preparedSpawns.Remove(uid);
        public bool ConsumeDeathPresentation(string uid) => preparedDeaths.Remove(uid);

        private static Vector3 SlotPosition(Slot slot)
        {
            BSlot boardSlot = BSlot.Get(slot);
            return boardSlot != null ? boardSlot.GetPosition(slot) : Vector3.zero;
        }

        private static Vector3 CardPosition(Card card)
        {
            BoardCard board = BoardCard.Get(card.uid);
            return board != null ? board.transform.position : SlotPosition(card.slot);
        }

        private void PresentAbilityTarget(AbilityData ability, Card caster, Vector3 position)
        {
            if (ability == null || caster == null) return;
            FXTool.ScheduleStateFX(ability.target_fx, position, abilityTarget: true, ownerPlayerId: caster.player_id);
            AfterPresentationCommit(() => AudioTool.Get().PlaySFX("ability_effect", ability.target_audio));
        }

        private void PrepareBoardTransitions(Game next)
        {
            if (game_data == null) return;
            // FX have independent lifetimes. New cards need not exist before their spawn cue,
            // and removing a dead card at Impact cannot destroy its death cue or completion.
            foreach (Player player in next.players)
                foreach (Card card in player.cards_board)
                {
                    if (game_data.GetBoardCard(card.uid) != null || BoardCard.Get(card.uid) != null) continue;
                    PresentationClock.SkipGapForCurrentStep();
                    preparedSpawns.Add(card.uid);
                    var data = card.CardData;
                    ScheduleBoardSpawnFX(card, data.spawn_fx != null ? data.spawn_fx : AssetData.Get().card_spawn_fx,
                        SlotPosition(card.slot));
                }
            foreach (Player player in game_data.players)
                foreach (Card card in player.cards_board)
                {
                    if (next.GetBoardCard(card.uid) != null) continue;
                    Player owner = next.GetPlayer(card.player_id);
                    if (owner.GetHandCard(card.uid) == null && owner.GetDeckCard(card.uid) == null)
                        PresentationClock.SkipGapForCurrentStep();
                    preparedDeaths.Add(card.uid);
                    var data = card.CardData;
                    FXTool.ScheduleStateFX(data.death_fx != null ? data.death_fx : AssetData.Get().card_destroy_fx,
                        CardPosition(card), ownerPlayerId: card.player_id);
                }
        }
    }
}
