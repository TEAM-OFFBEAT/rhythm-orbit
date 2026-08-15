using UnityEngine;

/// <summary>
/// 튜토리얼 단계별 메시지와 노트 패턴을 제공한다.
/// 메시지, 목표 노트 수, 실제 노트 수, 별 UI 개수를 한 곳에서 맞춘다.
/// </summary>

public class TutorialPatternProvider : MonoBehaviour
{
    public TutorialPatternData GetAttackPracticePattern()
    {
        return new TutorialPatternData(
            "안녕",
            new[]
            {
                NoteType.HIGH,
                NoteType.LOW
            }
        );
    }

    public TutorialPatternData GetDefensePracticePattern(int index)
    {
        switch (index)
        {
            case 0:
                return new TutorialPatternData(
                    "응",
                    new[]
                    {
                        NoteType.HIGH
                    }
                );

            case 1:
                return new TutorialPatternData(
                    "나",
                    new[]
                    {
                        NoteType.LOW
                    }
                );

            default:
                return new TutorialPatternData(
                    "반가워",
                    new[]
                    {
                        NoteType.HIGH,
                        NoteType.LOW,
                        NoteType.HIGH
                    }
                );
        }
    }

    public TutorialPatternData GetRallyPlayerPattern(int turn)
    {
        switch (turn % 4)
        {
            case 0:
                return new TutorialPatternData(
                    "안녕",
                    new[]
                    {
                        NoteType.HIGH,
                        NoteType.LOW
                    }
                );

            case 1:
                return new TutorialPatternData(
                    "누구",
                    new[]
                    {
                        NoteType.LOW,
                        NoteType.LOW
                    }
                );

            case 2:
                return new TutorialPatternData(
                    "친구야",
                    new[]
                    {
                        NoteType.HIGH,
                        NoteType.HIGH,
                        NoteType.LOW
                    }
                );

            default:
                return new TutorialPatternData(
                    "반가워",
                    new[]
                    {
                        NoteType.LOW,
                        NoteType.HIGH,
                        NoteType.LOW
                    }
                );
        }
    }

    public TutorialPatternData GetRallyOpponentPattern(int turn)
    {
        switch (turn % 4)
        {
            case 0:
                return new TutorialPatternData(
                    "들리니",
                    new[]
                    {
                        NoteType.HIGH,
                        NoteType.LOW,
                        NoteType.HIGH
                    }
                );

            case 1:
                return new TutorialPatternData(
                    "잘들려",
                    new[]
                    {
                        NoteType.LOW,
                        NoteType.HIGH,
                        NoteType.LOW
                    }
                );

            case 2:
                return new TutorialPatternData(
                    "좋았어",
                    new[]
                    {
                        NoteType.HIGH,
                        NoteType.HIGH,
                        NoteType.LOW
                    }
                );

            default:
                return new TutorialPatternData(
                    "",
                    new[]
                    {
                        NoteType.LOW,
                        NoteType.LOW,
                        NoteType.HIGH
                    }
                );
        }
    }
}