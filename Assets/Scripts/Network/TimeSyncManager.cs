using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PING/PONG 교환으로 두 디바이스의 AudioSettings.dspTime 오프셋을 측정한다.
/// Host가 StartSync()를 호출하고, Guest는 OnPing()으로 응답한다.
/// 5회 샘플의 중앙값을 ClockOffset으로 확정한다.
///
/// 사용법:
///   - Host: timeSync.StartSync(onComplete)
///   - Guest: PING 수신 시 timeSync.OnPing() 호출
///   - Host: PONG 수신 시 timeSync.OnPong(sentDspTime) 호출
///   - 이후: timeSync.CorrectTime(remoteDspTime) 으로 원격 시각을 로컬 기준으로 변환
/// </summary>
public class TimeSyncManager
{
    public double ClockOffset { get; private set; }
    public bool IsSynced { get; private set; }

    // NetworkManager.Send 시그니처: void Send(Action<BinaryWriter>)
    // 따라서 이 콜백의 타입은 Action<Action<System.IO.BinaryWriter>>
    private readonly Action<Action<System.IO.BinaryWriter>> send;
    private readonly int sampleCount;
    private Action onSyncComplete;
    private readonly List<double> offsets = new();
    private double pingStartDspTime;
    private double localOffset; // CorrectTime 보정값: Host=-ClockOffset, Guest=+ClockOffset

    public TimeSyncManager(int sampleCount, Action<Action<System.IO.BinaryWriter>> send)
    {
        this.sampleCount = sampleCount;
        this.send = send;
    }

    /// <summary>
    /// Host가 호출. PING 전송을 시작해 클럭 동기화를 진행한다.
    /// 완료 시 onComplete 콜백을 메인 스레드에서 호출한다.
    /// </summary>
    public void StartSync(Action onComplete)
    {
        onSyncComplete = onComplete;
        offsets.Clear();
        IsSynced = false;
        SendPing();
    }

    /// <summary>
    /// PONG 수신 시 NetworkManager 메인 스레드 큐에서 호출.
    /// 샘플이 모이면 중앙값으로 ClockOffset을 확정하고 onSyncComplete를 호출한다.
    /// </summary>
    public void OnPong(double remoteSentDspTime)
    {
        double now = AudioSettings.dspTime;
        double rtt = now - pingStartDspTime;
        // offset = 로컬 기준으로 원격 dspTime이 얼마나 앞서 있는지
        // correctedRemote = remoteSentDspTime + rtt/2 ≈ 원격 기기의 "지금" 시각
        offsets.Add(remoteSentDspTime + rtt / 2.0 - now);

        if (offsets.Count >= sampleCount)
        {
            ClockOffset = Median(offsets);
            localOffset = -ClockOffset; // Host: guest→host 변환 시 ClockOffset을 빼야 함
            IsSynced = true;
            Debug.Log($"TimeSyncManager: 동기화 완료 / offset={ClockOffset * 1000:F2}ms (샘플 {sampleCount}회)");
            onSyncComplete?.Invoke();
        }
        else
        {
            SendPing();
        }
    }

    /// <summary>
    /// Guest가 PING 수신 시 호출. 현재 dspTime을 sentDspTime으로 담아 PONG 응답한다.
    /// </summary>
    public void OnPing()
    {
        double now = AudioSettings.dspTime;
        send(w => PacketSerializer.WritePong(w, now));
    }

    /// <summary>
    /// Guest가 GAME_START 수신 후 호출. Host가 측정한 clockOffset을 저장한다.
    /// localOffset = +clockOffset (host→guest 변환: 더하기).
    /// </summary>
    public void SetGuestOffset(double hostClockOffset)
    {
        ClockOffset = hostClockOffset;
        localOffset = hostClockOffset;
        IsSynced = true;
    }

    /// <summary>
    /// 원격 dspTime을 로컬 기준으로 변환한다.
    /// Host: remote - ClockOffset (guest→host). Guest: remote + ClockOffset (host→guest).
    /// corrected = remoteDspTime + localOffset
    /// </summary>
    public double CorrectTime(double remoteDspTime) => remoteDspTime + localOffset;

    private void SendPing()
    {
        pingStartDspTime = AudioSettings.dspTime;
        send(w => PacketSerializer.WritePing(w));
    }

    private static double Median(List<double> list)
    {
        var sorted = new List<double>(list);
        sorted.Sort();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 == 0
            ? (sorted[mid - 1] + sorted[mid]) / 2.0
            : sorted[mid];
    }
}
