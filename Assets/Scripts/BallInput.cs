using Unity.Collections;
using UnityEngine;

public struct BallInput {
    public Vector2 direction;
    public bool jump;
    public ushort tick;

    public BallInput(Vector2 _dir, bool _jump, ushort _tick)
    {
        direction = _dir;
        jump = _jump;
        tick = _tick;
    }
}