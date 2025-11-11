using UnityEngine;
using UnityEngine.Video;
#if UNITY_EDITOR
using UnityEditor;
#endif
/// <summary>
/// 각 컷씬의 정보를 담는 Asset을 생성하는 SO
/// </summary>

public enum CutsceneType
{
    Video,
    Dialogue
}

[CreateAssetMenu(
    fileName = "CutsceneConfig",
    menuName = "Configs/CutsceneConfig",
    order = 0)]
public class CutsceneConfig : ScriptableObject
{
    [Header("식별자 (enum 또는 int ID)")]
    public int id;                     // 컷신 번호 or enum의 int 값

    [Header("컷신 유형 선택")]
    public CutsceneType cutsceneType;

    [Header("비디오 컷신을 위한 옵션들")]
    [Header("영상 클립")]
    public VideoClip videoClip;        // VideoPlayer용 클립

    [Header("대화 컷신을 위한 옵션들")]
    [Header("배경음악")]
    public AudioClip bgmClip;          // BGM용 클립 (없으면 무음)

    [Header("배경 이미지")]
    public Sprite background;

    [Header("여자 캐릭터 초상화")]
    public Sprite girlCharacterSprite;
    [Header("남자 캐릭터 초상화")]
    public Sprite boyCharacterSprite;

    [Header("대사 라인들")]
    public DialogueLine[] lines;


    [Header("다음으로 이동할 씬 이름")]
    public string nextScene;           // 컷신 종료 후 이동할 씬

    [Header("스킵 가능 여부")]
    public bool skippable = true;      // 스킵 가능 여부

    /// <summary>
    /// 컷신 정보를 출력용 문자열로 반환.
    /// </summary>
    public override string ToString()
    {
        return $"CutsceneConfig(id={id}, CutsceneType={cutsceneType}, next={nextScene}, skippable={skippable})";
    }
}

[System.Serializable]
public class DialogueLine
{
    [Header("말하는 사람 이름")]
    public string speakerName;

    [Header("텍스트")]
    [TextArea(2, 4)]
    public string text;

    [Header("이 라인에서만 쓸 초상화 (null이면 기본 사용)")]
    public Sprite overrideCharacterSprite;

    [Header("보이스, 효과음 (선택)")]
    public AudioClip voiceClip;

    [Header("자동 진행 시간 (0이면 입력 대기)")]
    public float autoNextDelay = 0f;
}

#if UNITY_EDITOR
[CustomEditor(typeof(CutsceneConfig))]
public class CutsceneConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var typeProp = serializedObject.FindProperty("cutsceneType");
        EditorGUILayout.PropertyField(typeProp);

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
        EditorGUILayout.Space();

        var type = (CutsceneType)typeProp.enumValueIndex;
        if (type == CutsceneType.Video)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("videoClip"));
        }
        else
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bgmClip"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("background"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("girlCharacterSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("boyCharacterSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lines"), true);
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("nextScene"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skippable"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif