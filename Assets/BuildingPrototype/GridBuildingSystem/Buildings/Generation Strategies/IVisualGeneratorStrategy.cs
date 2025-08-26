using UnityEngine;

public interface IVisualGeneratorStrategy
{
    /// <summary>
    /// Генерує візуальне представлення об'єкта.
    /// Всі деталі генерації (матеріали, розміри кубів, випадкові висоти тощо) повинні бути
    /// налаштовані безпосередньо в реалізації цієї стратегії (наприклад, у полях ScriptableObject).
    /// </summary>
    /// <param name="parentTransform">Трансформ, до якого будуть додані згенеровані візуальні елементи.</param>
    /// <param name="placedObjectTypeSO">ScriptableObject, що визначає форму та властивості об'єкта.</param>
    /// <returns>Кореневий GameObject згенерованого візуалу.</returns>
    GameObject GenerateVisual(Transform parentTransform, PlacedObjectTypeSO placedObjectTypeSO);
}