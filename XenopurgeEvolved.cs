using HarmonyLib;
using MelonLoader;
using SaveSystem;
using SpaceCommander;
using SpaceCommander.BattleManagement;
using SpaceCommander.BattleManagement.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using WorldMap;

[assembly: MelonInfo(typeof(XenopurgeEvolved.XenopurgeEvolved), "Xenopurge Evolved", "1.0.0", "Felix Hao")]
[assembly: MelonGame("Traptics", "Xenopurge")]

namespace XenopurgeEvolved
{
    public class XenopurgeEvolved : MelonMod
    {
        public static List<Evolution> existingEvolutions = new List<Evolution>();
        public static List<Evolution> newEvolutions = new List<Evolution>();

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("XenopurgeEvolved initialized!");
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager))]
    [HarmonyPatch("SaveFile")]
    public class SaveFile_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string saveName)
        {
            MelonLogger.Msg("[SaveFile_Patch] Postfix called - Saving evolutions");

            try
            {
                // Serialize the evolutions list
                List<string> evolutionData = new List<string>();
                foreach (Evolution evolution in XenopurgeEvolved.existingEvolutions)
                {
                    if (evolution != null)
                    {
                        string serialized = evolution.GetType().FullName;
                        evolutionData.Add(serialized);
                        MelonLogger.Msg($"[SaveFile_Patch] Serialized evolution: {serialized}");
                    }
                }

                // Save to a separate file in the save folder
                string saveFolderPath = Path.Combine(Application.persistentDataPath, saveName);
                string evolutionsFilePath = Path.Combine(saveFolderPath, "evolutions.json");

                if (!Directory.Exists(saveFolderPath))
                {
                    Directory.CreateDirectory(saveFolderPath);
                }

                string json = SaveLoadUtils.Serialize(evolutionData);
                File.WriteAllText(evolutionsFilePath, json);

                MelonLogger.Msg($"[SaveFile_Patch] Successfully saved {evolutionData.Count} evolutions to {evolutionsFilePath}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[SaveFile_Patch] Error saving evolutions: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");
            }
        }
    }

    [HarmonyPatch(typeof(SaveLoadManager))]
    [HarmonyPatch("LoadFile")]
    public class LoadFile_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string saveName)
        {
            MelonLogger.Msg("[LoadFile_Patch] Postfix called - Loading evolutions");

            try
            {
                // Clear existing evolutions
                XenopurgeEvolved.existingEvolutions.Clear();
                XenopurgeEvolved.newEvolutions.Clear();

                // Load from the evolutions file
                string saveFolderPath = Path.Combine(Application.persistentDataPath, saveName);
                string evolutionsFilePath = Path.Combine(saveFolderPath, "evolutions.json");

                if (!File.Exists(evolutionsFilePath))
                {
                    MelonLogger.Msg("[LoadFile_Patch] No evolutions file found - starting fresh");
                    return;
                }

                string json = File.ReadAllText(evolutionsFilePath);
                List<string> evolutionData = SaveLoadUtils.Deserialize<List<string>>(json);

                foreach (string serialized in evolutionData)
                {
                    Type type = Type.GetType(serialized);
                    Evolution evolution = (Evolution)System.Activator.CreateInstance(type);
                    if (evolution != null)
                    {
                        XenopurgeEvolved.existingEvolutions.Add(evolution);
                        evolution.Activate(); // Re-activate the evolution
                        MelonLogger.Msg($"[LoadFile_Patch] Loaded and activated evolution: {evolution.ToString()}");
                    }
                }

                MelonLogger.Msg($"[LoadFile_Patch] Successfully loaded {XenopurgeEvolved.existingEvolutions.Count} evolutions");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[LoadFile_Patch] Error loading evolutions: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");

                // On error, clear evolutions to prevent issues
                XenopurgeEvolved.existingEvolutions.Clear();
                XenopurgeEvolved.newEvolutions.Clear();
            }
        }
    }

    [HarmonyPatch(typeof(WorldMapGeneratorController), "GenerateMap")]
    public class WorldMapGeneratorController_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(WorldMapGeneratorController __instance)
        {
            MelonLogger.Msg("[WorldMapGeneratorController_Patch] Prefix called - Generating map");
            WorldMapData _worldMapData = (WorldMapData)AccessTools.Field(typeof(WorldMapGeneratorController), "_worldMapData").GetValue(__instance);
            Evolution.random = _worldMapData.Seed != 0 ? new System.Random(_worldMapData.Seed) : new System.Random();
            if (_worldMapData.Path.Count == 0)
            {
                XenopurgeEvolved.existingEvolutions.Clear();
            }
            MelonLogger.Msg("[WorldMapGeneratorController_Patch] Map generation complete");
        }
    }

    [HarmonyPatch(typeof(BattleManagementWindowController))]
    [HarmonyPatch("InitializeBattleManagementController")]
    public class BattleManagementWindowController_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(TestGame testGame, RetreatController retreatController)
        {
            PlayerData playerData = Singleton<Player>.Instance.PlayerData;
            int nUnits = playerData.Squad.SquadUnitsCount;


            XenopurgeEvolved.newEvolutions.Clear();

            int loopCount = 0;
            MelonLogger.Msg("[BattleManagementWindowController_Patch] Prefix called - Assigning evolutions");
            while (nUnits - 1 > XenopurgeEvolved.existingEvolutions.Count)
            {
                MelonLogger.Msg("[BattleManagementWindowController_Patch] Attempting to create new evolution");
                Evolution newEvolution = Evolution.CreateNewEvolution();
                if (newEvolution != null)
                {
                    XenopurgeEvolved.newEvolutions.Add(newEvolution);
                    XenopurgeEvolved.existingEvolutions.Add(newEvolution);
                    newEvolution.Activate();
                }
                else
                {
                    break;
                }
                loopCount++;
            }

            MelonLogger.Msg($"[BattleManagementWindowController_Patch] Assigned {XenopurgeEvolved.newEvolutions.Count} new evolutions in {loopCount} loops");

            return true;
        }
    }

    [HarmonyPatch(typeof(ShowObjective_BattleManagementDirectory))]
    [HarmonyPatch("SetUpObjectivesTexts")]
    public class SetUpObjectivesTexts_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ShowObjective_BattleManagementDirectory __instance)
        {
            MelonLogger.Msg("[SetUpObjectivesTexts_Patch] Postfix called - Updating objectives text");
            try
            {
                // Access the private _rewardText field using reflection
                var rewardTextField = AccessTools.Field(typeof(ShowObjective_BattleManagementDirectory), "_rewardText");
                TextMeshProUGUI rewardText = (TextMeshProUGUI)rewardTextField.GetValue(__instance);

                if (rewardText != null)
                {

                    // Get the current text
                    string currentText = rewardText.text;

                    // Build the evolution list text
                    string evolutionText = "";

                    // Add new evolutions at the top with red "NEW! " prefix
                    int newCount = 0;
                    foreach (Evolution newEvolution in XenopurgeEvolved.newEvolutions)
                    {
                        evolutionText += TextUtils.GetRedText(ModLocalization.Get("New! ")) + newEvolution.ToString() + "\n";
                        newCount++;
                    }
                    MelonLogger.Msg($"[SetUpObjectivesTexts_Patch] Added {newCount} new evolutions to objectives text");
                    // Add existing evolutions (excluding the new ones)
                    List<Evolution> oldEvolutions = XenopurgeEvolved.existingEvolutions
                        .Where(e => !XenopurgeEvolved.newEvolutions.Contains(e))
                        .ToList();

                    // Log each old evolution with Contains check
                    for (int i = 0; i < XenopurgeEvolved.existingEvolutions.Count; i++)
                    {
                        Evolution e = XenopurgeEvolved.existingEvolutions[i];
                        bool isInNew = XenopurgeEvolved.newEvolutions.Contains(e);
                    }

                    int oldCount = 0;
                    foreach (Evolution oldEvolution in oldEvolutions)
                    {
                        evolutionText += oldEvolution.ToString() + "\n";
                        oldCount++;
                    }
                    MelonLogger.Msg($"[SetUpObjectivesTexts_Patch] Added {oldCount} existing evolutions to objectives text");
                    // Combine everything
                    if (!string.IsNullOrEmpty(evolutionText))
                    {
                        string newInfo = TextUtils.GetYellowText(ModLocalization.Get("Evolutions:")) + "\n" + evolutionText.TrimEnd('\n');
                        rewardText.SetText(currentText + "\n\n" + newInfo);

                        // Force update
                        //rewardText.ForceMeshUpdate();
                    }
                    MelonLogger.Msg("[SetUpObjectivesTexts_Patch] Objectives text update complete");
                }
                else
                {
                    MelonLogger.Warning("[SetUpObjectivesTexts_Patch] rewardText is null!");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"[SetUpObjectivesTexts_Patch] Error: {ex.Message}");
                MelonLogger.Error($"Stack trace: {ex.StackTrace}");
            }

        }
    }
}