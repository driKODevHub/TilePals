using System;
using UnityEngine;

public static class PersonalityEventManager
{
    // --- ╡ямсчв╡ онд╡╞ ---
    public static event Action<PuzzlePiece> OnPiecePickedUp;
    public static void RaisePiecePickedUp(PuzzlePiece piece) => OnPiecePickedUp?.Invoke(piece);

    public static event Action<PuzzlePiece> OnPieceDropped;
    public static void RaisePieceDropped(PuzzlePiece piece) => OnPieceDropped?.Invoke(piece);

    public static event Action<PuzzlePiece, float> OnPieceShaken;
    public static void RaisePieceShaken(PuzzlePiece piece, float velocity) => OnPieceShaken?.Invoke(piece, velocity);

    public static event Action<PuzzlePiece> OnPiecePlaced;
    public static void RaisePiecePlaced(PuzzlePiece piece) => OnPiecePlaced?.Invoke(piece);

    // --- дндюм╡ онд╡╞ дкъ "цкюдфеммъ" ---
    public static event Action<PuzzlePiece> OnPettingStart;
    public static void RaisePettingStart(PuzzlePiece piece) => OnPettingStart?.Invoke(piece);

    public static event Action<PuzzlePiece, float> OnPettingUpdate;
    public static void RaisePettingUpdate(PuzzlePiece piece, float mouseSpeed) => OnPettingUpdate?.Invoke(piece, mouseSpeed);

    public static event Action<PuzzlePiece> OnPettingEnd;
    public static void RaisePettingEnd(PuzzlePiece piece) => OnPettingEnd?.Invoke(piece);
}