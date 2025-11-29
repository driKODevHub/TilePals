using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

[CustomEditor(typeof(CameraController))]
public class CameraControllerEditor : Editor
{
    private CameraController cameraController;
    private BoxBoundsHandle _boundsHandle = new BoxBoundsHandle();

    private void OnEnable()
    {
        cameraController = (CameraController)target;

        // Налаштування вигляду хендла
        // Дозволяємо тягнути тільки по X та Z (плоский ящик)
        _boundsHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z;
        _boundsHandle.wireframeColor = Color.yellow;
        _boundsHandle.handleColor = Color.yellow;
    }

    private void OnSceneGUI()
    {
        // Працюємо тільки якщо є активні дані
        if (cameraController == null || cameraController.activeGridData == null) return;

        GridDataSO data = cameraController.activeGridData;

        // --- Підготовка координат ---
        Vector3 centerWorld = new Vector3(data.cameraBoundsCenter.x, 0, data.cameraBoundsCenter.y);
        Quaternion rotation = Quaternion.Euler(0, data.cameraBoundsYRotation, 0);

        // --- 1. ПОВОРОТ (Rotation) ---
        // Малюємо ЗЕЛЕНЕ коло (вісь Y) для повороту
        // Робимо це до матриць, щоб диск був у світових координатах (зручніше візуально)
        EditorGUI.BeginChangeCheck();
        Handles.color = Color.green;
        float discSize = Mathf.Max(data.cameraBoundsSize.x, data.cameraBoundsSize.y) / 2f + 2f; // Диск трохи ширше за бокс
        Quaternion newRotation = Handles.Disc(rotation, centerWorld, Vector3.up, discSize, false, 0);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(data, "Rotate Camera Bounds");
            data.cameraBoundsYRotation = newRotation.eulerAngles.y;
            EditorUtility.SetDirty(data);
            data.TriggerOnValuesChanged();
            // Оновлюємо локальну змінну rotation для наступних кроків
            rotation = newRotation;
        }

        // --- 2. ЦЕНТР (Position Pivot) ---
        // Синя сфера + лейбл, як ти хотів
        Handles.color = Color.blue;
        Handles.SphereHandleCap(0, centerWorld, Quaternion.identity, 1f, EventType.Repaint);
        Handles.Label(centerWorld + Vector3.up * 1.5f, $"Bounds Center\n{data.name}");

        // Також додамо Position Handle (стрілочки), щоб можна було просто рухати весь бокс
        EditorGUI.BeginChangeCheck();
        Vector3 newCenterWorld = Handles.PositionHandle(centerWorld, rotation);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(data, "Move Camera Bounds Center");
            data.cameraBoundsCenter = new Vector2(newCenterWorld.x, newCenterWorld.z);
            EditorUtility.SetDirty(data);
            data.TriggerOnValuesChanged();
            // Оновлюємо centerWorld для наступного кроку
            centerWorld = newCenterWorld;
        }

        // --- 3. РОЗМІР (Rect Tool Logic) ---
        // Використовуємо BoxBoundsHandle в локальному просторі об'єкта

        // Зберігаємо поточну матрицю гізмосів
        Matrix4x4 oldMatrix = Handles.matrix;

        // Встановлюємо матрицю в центр нашого бокса з урахуванням повороту
        Handles.matrix = Matrix4x4.TRS(centerWorld, rotation, Vector3.one);

        // Налаштовуємо хендл
        // Важливо: Центр хендла спочатку в (0,0,0) локально
        _boundsHandle.center = Vector3.zero;
        _boundsHandle.size = new Vector3(data.cameraBoundsSize.x, 0, data.cameraBoundsSize.y);

        EditorGUI.BeginChangeCheck();
        _boundsHandle.DrawHandle();

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(data, "Resize Camera Bounds");

            // --- МАГІЯ RECT TOOL ---
            // Коли ми тягнемо за одну сторону, BoxBoundsHandle змінює свій center і size.
            // Наприклад, якщо потягнути праву стінку вправо: size збільшиться, а center зміститься вправо на половину приросту.

            // 1. Оновлюємо розмір (беремо абсолютне значення)
            data.cameraBoundsSize = new Vector2(Mathf.Abs(_boundsHandle.size.x), Mathf.Abs(_boundsHandle.size.z));

            // 2. Оновлюємо центр.
            // _boundsHandle.center — це зміщення відносно нашого півота (0,0 локально).
            // Нам треба перевести це локальне зміщення назад у світові координати з урахуванням повороту.
            Vector3 localCenterOffset = _boundsHandle.center;
            Vector3 worldCenterOffset = rotation * localCenterOffset;

            // Додаємо зміщення до нашого поточного центру
            Vector3 finalNewCenter = centerWorld + worldCenterOffset;

            data.cameraBoundsCenter = new Vector2(finalNewCenter.x, finalNewCenter.z);

            EditorUtility.SetDirty(data);
            data.TriggerOnValuesChanged();
        }

        // Повертаємо матрицю назад
        Handles.matrix = oldMatrix;
    }
}