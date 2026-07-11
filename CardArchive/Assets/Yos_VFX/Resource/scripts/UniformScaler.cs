using UnityEngine;

[ExecuteInEditMode]
public class UniformScaler : MonoBehaviour
{
    [SerializeField] private float scaleValue = 1f;
    private Vector3 originalScale;

    void OnEnable()
    {
        originalScale = transform.localScale;
        AplicarEscala();
    }

    void Update()
    {
        AplicarEscala();
    }



    private void AplicarEscala()
    {
        //   transform.localScale = originalScale * scaleValue;
        transform.localScale = new Vector3(1, 1, 1)* scaleValue;

    }
}