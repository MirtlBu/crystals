using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.VFX;

public class MaterialSurfaceBinder : MonoBehaviour
{
    public GameObject crystalls;
    public UIDocument uiDocument;
    public VisualEffect sparksVFX; // VFX объект sparks — привязать в Inspector

    // Хранит выбранный цвет из палитры для каждого свойства шейдера
    private Dictionary<string, Color> pickedColors = new Dictionary<string, Color>();

    // Хранит значение интенсивности для каждого свойства шейдера
    private Dictionary<string, float> intensities = new Dictionary<string, float>();

    // Какой канал цвета сейчас редактируется (например "_top_color")
    // null означает что панель закрыта
    private string activeProperty = null;

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
        // Начальные значения для каждого цветового канала
        pickedColors["_top_color"]    = Color.white;
        pickedColors["_base_color"]   = Color.white;
        pickedColors["_bottom_color"] = Color.white;

        intensities["_top_color"]    = 0f;
        intensities["_base_color"]   = 0f;
        intensities["_bottom_color"] = 0f;

        // по умолчанию активен base
        activeProperty = "_base_color";

        // ---------------------
        // Общая палитра: генерируем текстуру и настраиваем логику
        SetupSharedPicker(root, mat);

        // ---------------------
        // Кнопки для каждого цветового канала
        BindColorButton(root, "top_col_btn",    "_top_color");
        BindColorButton(root, "base_col_btn",   "_base_color");
        BindColorButton(root, "bottom_col_btn", "_bottom_color");
    }

    // Привязывает слайдер к float-свойству шейдера
    void BindBorderSlider(VisualElement root, Material mat, string sliderName, string shaderProperty)
    {
        Slider slider = root.Q<Slider>(sliderName);

        if (slider == null)
        {
            return;
        }

        slider.SetValueWithoutNotify(mat.GetFloat(shaderProperty));

        slider.RegisterValueChangedCallback(evt =>
        {
            mat.SetFloat(shaderProperty, evt.newValue);
        });
    }

    // Настраивает общую палитру и слайдер intensity
    void SetupSharedPicker(VisualElement root, Material mat)
    {
        Image palette = root.Q<Image>("shared_palette");
        Slider intSlider = root.Q<Slider>("shared_int");

        // присваиваем круглую color wheel текстуру
        palette.image = CreateColorWheelTexture(256);

        // слайдер intensity меняет яркость активного канала
        intSlider.RegisterValueChangedCallback(evt =>
        {
            if (activeProperty == null)
            {
                return;
            }

            intensities[activeProperty] = evt.newValue;
            ApplyColor(mat, activeProperty);
        });

        // клик по палитре — берём цвет для активного канала
        palette.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (activeProperty == null)
            {
                return;
            }

            if (palette.image is not Texture2D texture)
            {
                return;
            }

            float u = Mathf.Clamp01(evt.localPosition.x / palette.resolvedStyle.width);
            float v = 1f - Mathf.Clamp01(evt.localPosition.y / palette.resolvedStyle.height);

            pickedColors[activeProperty] = texture.GetPixelBilinear(u, v);
            ApplyColor(mat, activeProperty);
        });
    }

    // Привязывает кнопку к каналу цвета: переключает активный канал
    void BindColorButton(VisualElement root, string buttonName, string shaderProperty)
    {
        Button btn = root.Q<Button>(buttonName);
        Slider intSlider = root.Q<Slider>("shared_int");

        if (btn == null)
        {
            return;
        }

        btn.clicked += () =>
        {
            // переключаем активный канал
            activeProperty = shaderProperty;

            // обновляем слайдер intensity под выбранный канал
            intSlider.SetValueWithoutNotify(intensities[shaderProperty]);
        };
    }

    // Создаёт круглую color wheel текстуру:
    // угол  → hue (оттенок)
    // расстояние от центра → value (0 = чёрный, 1 = яркий)
    // насыщенность = 1 везде
    Texture2D CreateColorWheelTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;

        float center = size / 2f;
        float radius = size / 2f;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                float dx = x - center;
                float dy = y - center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // пиксели вне круга — прозрачные
                if (distance > radius)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                // угол → hue (0-1)
                float angle = Mathf.Atan2(dy, dx);
                float hue = (angle / (2f * Mathf.PI) + 1f) % 1f;

                // расстояние от центра → value (центр = чёрный, край = яркий)
                float value = distance / radius;

                Color color = Color.HSVToRGB(hue, 1f, value);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }

    // Применяет цвет с учётом интенсивности (HDRI: finalColor = color * 2^intensity)
    void ApplyColor(Material mat, string shaderProperty)
    {
        Color baseColor = pickedColors[shaderProperty];
        float intensity = intensities[shaderProperty];
        float multiplier = Mathf.Pow(2f, intensity);
        Color finalColor = baseColor * multiplier;

        mat.SetColor(shaderProperty, finalColor);

        // если изменился top или base — обновляем градиент VFX
        if (shaderProperty == "_top_color" || shaderProperty == "_base_color")
        {
            ApplyVFXGradient();
        }
    }

    // Строит Gradient из top_color и base_color и отправляет в VFX sparks
    void ApplyVFXGradient()
    {
        if (sparksVFX == null)
        {
            return;
        }

        // вычисляем финальные цвета с учётом intensity
        Color topColor  = pickedColors["_top_color"]  * Mathf.Pow(2f, intensities["_top_color"]);
        Color baseColor = pickedColors["_base_color"] * Mathf.Pow(2f, intensities["_base_color"]);

        // создаём градиент: top_color в начале, base_color в конце
        Gradient gradient = new Gradient();

        GradientColorKey[] colorKeys = new GradientColorKey[2];
        colorKeys[0] = new GradientColorKey(topColor,  0f);
        colorKeys[1] = new GradientColorKey(baseColor, 1f);

        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1f, 0f);
        alphaKeys[1] = new GradientAlphaKey(1f, 1f);

        gradient.SetKeys(colorKeys, alphaKeys);

        sparksVFX.SetGradient("gradient", gradient);
    }
}
