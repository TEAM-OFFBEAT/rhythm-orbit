using UnityEngine;

/// <summary>
/// 박자 모드에 따라 레인의 라인 그룹 표시를 전환한다.
/// </summary>
public class LaneController : MonoBehaviour
{
    [SerializeField] private GameObject lines44;
    [SerializeField] private GameObject lines34;

    /// <summary>
    /// 박자 모드에 맞는 라인 그룹을 활성화한다.
    /// </summary>
    public void SetMode(TimeSignatureMode mode)
    {
        if (lines44 != null) lines44.SetActive(mode == TimeSignatureMode.FourFour);
        if (lines34 != null) lines34.SetActive(mode == TimeSignatureMode.ThreeFour);
    }
}
