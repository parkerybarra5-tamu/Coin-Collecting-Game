using Unity.Collections;
using UnityEngine;

public struct BallInput {
    public Vector2 direction;
    public bool jump;

    public BallInput(Vector2 _dir, bool _jump)
    {
        direction = _dir;
        jump = _jump;
    }

    public BallInput(DataStreamReader reader)
    {
        direction = new Vector3(reader.ReadFloat(), reader.ReadFloat());
        jump = (reader.ReadByte() == 1) ? true : false;
    }

    public BallInput(BallInput full, DataStreamReader reader)
    {
        direction = full.direction;
        jump = full.jump;

        byte mask = reader.ReadByte();
        if((mask & 1) == 1)
        {
            jump = true;
        }
        else
        {
            jump = false;
        }

        if((mask & 2) == 2)
        {
            direction.x = reader.ReadFloat();
        }

        if((mask& 4) == 4)
        {
            direction.y = reader.ReadFloat();
        }
    }
}