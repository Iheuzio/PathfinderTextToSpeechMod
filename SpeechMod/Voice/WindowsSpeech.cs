using System;
using System.Linq;
using System.Text.RegularExpressions;
using Kingmaker;
using Kingmaker.Blueprints;
using SpeechMod.Unity;
using System.Collections.Generic;
using System.Diagnostics;
using UniRx.Diagnostics;

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
        text = match.Groups[1].Value;
        //Thread the process so that the program does not wait until it is done running
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            Process process = new Process();
            process.StartInfo.FileName = "edge-playback";
            text = text.Replace("~", "");
            text = text.Replace("-", "");
            text = text.Replace("_", "");
            text = text.Replace(":", "\\:");
            text = text.Replace("/", "\\/");
            process.StartInfo.Arguments = $"-v \"en-IE-EmilyNeural\" -t \"{text}\"";
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
        });
#if DEBUG
        if (Assembly.GetEntryAssembly() == null)
            Main.Logger?.Warning("Invalid " + text);
        UnityEngine.Debug.Log(text);
#endif
        return text;
    }

    private string GetEdgePlaybackCommand(string text)
    {
        string command = $"/c start /b edge-playback -v \"en-IE-EmilyNeural\" -t \"{text}\"";
        Process process = new Process();
        process.StartInfo.FileName = "cmd.exe";
        process.StartInfo.Arguments = $"/c set edgeplayback \"C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe\" \"/profile-directory=Default\" & set edgeplayback & {command}";
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        string[] outputlines = output.Split('\n');
        string cmd = outputlines[4].TrimEnd('\r');

        return cmd + " && " + command;
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
