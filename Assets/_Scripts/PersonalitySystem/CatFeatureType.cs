/// <summary>
/// Глобальний перелік усіх можливих візуальних станів для частин обличчя кота.
/// Використовується для прив'язки емоцій до конкретних 3D-об'єктів.
/// </summary>
public enum CatFeatureType
{
    None = 0,

    // --- Mouths ---
    Mouth_Neutral,
    Mouth_Happy,
    Mouth_Sad,
    Mouth_Surprised,
    Mouth_Shocked,
    Mouth_Annoyed,
    Mouth_Nervous,
    Mouth_Crying,
    Mouth_Sleep,

    // --- Eyes (Shape/Lids) ---
    Eye_Normal,
    Eye_Closed,
    Eye_Wide,
    Eye_Squint,
    Eye_Sad,
    Eye_Happy,
    Eye_Annoyed,
    Eye_Shocked,
    Eye_Sleep,

    // --- Ears ---
    Ear_Up,
    Ear_Down,
    Ear_Back,

    // --- Extra ---
    Tears,
    Blush,
    Sweat
}
