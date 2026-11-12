using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Speech.Synthesis;
using System.Windows.Media;

namespace ChargerDrama
{
    // Plays voice clips (mp3/wav/m4a via Windows Media), the Windows voice, and sound effects.
    // Create and use it on the UI thread.
    internal sealed class AudioPlayer : IDisposable
    {
        private readonly MediaPlayer media = new MediaPlayer();
        private readonly SpeechSynthesizer synth = new SpeechSynthesizer();
        private SoundPlayer fx;
        private bool mediaPlaying;
        private DateTime mediaStarted;
        private DateTime fxUntil = DateTime.MinValue;

        public AudioPlayer()
        {
            media.Volume = 1.0;
            media.MediaEnded += delegate { mediaPlaying = false; };
            media.MediaFailed += delegate(object s, ExceptionEventArgs e)
            {
                mediaPlaying = false;
                AppPaths.LogError("MediaFailed", e.ErrorException);
            };
            synth.Volume = 100;
            try
            {
                var english = synth.GetInstalledVoices().FirstOrDefault(v => v.Enabled && v.VoiceInfo.Culture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase));
                if (english != null) synth.SelectVoice(english.VoiceInfo.Name);
                RobotVoice.Language = synth.Voice.Culture.Name;
            }
            catch (Exception ex) { AppPaths.LogError("Select voice", ex); }
        }

        // True while a voice line is playing (used for the mascot's moving mouth).
        public bool IsTalking
        {
            get
            {
                if (mediaPlaying && (DateTime.UtcNow - mediaStarted).TotalSeconds > 30) mediaPlaying = false; // safety net
                return mediaPlaying || synth.State == SynthesizerState.Speaking;
            }
        }

        public bool IsBusy { get { return IsTalking || DateTime.UtcNow < fxUntil; } }

        public void Play(Clip clip)
        {
            StopVoice();
            try
            {
                if (clip.File != null) PlayFile(clip.File);
                else if (clip.Ssml != null) synth.SpeakSsmlAsync(clip.Ssml);
            }
            catch (Exception ex) { AppPaths.LogError("Play " + clip.File, ex); }
        }

        public void PlayFile(string path)
        {
            StopVoice();
            media.Open(new Uri(path));
            media.Play();
            mediaPlaying = true;
            mediaStarted = DateTime.UtcNow;
        }

        public void PlayFx(byte[] wav)
        {
            try
            {
                if (fx != null) fx.Stop();
                fx = new SoundPlayer(new MemoryStream(wav));
                fx.Play();
                // 44-byte header, 16-bit mono 44.1 kHz
                fxUntil = DateTime.UtcNow.AddSeconds((wav.Length - 44) / 88200.0);
            }
            catch (Exception ex) { AppPaths.LogError("PlayFx", ex); }
        }

        public void StopVoice()
        {
            try
            {
                synth.SpeakAsyncCancelAll();
                media.Stop();
                media.Close();
            }
            catch { }
            mediaPlaying = false;
        }

        public void Dispose()
        {
            StopVoice();
            if (fx != null) fx.Dispose();
            synth.Dispose();
        }
    }
}
