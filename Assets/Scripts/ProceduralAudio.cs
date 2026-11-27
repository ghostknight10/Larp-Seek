using UnityEngine;

/// <summary>
/// Generates procedural audio clips for game actions (whistle, gunshot, hit, stomp, revive, etc.)
/// without needing any external audio assets.
/// </summary>
public static class ProceduralAudio
{
    static AudioClip s_WhistleClip;
    static AudioClip s_GunshotClip;
    static AudioClip s_HitClip;
    static AudioClip s_StompClip;
    static AudioClip s_DownedClip;
    static AudioClip s_RevivedClip;
    static AudioClip s_BeaconHumClip;
    static AudioClip s_DecoyPuffClip;

    const int SampleRate = 44100;

    public static AudioClip WhistleClip
    {
        get
        {
            if (s_WhistleClip == null) s_WhistleClip = CreateWhistleClip();
            return s_WhistleClip;
        }
    }

    public static AudioClip GunshotClip
    {
        get
        {
            if (s_GunshotClip == null) s_GunshotClip = CreateGunshotClip();
            return s_GunshotClip;
        }
    }

    public static AudioClip HitClip
    {
        get
        {
            if (s_HitClip == null) s_HitClip = CreateHitClip();
            return s_HitClip;
        }
    }

    public static AudioClip StompClip
    {
        get
        {
            if (s_StompClip == null) s_StompClip = CreateStompClip();
            return s_StompClip;
        }
    }

    public static AudioClip DownedClip
    {
        get
        {
            if (s_DownedClip == null) s_DownedClip = CreateDownedClip();
            return s_DownedClip;
        }
    }

    public static AudioClip RevivedClip
    {
        get
        {
            if (s_RevivedClip == null) s_RevivedClip = CreateRevivedClip();
            return s_RevivedClip;
        }
    }

    public static AudioClip BeaconHumClip
    {
        get
        {
            if (s_BeaconHumClip == null) s_BeaconHumClip = CreateBeaconHumClip();
            return s_BeaconHumClip;
        }
    }

    public static AudioClip DecoyPuffClip
    {
        get
        {
            if (s_DecoyPuffClip == null) s_DecoyPuffClip = CreateDecoyPuffClip();
            return s_DecoyPuffClip;
        }
    }

