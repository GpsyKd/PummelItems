using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Produces the mod's sound effects.
///
/// Every clip is written as a .wav under Assets/Audio and packed into the bundle.
/// A file that already exists is NEVER regenerated - so dropping a real recording in
/// with the right name permanently replaces the synthesised stand-in, no code change.
/// </summary>
public static class SoundBuilder
{
    private const int SampleRate = 44100;
    public const string AudioDir = "Assets/Audio";

    public static void BuildAll(string bundleName)
    {
        Directory.CreateDirectory(AudioDir);

        Ensure("snd_shuffle",     bundleName, () => Whoosh(0.75f));
        Ensure("snd_dice_charge", bundleName, () => RisingCharge(0.45f));
        Ensure("snd_dice_ready",  bundleName, () => Chime(0.7f));
        Ensure("snd_explode",     bundleName, () => Boom(0.9f, 90f, 35f));
        Ensure("snd_bounce",      bundleName, () => Bounce(0.12f, 320f, 730f));

        // One sound per item: a shared bang everywhere made them all feel the same.
        Ensure("snd_blast",       bundleName, () => Boom(0.7f, 140f, 55f));    // grenade: sharper
        Ensure("snd_clink",       bundleName, () => Bounce(0.09f, 900f, 1650f)); // metal tick
        Ensure("snd_ping",        bundleName, () => Ping(0.22f));               // ricochet
        Ensure("snd_shatter",     bundleName, () => Shatter(0.5f));             // ice
        Ensure("snd_squelch",     bundleName, () => Squelch(0.25f));            // sticky
        Ensure("snd_whirl",       bundleName, () => Whirl(0.9f));               // boomerang flight
        Ensure("snd_whack",       bundleName, () => Whack(0.18f));              // boomerang hit
        Ensure("snd_chaos",       bundleName, () => Chaos(1.0f));               // rules changed
        Ensure("snd_drain",       bundleName, () => Drain(0.8f));               // life siphon

        // Armageddon drops one meteor per player in quick succession. A single impact sound
        // repeated four times half a second apart reads as a stutter, not a barrage, so the
        // impacts pick from these at random and the opening rumble is its own cue.
        Ensure("snd_doom",        bundleName, () => Boom(1.0f, 70f, 28f));
        Ensure("snd_boom_a",      bundleName, () => Boom(0.8f, 110f, 40f));
        Ensure("snd_boom_b",      bundleName, () => Boom(0.85f, 95f, 33f));
        Ensure("snd_boom_c",      bundleName, () => Boom(0.75f, 125f, 46f));

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void Ensure(string name, string bundleName, Func<float[]> generate)
    {
        // A real recording dropped in beside the generated clips wins, whatever its format.
        // That is how a synthesised placeholder gets replaced: put the file in and it is
        // picked up, with nothing here to edit. Only what is still missing gets synthesised.
        string path = null;
        string[] formats = { ".ogg", ".wav", ".mp3", ".aiff" };
        for (int i = 0; i < formats.Length; i++)
        {
            string candidate = AudioDir + "/" + name + formats[i];
            if (File.Exists(candidate)) { path = candidate; break; }
        }

        if (path != null)
        {
            Debug.Log("[PCI] sound kept (already present): " + path);
        }
        else
        {
            path = AudioDir + "/" + name + ".wav";
            WriteWav(path, generate());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            Debug.Log("[PCI] sound synthesised: " + path);
        }

        AssetImporter imp = AssetImporter.GetAtPath(path);
        if (imp != null && imp.assetBundleName != bundleName)
        {
            imp.assetBundleName = bundleName;
            imp.SaveAndReimport();
        }
    }

    // ------------------------------------------------------------------ generators

    /// <summary>Teleport-style swish: noise pushed through a sweeping resonant tone.</summary>
    private static float[] Whoosh(float seconds)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];
        System.Random rnd = new System.Random(1337);

        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;

