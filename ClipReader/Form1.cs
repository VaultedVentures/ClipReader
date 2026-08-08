using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SpeechLib;

namespace ClipReader
{
    public partial class Form1 : Form
    {
        private bool nowClose = false;
        private bool winVisible = true;
        private bool paused = false;
        private int speed = 0;
        private String clipboardText = "";

        // SAPI's ISpVoice::Speak is unreliable with very long strings and will
        // silently stop part-way through. We split the text into small chunks
        // (well under any reported engine limit) and queue them one after another.
        private const int MAX_CHUNK_CHARS = 4000;

        private SpVoice speech;

        public Form1()
        {
            InitializeComponent();
            int w = Screen.PrimaryScreen.WorkingArea.Width - this.Width;
            int h = Screen.PrimaryScreen.WorkingArea.Height - this.Height;
            this.Location = new Point(w, h);
            winVisible = true;
            speech = new SpVoice();
            speech.Rate = speed;
            timer1.Start();
        }

        private void Form1_Resize(object sender, System.EventArgs e)
        {
            if (FormWindowState.Minimized == WindowState)
                Hide();
        }

        private void CloseMenuItem_Click(object sender, EventArgs e)
        {
            nowClose = true;
            Close();
        }

        private void RestoreMenuItem_Click(object sender, EventArgs e)
        {
            if (winVisible)
            {
                Hide();
                winVisible = false;
            }
            else
            {
                Show();
                winVisible = true;
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!nowClose)
            {
                if (Form1.ActiveForm != null)
                    Form1.ActiveForm.WindowState = FormWindowState.Minimized;
                e.Cancel = true;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (winVisible)
            {
                Hide();
                winVisible = false;
            }
            else
            {
                Show();
                winVisible = true;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {
                String newClipboardText = Clipboard.GetText();
                if (clipboardText != newClipboardText)
                {
                    clipboardText = newClipboardText;
                    READ();
                }
            }
            catch (Exception ex)
            {
                // The clipboard can be briefly locked by the app that is copying;
                // skip this tick and pick it up on the next one instead of crashing.
                System.Diagnostics.Debug.WriteLine("ClipReader: clipboard read failed: " + ex.Message);
            }
        }

        private void READ()
        {
            if (String.IsNullOrEmpty(clipboardText))
                return;

            speech.Rate = speed;

            // Never let SAPI parse the text as XML markup. Without this flag SAPI
            // tries to interpret '<', '>', '&' etc. as tags, and malformed markup
            // (very common in copied text) makes it silently stop mid-sentence.
            // SVSFlagsAsync queues each chunk behind the previous one, so the whole
            // text is read continuously.
            SpeechVoiceSpeakFlags flags =
                SpeechVoiceSpeakFlags.SVSFlagsAsync |
                SpeechVoiceSpeakFlags.SVSFIsNotXML;

            List<String> chunks = SplitIntoChunks(clipboardText);

            // New clipboard content interrupts whatever is still being read, so a
            // long article can't block a new copy for minutes.
            bool purgeBeforeSpeak = true;
            foreach (String chunk in chunks)
            {
                SpeechVoiceSpeakFlags chunkFlags = flags;
                if (purgeBeforeSpeak)
                {
                    chunkFlags |= SpeechVoiceSpeakFlags.SVSFPurgeBeforeSpeak;
                    purgeBeforeSpeak = false;
                }
                speech.Speak(chunk, chunkFlags);
            }
        }

        /// <summary>
        /// Splits text into chunks of at most MAX_CHUNK_CHARS characters, breaking
        /// on sentence/line boundaries when possible so speech sounds natural.
        /// The original text is never altered - chunks are exact substrings.
        /// </summary>
        private static List<String> SplitIntoChunks(String text)
        {
            List<String> chunks = new List<String>();
            if (String.IsNullOrEmpty(text))
                return chunks;
            if (text.Length <= MAX_CHUNK_CHARS)
            {
                chunks.Add(text);
                return chunks;
            }

            char[] boundaries = { '.', '!', '?', '。', '！', '？', '\n', '\r', ';', '；' };

            int start = 0;
            while (start < text.Length)
            {
                int remaining = text.Length - start;
                if (remaining <= MAX_CHUNK_CHARS)
                {
                    chunks.Add(text.Substring(start));
                    break;
                }

                // Find the last boundary character inside the window, so the chunk
                // ends at a natural pause point.
                int windowEnd = start + MAX_CHUNK_CHARS;
                int cut = -1;
                for (int i = windowEnd; i > start; i--)
                {
                    char c = text[i - 1];
                    if (Array.IndexOf(boundaries, c) >= 0)
                    {
                        cut = i;
                        break;
                    }
                }

                // No boundary in the window: hard-break at the window edge.
                if (cut < 0)
                    cut = windowEnd;

                chunks.Add(text.Substring(start, cut - start));

                start = cut;
                // Skip leading whitespace/newlines so the next chunk starts clean.
                while (start < text.Length && Char.IsWhiteSpace(text[start]))
                    start++;
            }
            return chunks;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (paused)
            {
                paused = false;
                speech.Resume();
                timer1.Start();
                button1.Text = "PAUSE";
            }
            else
            {
                paused = true;
                speech.Pause();
                timer1.Stop();
                button1.Text = "UNPAUSE";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            speech.Speak("", SpeechVoiceSpeakFlags.SVSFPurgeBeforeSpeak);
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            speed = trackBar1.Value;
            speech.Rate = speed;
        }
    }

}
