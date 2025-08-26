using System;
using UnityEngine;

/// <summary>
/// Статичний клас для керування глобальними подіями, пов'язаними з особистістю фігур.
/// </summary>
public static class PersonalityEventManager
{
    // Подія, що викликається, коли гравець піднімає фігуру.
    public static event Action<PuzzlePiece> OnPiecePickedUp;
    public static void RaisePiecePickedUp(PuzzlePiece piece) => OnPiecePickedUp?.Invoke(piece);

    // Подія, коли гравець опускає/ставить фігуру (повернуто до простої версії).
    public static event Action<PuzzlePiece> OnPieceDropped;
    public static void RaisePieceDropped(PuzzlePiece piece) => OnPieceDropped?.Invoke(piece);

    // НОВА ПОДІЯ: Викликається, коли фігуру різко рухають.
    // float - інтенсивність руху (швидкість).
    public static event Action<PuzzlePiece, float> OnPieceShaken;
    public static void RaisePieceShaken(PuzzlePiece piece, float velocity) => OnPieceShaken?.Invoke(piece, velocity);
}