    public static void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volume));
    }

    static AudioClip s_ScreamClip;
    public static AudioClip ScreamClip => s_ScreamClip != null ? s_ScreamClip : (s_ScreamClip = CreateScreamClip());

    /// <summary>Death scream, played as a 3D sound that carries well (others nearby hear the kill).</summary>
    public static void PlayScream(Vector3 position, float volume = 1f)
    {
        var clip = ScreamClip;
        if (clip == null) return;
        var go = new GameObject("Scream_OneShot");
        go.transform.position = position;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume);
        src.spatialBlend = 0.85f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 3f;
        src.maxDistance = 45f;
        src.pitch = Random.Range(0.92f, 1.08f); // every scream sounds a bit different
        src.Play();
        Object.Destroy(go, clip.length / src.pitch + 0.1f);
    }

    static AudioClip CreateScreamClip()
    {
        // Human-ish scream: harsh glottal pulse train with a rising-then-falling pitch, jitter/vibrato,
        // two vowel formants ("AAH") and breath noise, then crushed to 8-bit-ish for the retro vibe.
        float duration = 1.35f;
        int sampleCount = (int)(SampleRate * duration);
        float[] data = new float[sampleCount];
        var rng = new System.Random(1337);

        float phase = 0f;
        float f1State = 0f, f1Vel = 0f, f2State = 0f, f2Vel = 0f;
        const float f1 = 850f, f2 = 1350f; // "aah" formants

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float n = t / duration;

            float contour = n < 0.18f ? Mathf.Lerp(420f, 780f, n / 0.18f) : Mathf.Lerp(780f, 330f, (n - 0.18f) / 0.82f);
            float vib = Mathf.Sin(2f * Mathf.PI * 9f * t) * 28f + ((float)rng.NextDouble() - 0.5f) * 40f;
            phase += (contour + vib) / SampleRate;
            phase -= Mathf.Floor(phase);

            // Glottal pulse (sharp saw with a soft clip)
            float src = (1f - 2f * phase);
            src = src * src * src + ((float)rng.NextDouble() * 2f - 1f) * 0.35f;

            // Two resonant formant filters (simple 2nd-order resonators)
            float w1 = 2f * Mathf.PI * f1 / SampleRate, w2 = 2f * Mathf.PI * f2 / SampleRate;
            f1Vel += (src - f1State) * w1 * w1 - f1Vel * 0.12f; f1State += f1Vel;
            f2Vel += (src - f2State) * w2 * w2 - f2Vel * 0.16f; f2State += f2Vel;
            float voiced = f1State * 0.7f + f2State * 0.5f + src * 0.25f;

            float env = n < 0.04f ? n / 0.04f : (n > 0.75f ? Mathf.Clamp01((1f - n) / 0.25f) : 1f);
            float s = Mathf.Clamp(voiced * 0.9f, -1f, 1f) * env;

            // Bit-crush to 6 bits for a lo-fi game feel
            s = Mathf.Round(s * 32f) / 32f;
            data[i] = s * 0.8f;
        }

        AudioClip clip = AudioClip.Create("Procedural_Scream", sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Fully 3D whistle with a linear falloff: loud when the listener is close, fading to silence
    /// at <see cref="WhistleMaxDistance"/>, so a Hunter can home in on it by ear.
    /// </summary>
    public const float WhistleMinDistance = 1.5f;
    public const float WhistleMaxDistance = 16f;   // beyond this the Hunter hears nothing
    public const float WhistleVolumeScale = 0.45f; // quiet overall - only noticeable up close
    public static void PlayWhistle(Vector3 position, float volume = 1f)
    {
        var clip = WhistleClip;
        if (clip == null) return;
        var go = new GameObject("Whistle_OneShot");
        go.transform.position = position;
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = Mathf.Clamp01(volume) * WhistleVolumeScale;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Custom;
        src.minDistance = WhistleMinDistance;
        src.maxDistance = WhistleMaxDistance;
        // Custom curve (x = distance / maxDistance): full volume up close, quickly quieter, silent at max.
        src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(WhistleMinDistance / WhistleMaxDistance, 1f),
            new Keyframe(0.4f, 0.35f),
            new Keyframe(0.75f, 0.08f),
            new Keyframe(1f, 0f)));
        src.dopplerLevel = 0f;
        src.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }
    public static void PlayGunshot(Vector3 position, float volume = 1f) => PlayAtPoint(GunshotClip, position, volume);
    public static void PlayHit(Vector3 position, float volume = 1f) => PlayAtPoint(HitClip, position, volume);
    public static void PlayStomp(Vector3 position, float volume = 1f) => PlayAtPoint(StompClip, position, volume);
    public static void PlayDowned(Vector3 position, float volume = 1f) => PlayAtPoint(DownedClip, position, volume);
    public static void PlayRevived(Vector3 position, float volume = 1f) => PlayAtPoint(RevivedClip, position, volume);
    public static void PlayBeaconHum(Vector3 position, float volume = 0.8f) => PlayAtPoint(BeaconHumClip, position, volume);
    public static void PlayDecoyPuff(Vector3 position, float volume = 0.85f) => PlayAtPoint(DecoyPuffClip, position, volume);

    static AudioClip CreateWhistleClip()
    {
        float duration = 0.85f;
        int sampleCount = (int)(SampleRate * duration);
        float[] data = new float[sampleCount];

        float baseFreq1 = 1950f;
        float baseFreq2 = 2380f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;

            // Attack & decay envelope
            float env = 1f;
            if (t < 0.06f) env = t / 0.06f;
            else if (t > 0.70f) env = Mathf.Max(0f, (duration - t) / 0.15f);

            // Vibrato & dual resonant frequencies
            float vib = Mathf.Sin(2f * Mathf.PI * 7.5f * t) * 45f;
            float s1 = Mathf.Sin(2f * Mathf.PI * (baseFreq1 + vib) * t);
            float s2 = Mathf.Sin(2f * Mathf.PI * (baseFreq2 + vib * 0.8f) * t);

            // Slight breath noise
            float breath = (Random.value * 2f - 1f) * 0.08f;

            data[i] = (s1 * 0.6f + s2 * 0.35f + breath) * env * 0.65f;
        }

        AudioClip clip = AudioClip.Create("Procedural_Whistle", sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip CreateGunshotClip()
    {
        float duration = 0.28f;
        int sampleCount = (int)(SampleRate * duration);
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;

            // Fast decaying noise burst
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 22f);

            // Low frequency thump/kick
            float thumpFreq = Mathf.Lerp(160f, 40f, t / duration);
            float thump = Mathf.Sin(2f * Mathf.PI * thumpFreq * t) * Mathf.Exp(-t * 14f);

            data[i] = Mathf.Clamp((noise * 0.75f + thump * 0.55f) * 0.9f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Procedural_Gunshot", sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip CreateHitClip()
    {
        float duration = 0.18f;
        int sampleCount = (int)(SampleRate * duration);
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float freq = Mathf.Lerp(1400f, 950f, t / duration);
            float env = Mathf.Exp(-t * 26f);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.65f;
        }

        AudioClip clip = AudioClip.Create("Procedural_Hit", sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip CreateStompClip()
    {
        float duration = 0.32f;
        int sampleCount = (int)(SampleRate * duration);
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            // Upward spring sweep (cartoon boing)
            float freq = Mathf.Lerp(180f, 620f, Mathf.Sqrt(t / duration)) + Mathf.Sin(2f * Mathf.PI * 18f * t) * 35f;
            float env = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.75f;
        }

        AudioClip clip = AudioClip.Create("Procedural_Stomp", sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip CreateDownedClip()
    {
        float duration = 0.55f;
        int sampleCount = (int)(SampleRate * duration);
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            // Descending sad tone
            float freq = Mathf.Lerp(380f, 110f, t / duration);
            float env = Mathf.Exp(-t * 4.5f);
            float saw = (t * freq % 1f) * 2f - 1f;
            data[i] = saw * env * 0.5f;
        }

        AudioClip clip = AudioClip.Create("Procedural_Downed", sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip CreateRevivedClip()
    {
        float duration = 0.8f;
        int sampleCount = (int)(SampleRate * duration);
        float[] data = new float[sampleCount];

        // Three ascending notes: C5 (523Hz), E5 (659Hz), G5 (784Hz)
        float[] notes = { 523.25f, 659.25f, 783.99f };
        float noteLen = 0.22f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            int noteIdx = Mathf.Clamp((int)(t / noteLen), 0, notes.Length - 1);
            float noteT = t - noteIdx * noteLen;
            float freq = notes[noteIdx];
            float env = Mathf.Exp(-noteT * 7f);
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.55f;
        }

        AudioClip clip = AudioClip.Create("Procedural_Revived", sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip CreateBeaconHumClip()
    {
        float duration = 0.45f;
        int sampleCount = (int)(SampleRate * duration);
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float s1 = Mathf.Sin(2f * Mathf.PI * 440f * t);
            float s2 = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.35f;
            float env = Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI);
            data[i] = (s1 + s2) * env * 0.45f;
        }

        AudioClip clip = AudioClip.Create("Procedural_BeaconHum", sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip CreateDecoyPuffClip()
    {
        float duration = 0.22f;
        int sampleCount = (int)(SampleRate * duration);
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float pop = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(280f, 80f, t / duration) * t) * Mathf.Exp(-t * 18f);
            float hiss = (Random.value * 2f - 1f) * Mathf.Exp(-t * 25f);
            data[i] = (pop * 0.6f + hiss * 0.4f) * 0.6f;
        }

        AudioClip clip = AudioClip.Create("Procedural_DecoyPuff", sampleCount, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
