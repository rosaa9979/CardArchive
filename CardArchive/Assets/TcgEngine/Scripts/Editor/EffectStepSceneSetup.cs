using System;
using System.Linq;
using TcgEngine.Client;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UI = UnityEngine.UI;

/// <summary>Author the effect-test debugger using Unity serialization and a reusable card prefab.</summary>
public static class EffectStepSceneSetup
{
    public const string PrefabPath = "Assets/TcgEngine/Prefabs/UI/EffectStepCard.prefab";
    static TMP_FontAsset font;
    static Color ink = new Color32(230, 237, 247, 255);
    static Color muted = new Color32(154, 174, 198, 255);

    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before authoring.");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TcgEngine/Fonts/Pretendard-Bold SDF.asset");
        if (font == null) throw new InvalidOperationException("Pretendard font missing.");
        var test = SceneManager.GetSceneByPath(TcgEngine.EffectTestPanel.ScenePath);
        if (!test.isLoaded) test = EditorSceneManager.OpenScene(TcgEngine.EffectTestPanel.ScenePath, OpenSceneMode.Additive);
        if (test.GetRootGameObjects().Any(g => g.GetComponentInChildren<EffectStepPanel>(true) != null))
            throw new InvalidOperationException("Effect test panel already exists; edit it in place.");
        SceneManager.SetActiveScene(test);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            throw new InvalidOperationException("Card prefab already exists; edit it in place.");
        EnsureFolder("Assets/TcgEngine/Prefabs/UI");
        var card = CreateCard();
        var prefab = PrefabUtility.SaveAsPrefabAsset(card.gameObject, PrefabPath).GetComponent<EffectStepItem>();
        UnityEngine.Object.DestroyImmediate(card.gameObject);
        CreatePanel(prefab);
        EditorSceneManager.MarkSceneDirty(test);
        EditorSceneManager.SaveScene(test);

        var ai = SceneManager.GetSceneByPath("Assets/TcgEngine/Resources/Scenes/Tool/AiDebugGame.unity");
        if (!ai.isLoaded) ai = EditorSceneManager.OpenScene("Assets/TcgEngine/Resources/Scenes/Tool/AiDebugGame.unity", OpenSceneMode.Additive);
        foreach (var old in ai.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EffectStepPanel>(true)).ToArray())
        {
            if (old.name != "AbilityDebugPanel" || old.canvas_group == null || !old.canvas_group.transform.IsChildOf(old.transform))
                throw new InvalidOperationException("Unexpected legacy panel hierarchy.");
            Undo.DestroyObjectImmediate(old.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(ai);
        EditorSceneManager.SaveScene(ai);
        EditorSceneManager.CloseScene(ai, true);
        SceneManager.SetActiveScene(test);
        AssetDatabase.SaveAssets();
        Debug.Log("Effect step UI moved; card prefab and test-scene panel saved.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    static RectTransform Rect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = LayerMask.NameToLayer("UI");
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }
    static void Fill(RectTransform rect, float left=0, float top=0, float right=0, float bottom=0)
    {
        rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
        rect.offsetMin=new Vector2(left,bottom); rect.offsetMax=new Vector2(-right,-top);
    }
    static void Box(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(x,-y); rect.sizeDelta=new Vector2(width,height);
    }
    static UI.Image Paint(RectTransform rect, Color color)
    {
        var image=rect.gameObject.AddComponent<UI.Image>(); image.color=color; return image;
    }
    static TMP_Text Text(string name, Transform parent, string value, float size, Color color)
    {
        var rect=Rect(name,parent);
        var text=rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font; text.fontSharedMaterial=font.material; text.fontSize=size;
        text.color=color; text.text=value; text.richText=false; text.raycastTarget=false;
        text.alignment=TextAlignmentOptions.TopLeft; text.textWrappingMode=TextWrappingModes.Normal;
        return text;
    }
    static EffectStepItem CreateCard()
    {
        var root=Rect("EffectStepCard",null); root.sizeDelta=new Vector2(404,170);
        var item=root.gameObject.AddComponent<EffectStepItem>();
        item.border=Paint(root,new Color32(78,167,255,255));
        var size=root.gameObject.AddComponent<UI.LayoutElement>(); size.preferredHeight=170; size.minHeight=170;
        var body=Rect("Body",root); Fill(body,2,2,2,2);
        item.background=Paint(body,new Color32(22,31,47,255)); item.background.raycastTarget=false;
        item.index_text=Text("Order",body,"01",18,muted); Box(item.index_text.rectTransform,12,10,34,24);
        item.owner_text=Text("Owner",body,"내 효과",17,new Color32(78,167,255,255)); Box(item.owner_text.rectTransform,52,10,100,24);
        var next=Text("Next",body,"다음 처리",16,new Color32(255,221,139,255));
        Box(next.rectTransform,285,10,94,24); next.alignment=TextAlignmentOptions.TopRight; item.next_marker=next.gameObject;
        item.source_text=Text("SourceUnit",body,"발동 주체 유닛",22,ink); Box(item.source_text.rectTransform,12,38,374,32);
        item.source_text.enableAutoSizing=true; item.source_text.fontSizeMin=16; item.source_text.fontSizeMax=22;
        item.desc_text=Text("Effect",body,"발동 효과",19,ink); Box(item.desc_text.rectTransform,12,74,374,52);
        item.desc_text.enableAutoSizing=true; item.desc_text.fontSizeMin=14; item.desc_text.fontSizeMax=19;
        item.tag_text=Text("Phase",body,"유발 · 연쇄 깊이 1",15,muted); Box(item.tag_text.rectTransform,12,138,374,22);
        return item;
    }
    static UI.Button Button(Transform parent, string name, string caption, float x, float width, out TMP_Text label)
    {
        var rect=Rect(name,parent); Box(rect,x,76,width,38);
        Paint(rect,new Color32(43,64,89,255));
        var button=rect.gameObject.AddComponent<UI.Button>();
        label=Text("Label",rect,caption,18,ink); Fill(label.rectTransform,6,4,6,4); label.alignment=TextAlignmentOptions.Center;
        return button;
    }
    static RectTransform Scroll(Transform parent, string name, float top, float bottom, out RectTransform viewport)
    {
        var root=Rect(name,parent); Fill(root,16,top,16,bottom);
        var scroll=root.gameObject.AddComponent<UI.ScrollRect>(); scroll.horizontal=false; scroll.vertical=true;
        scroll.movementType=UI.ScrollRect.MovementType.Clamped; scroll.scrollSensitivity=32;
        viewport=Rect("Viewport",root); Fill(viewport);
        Paint(viewport,Color.white); viewport.gameObject.AddComponent<UI.Mask>().showMaskGraphic=false;
        var content=Rect("Content",viewport);
        content.anchorMin=new Vector2(0,1); content.anchorMax=new Vector2(1,1); content.pivot=new Vector2(.5f,1); content.sizeDelta=Vector2.zero;
        var layout=content.gameObject.AddComponent<UI.VerticalLayoutGroup>();
        layout.spacing=10; layout.childControlWidth=true; layout.childControlHeight=true;
        layout.childForceExpandWidth=true; layout.childForceExpandHeight=false;
        var fitter=content.gameObject.AddComponent<UI.ContentSizeFitter>(); fitter.verticalFit=UI.ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport=viewport; scroll.content=content;
        return content;
    }
    static void CreatePanel(EffectStepItem prefab)
    {
        var root=Rect("EffectStepCanvas",null);
        var canvas=root.gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=31000;
        var scaler=root.gameObject.AddComponent<UI.CanvasScaler>(); scaler.uiScaleMode=UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=.5f;
        root.gameObject.AddComponent<UI.GraphicRaycaster>();
        var box=Rect("EffectPipeline",root); box.anchorMin=new Vector2(1,0); box.anchorMax=new Vector2(1,1); box.pivot=new Vector2(1,.5f);
        box.offsetMin=new Vector2(-456,20); box.offsetMax=new Vector2(-20,-20);
        Paint(box,new Color32(13,20,32,248));
        var panel=box.gameObject.AddComponent<EffectStepPanel>(); panel.canvas_group=box.gameObject.AddComponent<CanvasGroup>(); panel.start_hidden=false;
        var title=Text("Title",box,"효과 파이프라인",26,ink); Box(title.rectTransform,16,14,404,36);
        panel.status_text=Text("Status",box,"스텝 모드를 켜면 효과가 단계마다 멈춥니다",16,muted); Box(panel.status_text.rectTransform,16,50,404,24);
        panel.toggle_button=Button(box,"ToggleSteps","효과 스텝: OFF",16,230,out var toggleLabel); panel.toggle_label=toggleLabel;
        panel.next_button=Button(box,"NextStep","다음 · F10",256,164,out var nextLabel);
        var legend=Text("Legend",box,"F9 켜기/끄기   ·   파랑: 내 효과   빨강: 상대",15,muted); Box(legend.rectTransform,16,120,404,25);
        panel.items_root=Scroll(box,"PendingQueue",154,236,out var viewport); panel.item_prefab=prefab;
        panel.empty_text=Text("Empty",viewport,"대기 중인 효과 없음",20,muted); Fill(panel.empty_text.rectTransform,10,30,10,0);
        var logTitle=Text("LogTitle",box,"직전 단계 실행 기록",20,ink);
        logTitle.rectTransform.anchorMin=logTitle.rectTransform.anchorMax=new Vector2(0,0); logTitle.rectTransform.pivot=new Vector2(0,1);
        logTitle.rectTransform.anchoredPosition=new Vector2(16,218); logTitle.rectTransform.sizeDelta=new Vector2(404,28);
        var logRoot=Rect("LogArea",box); logRoot.anchorMin=new Vector2(0,0); logRoot.anchorMax=new Vector2(1,0); logRoot.pivot=new Vector2(.5f,0);
        logRoot.offsetMin=new Vector2(0,16); logRoot.offsetMax=new Vector2(0,186);
        var logContent=Scroll(logRoot,"ExecutionLog",0,0,out var logViewport);
        panel.effect_text=Text("ExecutedEffects",logContent,"(스텝 모드를 켜면 기록됩니다)",17,muted);
        panel.effect_text.rectTransform.sizeDelta=new Vector2(404,40);
    }
}