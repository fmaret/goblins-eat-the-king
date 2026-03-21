using System.Collections;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance => FindFirstObjectByType<SoundManager>();

    [Header("SFX")]
    [SerializeField] private AudioClip swordSlice;
    [SerializeField] private AudioClip potBreak;
    [SerializeField] private AudioClip coinPickup;
    [SerializeField] private AudioClip chestOpen;
    [SerializeField] private AudioClip doorOpen;

    [Header("Musique de combat")]
    [SerializeField] private AudioClip fightMusic;
    [SerializeField] private AudioClip bossFightMusic;
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.6f;
    [SerializeField] private float fadeDuration = 1.5f;

    private AudioSource[] _pool;
    private int _poolIndex;
    private AudioSource _musicSource;
    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        Debug.Log($"[SoundManager] Awake | scene={gameObject.scene.name} | id={GetInstanceID()}");

        _pool = new AudioSource[6];
        for (int i = 0; i < _pool.Length; i++)
            _pool[i] = gameObject.AddComponent<AudioSource>();

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.loop = true;
        _musicSource.volume = 0f;
    }

    public void Play(AudioClip clip, float volume = 1f)
    {
        if (clip == null) return;

        for (int i = 0; i < _pool.Length; i++)
        {
            int idx = (_poolIndex + i) % _pool.Length;
            if (!_pool[idx].isPlaying)
            {
                PlayOnSource(_pool[idx], clip, volume);
                _poolIndex = (idx + 1) % _pool.Length;
                return;
            }
        }

        PlayOnSource(_pool[_poolIndex], clip, volume);
        _poolIndex = (_poolIndex + 1) % _pool.Length;
    }

    private static void PlayOnSource(AudioSource src, AudioClip clip, float volume)
    {
        src.clip   = clip;
        src.volume = volume;
        src.Play();
    }

    public void PlaySwordSlice() => Play(swordSlice);
    public void PlayPotBreak()   => Play(potBreak);
    public void PlayCoinPickup() => Play(coinPickup, 0.7f);
    public void PlayChestOpen()  => Play(chestOpen);
    public void PlayDoorOpen()   => Play(doorOpen);

    public void PlayFightMusic(bool isBoss)
    {
        AudioClip clip = isBoss ? bossFightMusic : fightMusic;
        if (clip == null) return;
        if (_musicSource.isPlaying && _musicSource.clip == clip) return;
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _musicSource.clip = clip;
        _musicSource.volume = 0f;
        _musicSource.Play();
        _fadeCoroutine = StartCoroutine(FadeTo(musicVolume, fadeDuration));
    }

    public void StopFightMusic()
    {
        if (!_musicSource.isPlaying) return;
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeOutAndStop(fadeDuration));
    }

    private IEnumerator FadeTo(float targetVolume, float duration)
    {
        float start = _musicSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(start, targetVolume, elapsed / duration);
            yield return null;
        }
        _musicSource.volume = targetVolume;
    }

    private IEnumerator FadeOutAndStop(float duration)
    {
        float start = _musicSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _musicSource.volume = Mathf.Lerp(start, 0f, elapsed / duration);
            yield return null;
        }
        _musicSource.Stop();
        _musicSource.volume = 0f;
    }
}
