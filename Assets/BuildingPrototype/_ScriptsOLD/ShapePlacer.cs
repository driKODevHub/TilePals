using UnityEngine;

public class ShapePlacer : MonoBehaviour
{
    void Update()
    {
        // Обробка кліку лівою кнопкою миші
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); // Створюємо промінь від курсора
            RaycastHit hit; // Змінна для зберігання інформації про зіткнення променя

            // Виконуємо Raycast
            if (Physics.Raycast(ray, out hit))
            {
                // Отримуємо координати сітки з точки зіткнення
                int x = Mathf.FloorToInt(hit.point.x);
                int y = Mathf.FloorToInt(hit.point.z);

                // Доступ до GameManager через його сінглтон для спроби розміщення шейпа
                if (GameManagerOld.Instance != null)
                {
                    GameManagerOld.Instance.TryPlaceShapeAt(x, y);
                }
                else
                {
                    Debug.LogError("GameManager instance is null! Cannot place shape.");
                }
            }
        }
    }
}