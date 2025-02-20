﻿using HarmonyLib;
using SpeechMod.Unity;
using SpeechMod.Voice;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityModManagerNet;

namespace SpeechMod
{
#if DEBUG
    [EnableReloading]
#endif
    public static class Main
    {
        public static UnityModManager.ModEntry.ModLogger Logger;
        public static Settings Settings;
        public static bool Enabled;

        public static string[] FontStyleNames = Enum.GetNames(typeof(FontStyles));

        public static string NarratorVoice => VoicesDict?.ElementAtOrDefault(Settings.NarratorVoice).Key;
        public static string FemaleVoice => VoicesDict?.ElementAtOrDefault(Settings.FemaleVoice).Key;
        public static string MaleVoice => VoicesDict?.ElementAtOrDefault(Settings.MaleVoice).Key;

        public static Dictionary<string, string> VoicesDict => Settings?.AvailableVoices?.Select(v =>
        {
            var splitV = v?.Split('#');
            return splitV.Length != 2
                ? new { Key = v, Value = "Unknown" }
                : new { Key = splitV[0], Value = splitV[1] };
        }).ToDictionary(p => p.Key, p => p.Value);

        public static ISpeech Speech;
        private static bool m_Loaded = false;

        private static bool Load(UnityModManager.ModEntry modEntry)
        {
            Debug.Log("Speech Mod Initializing...");

            Logger = modEntry.Logger;

            if (!SetSpeech())
            {
                Logger.Log("Failed to set speech");
                return false;
            }

            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            MenuGUI.UpdateColors();

            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGui;
            modEntry.OnSaveGUI = OnSaveGui;

            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Logger.Log(Speech.GetStatusMessage());

            if (!SetAvailableVoices())
            {
                Logger.Log("Failed to set Voices");
                return false;
            }

            SpeechExtensions.LoadDictionary();

            Debug.Log("Speech Mod Initialized!");
            m_Loaded = true;
            return true;
        }

        private static bool SetAvailableVoices()
        {
            if (Speech == null)
            {
                Logger.Warning("Speech is null!");
                return false;
            }
            var availableVoices = Speech?.GetAvailableVoices();

            if (availableVoices == null || availableVoices.Length == 0)
            {
                Logger.Warning("No available voices found! Disabling mod!");
                return false;
            }

            Logger.Log("Setting available voices list...");

            // You don't need to manually populate available voices here.

            return true;
        }

        private static bool SetSpeech()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXPlayer:
                    Speech = new AppleSpeech();
                    SpeechExtensions.AddUiElements<AppleVoiceUnity>(Constants.APPLE_VOICE_NAME);
                    break;
                case RuntimePlatform.WindowsPlayer:
                    Speech = new WindowsSpeech();
                    SpeechExtensions.AddUiElements<WindowsVoiceUnity>(Constants.WINDOWS_VOICE_NAME);
                    break;
                default:
                    Logger.Critical($"SpeechMod is not supported on {Application.platform}!");
                    return false;
            }

            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            Enabled = value;
            return true;
        }

        private static void OnGui(UnityModManager.ModEntry modEntry)
        {
            if (m_Loaded)
                MenuGUI.OnGui();
        }

        private static void OnSaveGui(UnityModManager.ModEntry modEntry)
        {
            MenuGUI.UpdateColors();
            Settings.Save(modEntry);
        }
    }
}
