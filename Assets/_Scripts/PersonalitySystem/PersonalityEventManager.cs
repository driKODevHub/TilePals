using System;
using UnityEngine;

public static class PersonalityEventManager
{
    // --- ПЕРЕМІЩЕННЯ ТА ВЗАЄМОДІЯ ---
    public static event Action<PuzzlePiece> OnPiecePickedUp;
    public static void RaisePiecePickedUp(PuzzlePiece piece) => OnPiecePickedUp?.Invoke(piece);

    public static event Action<PuzzlePiece> OnPieceDropped;
    public static void RaisePieceDropped(PuzzlePiece piece) => OnPieceDropped?.Invoke(piece);

    public static event Action<PuzzlePiece, float> OnPieceShaken;
    public static void RaisePieceShaken(PuzzlePiece piece, float velocity) => OnPieceShaken?.Invoke(piece, velocity);

    public static event Action<PuzzlePiece> OnPiecePlaced;
    public static void RaisePiecePlaced(PuzzlePiece piece) => OnPiecePlaced?.Invoke(piece);

    // --- ПЕТТИНГ (ГЛАДЖЕННЯ) ---
    public static event Action<PuzzlePiece> OnPettingStart;
    public static void RaisePettingStart(PuzzlePiece piece) => OnPettingStart?.Invoke(piece);

    public static event Action<PuzzlePiece, float, Vector3, Vector3> OnPettingUpdate;
    public static void RaisePettingUpdate(PuzzlePiece piece, float mouseSpeed, Vector3 worldDelta, Vector3 hitPoint) => OnPettingUpdate?.Invoke(piece, mouseSpeed, worldDelta, hitPoint);

    public static event Action<PuzzlePiece> OnPettingEnd;
    public static void RaisePettingEnd(PuzzlePiece piece) => OnPettingEnd?.Invoke(piece);

    // --- ДИСТАНЦІЙНІ РЕАКЦІЇ (ПРОЛІТ НАД КОТОМ) ---
    public static event Action<PuzzlePiece> OnPieceFlyOver;
    public static void RaisePieceFlyOver(PuzzlePiece stationaryPiece) => OnPieceFlyOver?.Invoke(stationaryPiece);

    // --- ВЗАЄМОДІЯ ТАП (СТУКІТ/ШУРХІТ) ---
    public static event Action<Vector3, float, float> OnFloorTap;
    public static void RaiseFloorTap(Vector3 position, float radius, float strength) => OnFloorTap?.Invoke(position, radius, strength);
}
