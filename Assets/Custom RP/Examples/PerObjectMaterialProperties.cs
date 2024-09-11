using UnityEngine;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour
{
	static MaterialPropertyBlock block;

	void Awake()
	{
		OnValidate();
	}

	void OnValidate()
	{
		if (block == null)
		{
			block = new MaterialPropertyBlock();
		}
		block.SetColor(baseColorId, baseColor);
		block.SetFloat(cutoffId, cutoff);
		block.SetFloat(metallicId, metallic);
		block.SetFloat(smoothnessId, smoothness);
		GetComponent<Renderer>().SetPropertyBlock(block);
	}

	static int baseColorId = Shader.PropertyToID("_BaseColor");
	static int cutoffId = Shader.PropertyToID("_Cutoff");
	static int metallicId = Shader.PropertyToID("_Metallic");
	static int smoothnessId = Shader.PropertyToID("_Smoothness");
	[SerializeField, Range(0f, 1f)]
	float cutoff = 0.5f, metallic = 0f, smoothness = 0.5f;
	[SerializeField]
	Color baseColor = Color.white;
}