using UnityEngine;

/// <summary>Procedurally generated audio so the demo needs no external assets.</summary>
public class DemoAudio : MonoBehaviour
{
    private const int SampleRate = 44100;

    private AudioSource ambientSource;
    private AudioSource sfxSource;
    private AudioClip blockClip;
    private AudioClip punchClip;
    private AudioClip shockClip;
    private AudioClip enemyWindupClip;
    private AudioClip enemyHitClip;
    private AudioClip enemyDeathClip;
    private AudioClip playerHitClip;
    private AudioClip winClip;
    private AudioClip ambientClip;
    private bool ready;

    private void Awake()
    {
        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.loop = true;
        ambientSource.playOnAwake = false;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = 0.22f;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;
        sfxSource.volume = 0.75f;

        blockClip = BuildBlock();
        punchClip = BuildPunch();
        shockClip = BuildShock();
        enemyWindupClip = BuildEnemyWindup();
        enemyHitClip = BuildEnemyHit();
        enemyDeathClip = BuildEnemyDeath();
        playerHitClip = BuildPlayerHit();
        winClip = BuildWin();
        ambientClip = BuildAmbient();
        ready = true;
    }

    public void PlayAmbient()
    {
        if (!ready || ambientSource == null)
        {
            return;
        }

        ambientSource.clip = ambientClip;
        if (!ambientSource.isPlaying)
        {
            ambientSource.Play();
        }
    }

    public void PlayBlock()
    {
        PlaySfx(blockClip, 0.9f);
    }

    public void PlayPunch(bool hit)
    {
        PlaySfx(punchClip, hit ? 0.75f : 0.5f);
    }

    public void PlayShock()
    {
        PlaySfx(shockClip, 1f);
    }

    public void PlayEnemyWindup()
    {
        PlaySfx(enemyWindupClip, 0.8f);
    }

    public void PlayEnemyHit()
    {
        PlaySfx(enemyHitClip, 0.7f);
    }

    public void PlayEnemyDeath()
    {
        PlaySfx(enemyDeathClip, 0.95f);
    }

    public void PlayPlayerHit()
    {
        PlaySfx(playerHitClip, 0.9f);
    }

    public void PlayWin()
    {
        PlaySfx(winClip, 0.8f);
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (!ready || clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip, volume);
    }

    private static AudioClip BuildBlock()
    {
        return CreateClip("Block_Clang", 0.32f, t =>
        {
            float envelope = Mathf.Exp(-t * 18f);
            float body = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.52f
                       + Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.28f
                       + Mathf.Sin(2f * Mathf.PI * 1810f * t) * 0.14f;
            float crack = (Random.value * 2f - 1f) * Mathf.Exp(-t * 55f) * 0.35f;
            return (body + crack) * envelope;
        });
    }

    private static AudioClip BuildPunch()
    {
        float lastNoise = 0f;
        return CreateClip("Punch_Whoosh", 0.22f, t =>
        {
            float envelope = Mathf.Sin(Mathf.Clamp01(t / 0.05f) * Mathf.PI * 0.5f)
                           * Mathf.Exp(-t * 10f);
            float noise = (Random.value * 2f - 1f);
            lastNoise = Mathf.Lerp(lastNoise, noise, 0.16f);
            float low = Mathf.Sin(2f * Mathf.PI * (115f - t * 120f) * t);
            return (lastNoise * 0.6f + low * 0.35f) * envelope;
        });
    }

