using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TcgEngine.UI;

public class DissolveCardFX : MonoBehaviour
{
    /// <summary>
    /// Card Dissolve 애니메이션 적용
    /// </summary>

    [SerializeField] private CardUI card_ui;
    [SerializeField] private Material mat;
    
    void Start()
    {
        card_ui.SetMaterial(mat);
        card_ui.Dissolve(1f);
        StartCoroutine(AutoDissolveSequence());
    }
    
    private IEnumerator AutoDissolveSequence()
    {
        // 2초 대기
        yield return new WaitForSeconds(1f);

        // 1초에 걸쳐 1→0으로 변화
        float duration = 1f;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;

            float dissolveValue = Mathf.Lerp(1f, 0f, progress);
            card_ui.Dissolve(dissolveValue);

            yield return null;
        }

        card_ui.Dissolve(0f);
    }
}
