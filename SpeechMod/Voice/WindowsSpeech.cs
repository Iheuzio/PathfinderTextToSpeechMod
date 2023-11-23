﻿using System;
using System.Linq;
using System.Text.RegularExpressions;
using Kingmaker;
using Kingmaker.Blueprints;
using SpeechMod.Unity;
using System.Collections.Generic;
using System.Diagnostics;
using UniRx.Diagnostics;
using System.Threading;
using System.IO;


#if DEBUG
using System.Reflection;
#endif

namespace SpeechMod.Voice;

public class WindowsSpeech : ISpeech
{
    private static string NarratorVoice => $"<voice required=\"Name={Main.NarratorVoice}\">";
    private static string NarratorPitch => $"<pitch absmiddle=\"{Main.Settings.NarratorPitch}\"/>";
    private static string NarratorRate => $"<rate absspeed=\"{Main.Settings.NarratorRate}\"/>";
    private static string NarratorVolume => $"<volume level=\"{Main.Settings.NarratorVolume}\"/>";

    private static string FemaleVoice => $"<voice required=\"Name={Main.FemaleVoice}\">";
    private static string FemaleVolume => $"<volume level=\"{Main.Settings.FemaleVolume}\"/>";
    private static string FemalePitch => $"<pitch absmiddle=\"{Main.Settings.FemalePitch}\"/>";
    private static string FemaleRate => $"<rate absspeed=\"{Main.Settings.FemaleRate}\"/>";

    private static string MaleVoice => $"<voice required=\"Name={Main.MaleVoice}\">";
    private static string MaleVolume => $"<volume level=\"{Main.Settings.MaleVolume}\"/>";
    private static string MalePitch => $"<pitch absmiddle=\"{Main.Settings.MalePitch}\"/>";
    private static string MaleRate => $"<rate absspeed=\"{Main.Settings.MaleRate}\"/>";

    public string CombinedNarratorVoiceStart => $"{NarratorVoice}{NarratorPitch}{NarratorRate}{NarratorVolume}";
    public string CombinedFemaleVoiceStart => $"{FemaleVoice}{FemalePitch}{FemaleRate}{FemaleVolume}";
    public string CombinedMaleVoiceStart => $"{MaleVoice}{MalePitch}{MaleRate}{MaleVolume}";

    public virtual string CombinedDialogVoiceStart
    {
        get
        {
            if (Game.Instance?.DialogController?.CurrentSpeaker == null)
                return CombinedNarratorVoiceStart;

            return Game.Instance.DialogController.CurrentSpeaker.Gender switch
            {
                Gender.Female => CombinedFemaleVoiceStart,
                Gender.Male => CombinedMaleVoiceStart,
                _ => CombinedNarratorVoiceStart
            };
        }
    }

    public static int Length(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var arr = new[] { "—", "-", "\"" };

        return arr.Aggregate(text, (current, t) => current.Replace(t, "")).Length;
    }

    private string FormatGenderSpecificVoices(string text)
    {
        text = text.Replace("<color=#616060>", $"</voice>{CombinedNarratorVoiceStart}");
        text = text.Replace("</color>", $"</voice>{CombinedDialogVoiceStart}");

        if (text.StartsWith("</voice>"))
            text = text.Remove(0, 8);
        else
            text = CombinedDialogVoiceStart + text;

        if (text.EndsWith(CombinedDialogVoiceStart))
            text = text.Remove(text.Length - CombinedDialogVoiceStart.Length, CombinedDialogVoiceStart.Length);

        if (!text.EndsWith("</voice>"))
            text += "</voice>";
        return text;
    }

    public void SpeakPreview(string text, VoiceType voiceType)
    {
        if (string.IsNullOrEmpty(text))
        {
            Main.Logger?.Warning("No text to speak!");
            return;
        }

        text = text.PrepareText();
        text = new Regex("<[^>]+>").Replace(text, "");

        switch (voiceType)
        {
            case VoiceType.Narrator:
                text = $"{CombinedNarratorVoiceStart}{text}</voice>";
                break;
            case VoiceType.Female:
                text = $"{CombinedFemaleVoiceStart}{text}</voice>";
                break;
            case VoiceType.Male:
                text = $"{CombinedMaleVoiceStart}{text}</voice>";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(voiceType), voiceType, null);
        }