    private static AudioClip BuildShock()
    {
        float phase = 0f;
        return CreateClip("Shockwave", 0.9f, t =>
        {
            float frequency = Mathf.Lerp(110f, 30f, Mathf.Clamp01(t / 0.8f));
            phase += 2f * Mathf.PI * frequency / SampleRate;
            float envelope = Mathf.Exp(-t * 4.5f);
            float boom = Mathf.Sin(phase) * 0.8f;
            float crack = (Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.45f;
            return (boom + crack) * envelope;
        });
    }

    private static AudioClip BuildEnemyWindup()
    {
        float phase = 0f;
        return CreateClip("Crystal_Windup", 1.05f, t =>
        {
            float frequency = Mathf.Lerp(150f, 540f, t / 1.05f);
            phase += 2f * Mathf.PI * frequency / SampleRate;
            float tremolo = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 12f * t);
            float envelope = Mathf.Clamp01(t / 0.1f) * Mathf.Clamp01((1.05f - t) / 0.16f);
            float tone = Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 2.02f) * 0.18f;
            float air = (Random.value * 2f - 1f) * 0.12f;
            return (tone + air) * tremolo * envelope;
        });
    }

    private static AudioClip BuildEnemyHit()
    {
        return CreateClip("Crystal_Hit", 0.24f, t =>
        {
            float envelope = Mathf.Exp(-t * 16f);
            float noise = (Random.value * 2f - 1f) * 0.45f;
            float glass = Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.24f
                        + Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.16f;
            float thud = Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.4f;
            return (noise + glass + thud) * envelope;
        });
    }

    private static AudioClip BuildEnemyDeath()
    {
        float phase = 0f;
        return CreateClip("Crystal_Death", 1.4f, t =>
        {
            float frequency = Mathf.Lerp(420f, 40f, Mathf.Clamp01(t / 1.2f));
            phase += 2f * Mathf.PI * frequency / SampleRate;
            float envelope = Mathf.Exp(-t * 2.8f);
            float tone = Mathf.Sin(phase) * 0.4f;
            float shatter = (Random.value * 2f - 1f) * Mathf.Exp(-t * 5f) * 0.4f;
            return (tone + shatter) * envelope;
        });
    }

    private static AudioClip BuildPlayerHit()
    {
        return CreateClip("Player_Hit", 0.36f, t =>
        {
            float envelope = Mathf.Exp(-t * 9f);
            float thud = Mathf.Sin(2f * Mathf.PI * (92f - t * 60f) * t) * 0.6f;
            float noise = (Random.value * 2f - 1f) * Mathf.Exp(-t * 30f) * 0.3f;
            return (thud + noise) * envelope;
        });
    }

    private static AudioClip BuildWin()
    {
        return CreateClip("Test_Complete", 1.8f, t =>
        {
            float a = Mathf.Sin(2f * Mathf.PI * 523.25f * t) * Mathf.Exp(-t * 2.2f);
            float b = Mathf.Sin(2f * Mathf.PI * 783.99f * Mathf.Max(0f, t - 0.18f)) * Mathf.Exp(-Mathf.Max(0f, t - 0.18f) * 2.5f);
            float c = Mathf.Sin(2f * Mathf.PI * 1046.5f * Mathf.Max(0f, t - 0.34f)) * Mathf.Exp(-Mathf.Max(0f, t - 0.34f) * 2.8f);
            return (a * 0.25f + b * 0.22f + c * 0.18f) * Mathf.Clamp01((1.8f - t) / 0.4f);
        });
    }

    private static AudioClip BuildAmbient()
    {
        float phaseA = 0f;
        float phaseB = 0f;
        float phaseC = 0f;
        return CreateClip("Facility_Hum", 6f, t =>
        {
            phaseA += 2f * Mathf.PI * 48f / SampleRate;
            phaseB += 2f * Mathf.PI * 72f / SampleRate;
            phaseC += 2f * Mathf.PI * 96f / SampleRate;
            float slow = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 0.08f * t);
            float tone = Mathf.Sin(phaseA) * 0.12f
                       + Mathf.Sin(phaseB) * 0.05f
                       + Mathf.Sin(phaseC) * 0.025f;
            float noise = (Random.value * 2f - 1f) * 0.018f;
            return (tone * slow) + noise;
        }, loop: true);
    }

    private static AudioClip CreateClip(string name, float duration, System.Func<float, float> generator, bool loop = false)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)SampleRate;
            samples[i] = Mathf.Clamp(generator(t) * 0.8f, -0.98f, 0.98f);
        }

        AudioClip clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
