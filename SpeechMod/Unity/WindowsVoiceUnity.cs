using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace SpeechMod.Unity
{
    public class WindowsVoiceUnity : MonoBehaviour
    {
        public static bool IsSpeaking { get; private set; }

        private static string textToSpeak;

        public static void Speak(string text, int length, float delay = 0f)
        {
            Main.Logger?.Warning("Speaking: " + text);
            if (Main.Settings.InterruptPlaybackOnPlay && IsSpeaking)
                Stop();

            textToSpeak = text;
            IsSpeaking = true;
        }

        public static void Stop()
        {
            // Stop the currently running edge-playback process (if any)
            Process[] processes = Process.GetProcessesByName("edge-playback");
            foreach (Process process in processes)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }
            }

            IsSpeaking = false;
        }
    }
}
