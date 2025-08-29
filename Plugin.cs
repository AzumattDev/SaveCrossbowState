using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SaveCrossbowState
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class SaveCrossbowStatePlugin : BaseUnityPlugin
    {
        internal const string ModName = "SaveCrossbowState";
        internal const string ModVersion = "1.0.2";
        internal const string Author = "Azumatt";
        private const string ModGUID = $"{Author}.{ModName}";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource SaveCrossbowStateLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        public const string CrossbowLoaded = "crossbowLoaded";
        public const string CrossbowLoadedChanged = "crossbowLoaded_changed";

        public void Awake()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                SaveCrossbowStateLogger.LogWarning("This mod is client-side only and is not needed on a dedicated server. Plugin patches will not be applied.");
                return;
            }

            _harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static bool IsCrossbowItem(ItemDrop.ItemData? possibleCrossbow)
        {
            return possibleCrossbow?.m_shared.m_skillType is Skills.SkillType.Crossbows or Skills.SkillType.ElementalMagic;
        }
    }

    [HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]
    static class ResetLoaded
    {
        static void Prefix(Attack __instance)
        {
            ItemDrop.ItemData? currentWeapon = __instance.m_character.GetCurrentWeapon();
            if (!__instance.m_character.IsPlayer() || __instance.m_character != Player.m_localPlayer || !SaveCrossbowStatePlugin.IsCrossbowItem(currentWeapon)) return;
            currentWeapon!.m_customData[SaveCrossbowStatePlugin.CrossbowLoaded] = "false";
            if (!currentWeapon!.m_customData.TryGetValue(SaveCrossbowStatePlugin.CrossbowLoadedChanged, out string? changed) || !bool.TryParse(changed, out bool isChanged) || !isChanged) return;
            currentWeapon!.m_customData.Remove(SaveCrossbowStatePlugin.CrossbowLoadedChanged);
            currentWeapon!.m_shared.m_attack.m_requiresReload = true;
        }
    }

    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem))]
    static class CheckIfCrossbowWasUnequipped
    {
        static void Prefix(Humanoid __instance, ItemDrop.ItemData? item, bool triggerEquipEffects = true)
        {
            if (!__instance.IsPlayer() || Player.m_localPlayer == null || Player.m_localPlayer.m_isLoading) return;
            if (!SaveCrossbowStatePlugin.IsCrossbowItem(item)) return;
            Player player = (Player)__instance;
            if (!player.IsWeaponLoaded()) return;
            item!.m_customData[SaveCrossbowStatePlugin.CrossbowLoaded] = "true";
            if (!item.m_shared.m_attack.m_requiresReload) return;
            item!.m_customData[SaveCrossbowStatePlugin.CrossbowLoadedChanged] = "true";
            item.m_shared.m_attack.m_requiresReload = false;
        }
    }
}