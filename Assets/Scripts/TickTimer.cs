[System.Serializable]
public struct TickTimer
{
    public ushort Tick;
    public double Acc;
    public double SecondsPerTick;

    public int Update(double delta)
    {
        Acc += delta;

        int ticks = 0;
        while (Acc > SecondsPerTick)
        {
            ticks++;
            Tick++;
            Acc -= SecondsPerTick;
        }
        return ticks;
    }

    public double TotalTime()
    {
        return Tick * SecondsPerTick + Acc;
    }

    public void RestoreAcc()
    {
        while (Acc < 0)
        {
            Acc += SecondsPerTick;
            Tick--;
        }
        while (Acc >= SecondsPerTick && Tick != 0)
        {
            Acc -= SecondsPerTick;
            Tick++;
        }
    }

    
}
