using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ManaFilter : MonoBehaviour
{
    public Sprite normalSprite;    // 선택 안 된 상태
    public Sprite selectedSprite;  // 선택된 상태

    public List<Toggle> manaToggles; // Index = 마나값 (0 ~ 10)

    public UnityEvent onManaClicked;

    private HashSet<int> filteredManaValues = new HashSet<int>();

    private bool isFiltering = false;

    void Start()
    {
        for (int i = 0; i < manaToggles.Count; i++)
        {
            int manaValue = i;
            manaToggles[i].onValueChanged.AddListener((isOn) => OnManaToggleChanged(manaValue, isOn));
        }

        UpdateFilterState(); // 초기 상태 갱신
        UpdateAllVisuals(); // 초기 비주얼 설정
    }

    public void OnManaToggleChanged(int clickedIndex, bool isOn)
    {
        Debug.Log($"Toggle {clickedIndex} changed to {isOn}, isFiltering: {isFiltering}");

        if (!isFiltering)
        {
            // 모든 토글이 활성화된 상태에서 하나를 클릭
            // 클릭한 것만 남기고 나머지는 비활성화
            for (int i = 0; i < manaToggles.Count; i++)
            {
                if (i == clickedIndex)
                {
                    manaToggles[i].onValueChanged.RemoveAllListeners();
                    manaToggles[i].isOn = true;
                    manaToggles[i].onValueChanged.AddListener((value) => OnManaToggleChanged(i, value));
                }
                else
                {
                    manaToggles[i].onValueChanged.RemoveAllListeners();
                    manaToggles[i].isOn = false;
                    manaToggles[i].onValueChanged.AddListener((value) => OnManaToggleChanged(i, value));
                }
            }
            isFiltering = true;
        }
        else
        {
            // 필터링 상태에서는 일반적인 토글 동작
            // 아무것도 선택되지 않으면 모든 토글 활성화
        }

        UpdateFilterState();
        UpdateAllVisuals();
    }

    void UpdateFilterState()
    {
        filteredManaValues.Clear();

        // 현재 활성화된 토글만 수집
        for (int i = 0; i < manaToggles.Count; i++)
        {
            if (manaToggles[i].isOn)
            {
                filteredManaValues.Add(i);
            }
        }

        // 필터링 상태에서 아무것도 선택되지 않은 경우 → 전부 다시 켜기
        if (isFiltering && filteredManaValues.Count == 0)
        {
            for (int i = 0; i < manaToggles.Count; i++)
            {
                manaToggles[i].onValueChanged.RemoveAllListeners();
                manaToggles[i].isOn = true;
                int index = i;
                manaToggles[i].onValueChanged.AddListener((value) => OnManaToggleChanged(index, value));
            }

            // selectedManaValues 다시 채우기
            filteredManaValues.Clear();
            for (int i = 0; i < manaToggles.Count; i++)
            {
                filteredManaValues.Add(i);
            }
        }

        if (filteredManaValues.Count == manaToggles.Count)
            isFiltering = false;

        onManaClicked?.Invoke();
    }

    // 모든 토글의 비주얼을 업데이트
    void UpdateAllVisuals()
    {
        for (int i = 0; i < manaToggles.Count; i++)
        {
            UpdateToggleVisual(i);
        }
    }

    // 특정 토글의 비주얼을 업데이트
    void UpdateToggleVisual(int toggleIndex)
    {
        if (toggleIndex < 0 || toggleIndex >= manaToggles.Count) return;

        Toggle toggle = manaToggles[toggleIndex];
        Image backgroundImage = toggle.targetGraphic as Image;

        if (backgroundImage == null) return;

        // isOn 상태에 따라 스프라이트 변경
        if (toggle.isOn)
        {
            backgroundImage.sprite = selectedSprite;
        }
        else
        {
            backgroundImage.sprite = normalSprite;
        }
    }

    public HashSet<int> GetFilteredMana()
    {
        return filteredManaValues;
    }
}