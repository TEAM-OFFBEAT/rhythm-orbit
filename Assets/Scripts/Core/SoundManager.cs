using UnityEngine;

public class SoundManager : SceneSingleton<SoundManager>
{
    [SerializeField] private AudioSource audioSource;

    /// <summary>
    /// 지정한 AudioClip을 0.1초 후 dspTime 기준으로 예약 재생.
    /// </summary>
    public void PlayBGM(AudioClip clip)
    {
        audioSource.clip = clip;
        audioSource.PlayScheduled(AudioSettings.dspTime + 0.1);
    }

    /// <summary>
    /// 현재 재생 중인 BGM을 즉시 정지.
    /// </summary>
    public void StopBGM()
    {
        audioSource.Stop();
    }
}
