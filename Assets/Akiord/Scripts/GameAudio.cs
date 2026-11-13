using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GameAudio : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField, Range(0f, 1f)] private float volume = 0.7f;
    [SerializeField] private AudioClip confirmClip;
    [SerializeField] private AudioClip coinClip;
    [SerializeField] private AudioClip winClip;
    [SerializeField] private AudioClip loseClip;
    [SerializeField] private AudioClip countdownClip;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.45f;

    private void Awake()
    {
        if (source == null) source = GetComponent<AudioSource>();
    }

    private void Play(AudioClip clip, float scale = 1f)
    {
        if (source != null && clip != null) source.PlayOneShot(clip, volume * scale);
    }

    public void PlayConfirm() => Play(confirmClip);
    public void PlayCoin() => Play(coinClip);
    public void PlayWin() => Play(winClip);
    public void PlayLose() => Play(loseClip);
    public void PlayCountdown() => Play(countdownClip);

    public void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0) return;
        Play(footstepClips[Random.Range(0, footstepClips.Length)], footstepVolume);
    }

    public void PlayRestart()
    {
        if (confirmClip == null) return;
        var holder = new GameObject("Restart Sound");
        DontDestroyOnLoad(holder);
        var restartSource = holder.AddComponent<AudioSource>();
        restartSource.playOnAwake = false;
        restartSource.spatialBlend = 0f;
        restartSource.volume = volume;
        restartSource.clip = confirmClip;
        restartSource.Play();
        Destroy(holder, confirmClip.length + 0.1f);
    }
}
