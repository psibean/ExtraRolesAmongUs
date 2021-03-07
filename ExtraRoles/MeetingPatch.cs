using ExtraRolesMod;
using HarmonyLib;
using Hazel;
using System;
using static ExtraRolesMod.ExtraRoles;
using UnhollowerBaseLib;
using System.Linq;
using UnityEngine;

namespace ExtraRoles
{
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Instance.Method_84))]
    class CalculateResultsPatch
    {
        public static bool Prefix(MeetingHud __instance, ref Il2CppStructArray<byte> __result)
        {

            byte[] array = new byte[11];
            foreach (PlayerVoteArea player in __instance.playerStates)
            {
                if (player.didVote)
                {
                    PlayerControl cpc = PlayerControl.AllPlayerControls.ToArray().First(pc => pc.name.Equals(player.NameText.Text));

                    int num = (int)(player.votedFor + 1);
                    if (num >= 0 && num < array.Length)
                    {
                        byte[] array2 = array;
                        int num2 = num;
                        byte voteImportance = cpc.isPlayerRole("Mayor") ? (byte)2 : (byte)1;
                        array2[num2] += voteImportance;
                    }
                }
            }
            __result = array;
            return false;
        }
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Instance.PopulateResults))]
    class PopulateResultsPatch
    {
        private static void RenderVote(MeetingHud __instance, SpriteRenderer spriteRenderer, Transform parentTransform, int num2)
        {
            spriteRenderer.transform.SetParent(parentTransform);
            spriteRenderer.transform.localPosition = __instance.CounterOrigin + new Vector3(__instance.CounterOffsets.x * (float)num2, 0f, 0f);
            spriteRenderer.transform.localScale = Vector3.zero;
            __instance.StartCoroutine(Effects.Bloop((float)num2 * 0.5f, spriteRenderer.transform, 1f, 0.5f));
        }

        private static void SetSpriteRendererMaterialColors(SpriteRenderer spriteRenderer, int color)
        {
            if (PlayerControl.GameOptions.AnonymousVotes)
                PlayerControl.SetPlayerMaterialColors(Palette.DisabledGrey, spriteRenderer);
            else
                PlayerControl.SetPlayerMaterialColors(color, spriteRenderer);
        }

        private static void SetSpriteRendererMaterialColors(SpriteRenderer spriteRenderer, Color color)
        {
            if (PlayerControl.GameOptions.AnonymousVotes)
                PlayerControl.SetPlayerMaterialColors(Palette.DisabledGrey, spriteRenderer);
            else
                PlayerControl.SetPlayerMaterialColors(color, spriteRenderer);
        }

        public static bool Prefix(MeetingHud __instance, Il2CppStructArray<byte> GPOAEGEJFCK)
        {
            __instance.TitleText.Text = DestroyableSingleton<TranslationController>.Instance.GetString(StringNames.MeetingVotingResults, new Il2CppReferenceArray<Il2CppSystem.Object>(0));
            int num = 0;
            for (int i = 0; i < __instance.playerStates.Length; i++)
            {
                PlayerVoteArea playerVoteArea = __instance.playerStates[i];
                playerVoteArea.ClearForResults();
                int num2 = 0;
                bool addMayorVote = false;
                for (int j = 0; j < __instance.playerStates.Length; j++)
                {
                    PlayerVoteArea playerVoteArea2 = __instance.playerStates[j];
                    byte self = GPOAEGEJFCK[(int)playerVoteArea2.TargetPlayerId];
                    if (!((self & 128) > 0))
                    {
                        GameData.PlayerInfo playerById = GameData.Instance.GetPlayerById((byte)playerVoteArea2.TargetPlayerId);
                        int votedFor = (int)PlayerVoteArea.GetVotedFor(self);
                        SpriteRenderer spriteRenderer = UnityEngine.Object.Instantiate<SpriteRenderer>(__instance.PlayerVotePrefab);
                        Transform parentTransform = null;
                        if (votedFor == (int)playerVoteArea.TargetPlayerId)
                        {
                            addMayorVote = playerById.Object.isPlayerRole("Mayor");
                            if (addMayorVote)
                                System.Console.WriteLine($"Adding mayor vote to ${playerById.Object.name}");
                            parentTransform = playerVoteArea.transform;
                        }
                        else if (i == 0 && votedFor == -1)
                        {
                            parentTransform = __instance.SkippedVoting.transform;
                        }
                        if (parentTransform != null)
                        {
                            SetSpriteRendererMaterialColors(spriteRenderer, (int)playerById.ColorId);
                            RenderVote(__instance, spriteRenderer, parentTransform, num2);
                            num++;
                        }
                    }
                }
                if (addMayorVote)
                {
                    SpriteRenderer spriteRenderer3 = UnityEngine.Object.Instantiate<SpriteRenderer>(__instance.PlayerVotePrefab);
                    SetSpriteRendererMaterialColors(spriteRenderer3, new Color(0.44f, 0.31f, 0.66f, 1f));
                    RenderVote(__instance, spriteRenderer3, playerVoteArea.transform, num2);
                }
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UnityEngine.Object), nameof(UnityEngine.Object.Destroy),
        new[] {typeof(UnityEngine.Object)})]
    class MeetingExiledEnd
    {
        static void Prefix(UnityEngine.Object obj)
        {
            if (ExileController.Instance == null || obj != ExileController.Instance.gameObject)
                return;

            var Officer = Main.Logic.getRolePlayer("Officer");
            if (Officer != null)
                Officer.LastAbilityTime = DateTime.UtcNow;
            if (ExileController.Instance.Field_10 == null ||
                !ExileController.Instance.Field_10._object.isPlayerRole("Joker"))
                return;

            var writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId,
                (byte) CustomRPC.JokerWin, Hazel.SendOption.Reliable, -1);
            AmongUsClient.Instance.FinishRpcImmediately(writer);

            foreach (var player in PlayerControl.AllPlayerControls)
            {
                if (player.isPlayerRole("Joker"))
                    continue;
                player.RemoveInfected();
                player.Die(DeathReason.Exile);
                player.Data.IsDead = true;
                player.Data.IsImpostor = false;
            }

            var joker = Main.Logic.getRolePlayer("Joker").PlayerControl;
            joker.Revive();
            joker.Data.IsDead = false;
            joker.Data.IsImpostor = true;
        }
    }

    [HarmonyPatch(typeof(TranslationController), nameof(TranslationController.GetString),
        new[] {typeof(StringNames), typeof(Il2CppReferenceArray<Il2CppSystem.Object>)})]
    class TranslationPatch
    {
        static void Postfix(ref string __result, StringNames HKOIECMDOKL,
            Il2CppReferenceArray<Il2CppSystem.Object> EBKIKEILMLF)
        {
            if (ExileController.Instance == null || ExileController.Instance.Field_10 == null)
                return;

            switch (HKOIECMDOKL)
            {
                case StringNames.ExileTextPN:
                case StringNames.ExileTextSN:
                {
                        if (ExileController.Instance.Field_10.Object.isPlayerRole("Medic"))
                            __result = ExileController.Instance.Field_10.PlayerName + " was The Medic.";
                        else if (ExileController.Instance.Field_10.Object.isPlayerRole("Engineer"))
                            __result = ExileController.Instance.Field_10.PlayerName + " was The Engineer.";
                        else if (ExileController.Instance.Field_10.Object.isPlayerRole("Officer"))
                            __result = ExileController.Instance.Field_10.PlayerName + " was The Officer.";
                        else if (ExileController.Instance.Field_10.Object.isPlayerRole("Joker"))
                            __result = ExileController.Instance.Field_10.PlayerName + " was The Joker.";
                        else if (ExileController.Instance.Field_10.Object.isPlayerRole("Mayor"))
                            __result = ExileController.Instance.Field_10.PlayerName + " was The Mayor.";
                        else
                            __result = ExileController.Instance.Field_10.PlayerName + " was not The Impostor.";
                    break;
                }
                case StringNames.ImpostorsRemainP:
                case StringNames.ImpostorsRemainS:
                {
                    if (ExileController.Instance.Field_10.Object.isPlayerRole("Joker"))
                        __result = "";
                    break;
                }
            }
        }
    }
}