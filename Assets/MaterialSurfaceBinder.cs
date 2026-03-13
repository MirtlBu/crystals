using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MaterialSurfaceBinder : MonoBehaviour
{
    public GameObject crystalls;
    public UIDocument uiDocument;

    // Хранит выбранный цвет из палитры для каждого свойства шейдера
    private Dictionary<string, Color> pickedColors = new Dictionary<string, Color>();

    // Хранит значение интенсивности для каждого свойства шейдера
    private Dictionary<string, float> intensities = new Dictionary<string, float>();

    void Start()
    {
        VisualElement root = uiDocument.rootVisualElement;
        Renderer rend = crystalls.GetComponent<Renderer>();
        Material mat = rend.material;

        // ---------------------
        // Слайдеры границ
        BindBorderSlider(root, mat, "top_line",    "_top_line");
        BindBorderSlider(root, mat, "bottom_line", "_bottom_line");

        // ---------------------
        // Каналы цвета: палитра + слайдер интенсивности
        BindColorChannel(root, mat, "top_col_btn",    "top_col",    "top_int",    "_top_color");
        BindColorChannel(root, mat, "base_col_btn",   "base_col",   "base_int",   "_base_color");
        BindColorChannel(root, mat, "bottom_col_btn", "bottom_col", "bottom_int", "_bottom_color");
    }

    // Привязывает слайдер к float-свойству шейдера
    void BindBorderSlider(VisualElement root, Material mat, string sliderName, string shaderProperty)
    {
        Slider slider = root.Q<Slider>(sliderName);

        if (slider == null)
        {
            return;
        }

        // устанавливаем начальное значение из материала
        slider.SetValueWithoutNotify(mat.GetFloat(shaderProperty));

        // при изменении — обновляем шейдер
        slider.RegisterValueChangedCallback(evt =>
        {
            mat.SetFloat(shaderProperty, evt.newValue);
        });
    }

    // Привязывает кнопку + палитру + слайдер интенсивности к color-свойству шейдера
    void BindColorChannel(VisualElement root, Material mat,
        string buttonName, string imageName, string intensitySliderName, string shaderProperty)
    {
        Button btn = root.Q<Button>(buttonName);
        Image palette = root.Q<Image>(imageName);
        Slider intSlider = root.Q<Slider>(intensitySliderName);

        // начальные значения
        pickedColors[shaderProperty] = Color.white;

        if (intSlider != null)
        {
            intensities[shaderProperty] = intSlider.value;
        }
        else
        {
            intensities[shaderProperty] = 0f;
        }

        // кнопка открывает/закрывает палитру
        if (btn != null && palette != null)
        {
            btn.clicked += () =>
            {
                bool isVisible = palette.style.display == DisplayStyle.Flex;

                if (isVisible)
                {
                    palette.style.display = DisplayStyle.None;
                }
                else
                {
                    palette.style.display = DisplayStyle.Flex;
                }
            };

            // клик по палитре — берём цвет и закрываем палитру
            palette.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (palette.image is not Texture2D texture)
                {
                    return;
                }

                float u = Mathf.Clamp01(evt.localPosition.x / palette.resolvedStyle.width);
                float v = 1f - Mathf.Clamp01(evt.localPosition.y / palette.resolvedStyle.height);

                pickedColors[shaderProperty] = texture.GetPixelBilinear(u, v);
                ApplyColor(mat, shaderProperty);
            });
        }

        // слайдер intensity меняет яркость цвета
        if (intSlider != null)
        {
            intSlider.RegisterValueChangedCallback(evt =>
            {
                intensities[shaderProperty] = evt.newValue;
                ApplyColor(mat, shaderProperty);
            });
        }
    }

    // Применяет цвет с учётом интенсивности (как HDRI: finalColor = color * 2^intensity)
    void ApplyColor(Material mat, string shaderProperty)
    {
        Color baseColor = pickedColors[shaderProperty];
        float intensity = intensities[shaderProperty];
        float multiplier = Mathf.Pow(2f, intensity);

        mat.SetColor(shaderProperty, baseColor * multiplier);
    }
}
