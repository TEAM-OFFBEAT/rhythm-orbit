using UnityEngine;

/// <summary>
/// 카메라 이동에 반응하여 배경 레이어를 패럴랙스 스크롤링.
/// 각 레이어는 카메라 이동 거리의 일부만큼 반대 방향으로 이동한다.
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform sparkleLayer;
    [SerializeField] private Transform planetLayer;

    [Header("Parallax Factors")]
    [SerializeField] private float sparkleFactor = 0.25f;
    [SerializeField] private float planetFactor = 0.333f;

    private float cameraStartX;
    private Vector3 sparkleStartLocalPos;
    private Vector3 planetStartLocalPos;

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        cameraStartX = targetCamera.transform.position.x;
        sparkleStartLocalPos = sparkleLayer != null ? sparkleLayer.localPosition : Vector3.zero;
        planetStartLocalPos = planetLayer != null ? planetLayer.localPosition : Vector3.zero;
    }

    private void LateUpdate()
    {
        float cameraDelta = targetCamera.transform.position.x - cameraStartX;

        if (sparkleLayer != null)
        {
            Vector3 pos = sparkleStartLocalPos;
            pos.x -= cameraDelta * sparkleFactor;
            sparkleLayer.localPosition = pos;
        }

        if (planetLayer != null)
        {
            Vector3 pos = planetStartLocalPos;
            pos.x -= cameraDelta * planetFactor;
            planetLayer.localPosition = pos;
        }
    }
}