            // Bell-shaped loudness so it swells and fades rather than clicking on.
            float env = Mathf.Sin(Mathf.PI * t);
            env *= env;

            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);

            // Sweeping one-pole low-pass: opens up, then closes again.
            float cutoff = Mathf.Lerp(0.02f, 0.45f, Mathf.Sin(Mathf.PI * t));
            lp += (noise - lp) * cutoff;

            // A tone gliding downward gives it direction, like something passing by.
            float freq = Mathf.Lerp(900f, 180f, t);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * i / SampleRate);

            buf[i] = (lp * 0.75f + tone * 0.25f) * env * 0.7f;
        }
        return buf;
    }

    /// <summary>Winding up: pitch climbs, amplitude grows, ends on a small snap.</summary>
    private static float[] RisingCharge(float seconds)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;

            float freq = Mathf.Lerp(220f, 880f, t * t);
            phase += 2f * Mathf.PI * freq / SampleRate;

            // Square-ish body keeps it audible over music without being shrill.
            float body = Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2f) * 0.25f;

            float env = Mathf.Clamp01(t * 4f) * Mathf.Clamp01((1f - t) * 6f);
            buf[i] = body * env * 0.55f;
        }
        return buf;
    }

    /// <summary>Bright two-note ding for "the charge is live".</summary>
    private static float[] Chime(float seconds)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float time = (float)i / SampleRate;

            float a = Mathf.Sin(2f * Mathf.PI * 880f * time);
            float b = Mathf.Sin(2f * Mathf.PI * 1318f * time) * 0.6f;
            float c = Mathf.Sin(2f * Mathf.PI * 1760f * time) * 0.25f;

            float env = Mathf.Exp(-5f * t) * Mathf.Clamp01(t * 200f);
            buf[i] = (a + b + c) * env * 0.32f;
        }
        return buf;
    }

    /// <summary>Descending whistle with a ricochet's characteristic pitch drop.</summary>
    private static float[] Ping(float seconds)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float freq = Mathf.Lerp(2600f, 900f, t * t);
            phase += 2f * Mathf.PI * freq / SampleRate;
            buf[i] = Mathf.Sin(phase) * Mathf.Exp(-9f * t) * 0.45f;
        }
        return buf;
    }

    /// <summary>Ice breaking: a cluster of bright, detuned chips falling away.</summary>
    private static float[] Shatter(float seconds)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];
        System.Random rnd = new System.Random(99);

        float[] freqs = new float[7];
        float[] starts = new float[7];
        for (int k = 0; k < freqs.Length; k++)
        {
            freqs[k] = 1400f + (float)rnd.NextDouble() * 2600f;
            starts[k] = (float)rnd.NextDouble() * 0.35f;
        }

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float time = (float)i / SampleRate;
            float v = 0f;

            for (int k = 0; k < freqs.Length; k++)
            {
                if (t < starts[k]) continue;
                float local = t - starts[k];
                v += Mathf.Sin(2f * Mathf.PI * freqs[k] * time) * Mathf.Exp(-16f * local);
            }
            buf[i] = v * 0.16f;
        }
        return buf;
    }

    /// <summary>Wet slap for something landing in goo.</summary>
    private static float[] Squelch(float seconds)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];
        System.Random rnd = new System.Random(7);
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            // Filter slams shut, which is what makes it sound wet rather than crisp.
            lp += (noise - lp) * Mathf.Lerp(0.35f, 0.02f, t);

            float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(420f, 120f, t) * i / SampleRate);
            buf[i] = (lp * 0.7f + body * 0.5f) * Mathf.Exp(-11f * t) * 0.75f;
        }
        return buf;
    }

    /// <summary>Spinning wood: a tone chopped by its own rotation.</summary>
    private static float[] Whirl(float seconds)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float time = (float)i / SampleRate;

            float tone = Mathf.Sin(2f * Mathf.PI * 330f * time)
                       + Mathf.Sin(2f * Mathf.PI * 495f * time) * 0.4f;

            // Amplitude chopped at ~18 Hz - the blade passing the ear.
            float chop = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 18f * time);
            float env = Mathf.Sin(Mathf.PI * t);

            buf[i] = tone * chop * env * 0.3f;
        }
        return buf;
    }

    /// <summary>Rules changing: a fanfare that slides upward and refuses to resolve.</summary>
    private static float[] Chaos(float seconds)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];

        // Three voices a fifth apart, all bending up together - unsettling on purpose.
        float[] baseFreq = { 196f, 294f, 440f };

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float time = (float)i / SampleRate;
            float bend = Mathf.Lerp(1f, 1.5f, t * t);

            float v = 0f;
            for (int k = 0; k < baseFreq.Length; k++)
                v += Mathf.Sin(2f * Mathf.PI * baseFreq[k] * bend * time) / (k + 1.4f);

            // Tremolo speeding up as it rises.
            float wobble = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(5f, 14f, t) * time);
            float env = Mathf.Clamp01(t * 8f) * Mathf.Clamp01((1f - t) * 3f);

            buf[i] = v * wobble * env * 0.30f;
        }
        return buf;
    }


    /// <summary>Siphon: a tone sliding downward while a shimmer rises over it.</summary>
    private static float[] Drain(float seconds)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];
        float phase = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float time = (float)i / SampleRate;

            // The body falls away - something is being taken.
            float freq = Mathf.Lerp(520f, 130f, Mathf.Sqrt(t));
            phase += 2f * Mathf.PI * freq / SampleRate;
            float body = Mathf.Sin(phase) * 0.7f + Mathf.Sin(phase * 0.5f) * 0.3f;

            // A thin rising line on top, so it also reads as a gain.
            float shimmer = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(700f, 1500f, t) * time) * 0.18f;

            float env = Mathf.Clamp01(t * 12f) * Mathf.Clamp01((1f - t) * 3.5f);
            buf[i] = (body + shimmer) * env * 0.42f;
        }
        return buf;
    }

    /// <summary>Blunt wooden hit.</summary>
    private static float[] Whack(float seconds)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];
        System.Random rnd = new System.Random(555);

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float time = (float)i / SampleRate;

            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-40f * t);
            float body = Mathf.Sin(2f * Mathf.PI * 190f * time) * Mathf.Exp(-13f * t);

            buf[i] = (noise * 0.4f + body * 0.8f) * 0.7f;
        }
        return buf;
    }

    /// <summary>Explosion: a noise burst that decays fast, over a low body thump.</summary>
    private static float[] Boom(float seconds, float freqStart, float freqEnd)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];
        System.Random rnd = new System.Random(4242);

        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float time = (float)i / SampleRate;

            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            // Filter opens wide at the crack, then closes as the rumble takes over.
            lp += (noise - lp) * Mathf.Lerp(0.55f, 0.04f, t);

            // Low sine sliding down gives the blast its weight.
            float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(freqStart, freqEnd, t) * time);

            float crack = Mathf.Exp(-14f * t);
            float rumble = Mathf.Exp(-3.2f * t);

            buf[i] = (lp * crack * 0.85f + body * rumble * 0.55f) * 0.9f;
        }
        return buf;
    }

    /// <summary>Short woody tick for bouncing off the ground.</summary>
    private static float[] Bounce(float seconds, float f1, float f2)
    {
        int n = (int)(SampleRate * seconds);
        float[] buf = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float time = (float)i / SampleRate;

            float a = Mathf.Sin(2f * Mathf.PI * f1 * time);
            float b = Mathf.Sin(2f * Mathf.PI * f2 * time) * 0.5f;
            buf[i] = (a + b) * Mathf.Exp(-28f * t) * 0.5f;
        }
        return buf;
    }

    // ------------------------------------------------------------------ wav writer

    /// <summary>Minimal 16-bit mono PCM WAV - what Unity's importer expects.</summary>
    private static void WriteWav(string path, float[] samples)
    {
        using (FileStream fs = new FileStream(path, FileMode.Create))
        using (BinaryWriter w = new BinaryWriter(fs))
        {
            int dataBytes = samples.Length * 2;

            w.Write(new char[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + dataBytes);
            w.Write(new char[] { 'W', 'A', 'V', 'E' });

            w.Write(new char[] { 'f', 'm', 't', ' ' });
            w.Write(16);                       // chunk size
            w.Write((short)1);                 // PCM
            w.Write((short)1);                 // mono
            w.Write(SampleRate);
            w.Write(SampleRate * 2);           // byte rate
            w.Write((short)2);                 // block align
            w.Write((short)16);                // bits per sample

            w.Write(new char[] { 'd', 'a', 't', 'a' });
            w.Write(dataBytes);

            for (int i = 0; i < samples.Length; i++)
            {
                float v = Mathf.Clamp(samples[i], -1f, 1f);
                w.Write((short)(v * short.MaxValue));
            }
        }
    }
}