        WindowsVoiceUnity.Speak(text, Length(text));
    }

    private string[] SplitText(string text, int wordsPerFile)
    {
        // Your text processing logic here to prepare the text as needed

        // Split text into an array based on wordsPerFile
        string[] words = text.Split(' ');
        int totalFiles = (int)Math.Ceiling((double)words.Length / wordsPerFile);
        string[] textArray = new string[totalFiles];

        for (int i = 0; i < totalFiles; i++)
        {
            int startIndex = i * wordsPerFile;
            int endIndex = Math.Min((i + 1) * wordsPerFile, words.Length);
            textArray[i] = string.Join(" ", words, startIndex, endIndex - startIndex);
        }

        return textArray;
    }

    public string PrepareSpeechText(string text)
    {
#if DEBUG
        Main.Logger?.Log("Enter 1");
        UnityEngine.Debug.Log(text);
#endif
        text = new Regex("<[^>]+>").Replace(text, "");
        text = text.PrepareText();
        text = $"{CombinedNarratorVoiceStart}{text}</voice>";

        string pattern = @"(?<=\/>|>)([^<]+)";
        Regex regex = new Regex(pattern);
        Match match = regex.Match(text);
        var allText = match.Groups;
        // combine all text into one string
        text = "";
        foreach (var item in allText)
        {
            text += item;
        }
        // if process is already running, wait for it to finish
        // otherwise, start a new process
        ThreadPool.QueueUserWorkItem(delegate
        {
            ProcessText(text);
            PlayFile("file.mp3");
            DeleteFile("file.mp3");
        });
#if DEBUG
        if (System.Reflection.Assembly.GetEntryAssembly() == null)
            Main.Logger?.Warning("Invalid " + text);
        UnityEngine.Debug.Log(text);
#endif
        return text;
    }

    private void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
           using Process process = new Process();
            {
                File.Delete(filePath);
            }
        }
        Main.Logger?.Log("Deleted file: " + filePath);
    }

    private void PlayFile(string filePath)
    {
       // check if file exists
       if (!File.Exists(filePath))
        {
            Main.Logger?.Warning("File does not exist: " + filePath);
            return;
        }

        // play the file using c# library
        using (Process process = new Process())
        {
            process.StartInfo.FileName = "powershell";
            process.StartInfo.Arguments = $"mpv {filePath}";
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            process.Start();
            process.WaitForExit();
            process.Close();
        }
    }

    private void ProcessText(string text)
    {
        // Your text processing logic here

        // Use fileIndex to generate unique file names
        //string fileName = $"file{fileIndex + 1}.mp3";
       string fileName = "file.mp3";

        using (Process process = new Process())
        {
            process.StartInfo.FileName = "edge-tts";
            // Your other process setup logic here

            process.StartInfo.Arguments = $"-v \"en-IE-EmilyNeural\" -t \"{text}\" --write-media {fileName}";
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
            process.Start();
            process.WaitForExit();
            process.Close();
        }
    }

    public string PrepareDialogText(string text)
    {
        text = text.PrepareText();

        text = new Regex("<b><color[^>]+><link([^>]+)?>([^<>]*)</link></color></b>").Replace(text, "$2");

#if DEBUG
        if (Assembly.GetEntryAssembly() == null)
            UnityEngine.Debug.Log(text);
#endif

        text = FormatGenderSpecificVoices(text);

#if DEBUG
        if (Assembly.GetEntryAssembly() == null)
            UnityEngine.Debug.Log(text);
#endif

        return text;
    }

    public void SpeakDialog(string text, float delay = 0f)
    {
        if (string.IsNullOrEmpty(text))
        {
            Main.Logger?.Warning("No text to speak!");
            return;
        }

        if (!Main.Settings.UseGenderSpecificVoices)
        {
            Speak(text, delay);
            return;
        }

        text = PrepareDialogText(text);

        WindowsVoiceUnity.Speak(text, Length(text), delay);
    }

    public void Speak(string text, float delay = 0f)
    {
        if (string.IsNullOrEmpty(text))
        {
            Main.Logger?.Warning("No text to speak!");
            return;
        }

        text = PrepareSpeechText(text);

        WindowsVoiceUnity.Speak(text, Length(text), delay);
    }

    public void Stop()
    {
        WindowsVoiceUnity.Stop();
    }

    public string GetStatusMessage()
    {
        if (WindowsVoiceUnity.IsSpeaking)
        {
            return "Speaking";
        }
        else
        {
            try
            {
                Process[] processes = Process.GetProcessesByName("edge-playback");
                if (processes.Length > 0)
                {
                    return "Playing (edge-playback)";
                }
                else
                {
                    return "Ready";
                }
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }
    }

    public string[] GetAvailableVoices()
    {
        try
        {
            string voicesList = RunEdgePlaybackCommand("--list-voices");
            List<string> availableVoices = new List<string>();
            Main.Logger?.Log(voicesList);

            // Split the output by lines
            string[] lines = voicesList.Split('\n');
            string currentVoice = null;

            foreach (string line in lines)
            {
                if (line.StartsWith("Name: "))
                {
                    currentVoice = line.Replace("Name: ", "").Trim();
                }
                else if (line.StartsWith("Gender: "))
                {
                    string gender = line.Replace("Gender: ", "").Trim();
                    if (currentVoice != null)
                    {
                        availableVoices.Add($"{currentVoice}#{gender}");
                    }
                }
            }

            return availableVoices.ToArray();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting available voices: {ex}");
            return new string[0];
        }
    }

    private static string RunEdgePlaybackCommand(string arguments)
    {
        try
        {
            string output = "Name: en-IE-EmilyNeural\nGender: Female";

            return output;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error running edge-playback command: {ex}");
            return string.Empty;
        }
    }
}
